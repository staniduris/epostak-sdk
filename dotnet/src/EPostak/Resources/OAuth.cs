using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Stateless helpers for the <strong>integrator-initiated</strong> OAuth
/// <c>authorization_code</c> + PKCE flow. Use these from your own backend when
/// you want to onboard an end-user firm into ePošťák from inside your own
/// application — the user clicks a "Connect ePošťák" button in your UI, lands
/// on the ePošťák <c>/oauth/authorize</c> consent page, and ePošťák redirects
/// back to your <c>redirect_uri</c> with a <c>code</c>.
/// <para>
/// This is independent of the regular <see cref="AuthResource.TokenAsync"/>
/// flow (which uses <c>client_credentials</c>). Pick one or the other depending
/// on how the firm is linked to you.
/// </para>
/// <para>
/// The OAuth token endpoint lives at <c>https://epostak.sk/api/oauth/token</c> —
/// <strong>not</strong> under <c>/api/v1</c> — so this helper bypasses the
/// configured <see cref="EPostakConfig.BaseUrl"/>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // 1. On every onboarding attempt, generate a fresh PKCE pair.
/// var (codeVerifier, codeChallenge) = OAuth.GeneratePkce();
/// sessions[req.SessionId] = codeVerifier;
///
/// // 2. Build the authorize URL and redirect the user.
/// var url = OAuth.BuildAuthorizeUrl(
///     clientId: Environment.GetEnvironmentVariable("EPOSTAK_OAUTH_CLIENT_ID")!,
///     redirectUri: "https://your-app.com/oauth/epostak/callback",
///     codeChallenge: codeChallenge,
///     state: req.SessionId,
///     scope: "firm:read firm:manage document:send");
/// return Redirect(url);
///
/// // 3. On callback, exchange the code for a token pair.
/// var tokens = await OAuth.ExchangeCodeAsync(
///     code: req.Query["code"]!,
///     codeVerifier: sessions[req.Query["state"]!],
///     clientId: Environment.GetEnvironmentVariable("EPOSTAK_OAUTH_CLIENT_ID")!,
///     clientSecret: Environment.GetEnvironmentVariable("EPOSTAK_OAUTH_CLIENT_SECRET")!,
///     redirectUri: "https://your-app.com/oauth/epostak/callback");
/// </code>
/// </example>
public static class OAuth
{
    /// <summary>Default origin for ePošťák OAuth endpoints. Override for staging.</summary>
    public const string DefaultOrigin = "https://epostak.sk";

    /// <summary>
    /// Generate a fresh PKCE code-verifier + S256 code-challenge pair.
    /// <para>
    /// The <c>CodeVerifier</c> is 43 base64url characters (≈256 bits of
    /// entropy). Store it server-side keyed by <c>state</c> — you must NOT
    /// round-trip it through the user's browser, that defeats PKCE.
    /// </para>
    /// </summary>
    /// <returns>
    /// Tuple of <c>(CodeVerifier, CodeChallenge)</c> where the challenge is
    /// <c>base64url(SHA256(codeVerifier))</c>.
    /// </returns>
    public static (string CodeVerifier, string CodeChallenge) GeneratePkce()
    {
        var entropy = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = Base64UrlEncode(entropy);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(hash);
        return (codeVerifier, codeChallenge);
    }

    /// <summary>
    /// Build a <c>/oauth/authorize</c> URL the integrator can redirect the user
    /// to. Always sets <c>response_type=code</c> and
    /// <c>code_challenge_method=S256</c>.
    /// </summary>
    /// <param name="clientId">Registered OAuth client id.</param>
    /// <param name="redirectUri">Exact-match registered redirect URI.</param>
    /// <param name="codeChallenge">From <see cref="GeneratePkce"/>.</param>
    /// <param name="state">CSRF/session token; echoed back on the callback.</param>
    /// <param name="scope">Optional space-separated scope subset. Omit to receive
    /// the full registered scope list on the consent screen.</param>
    /// <param name="origin">Override the host (defaults to <see cref="DefaultOrigin"/>).</param>
    /// <returns>Absolute authorize URL.</returns>
    public static string BuildAuthorizeUrl(
        string clientId,
        string redirectUri,
        string codeChallenge,
        string state,
        string? scope = null,
        string? origin = null)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId),
            new("redirect_uri", redirectUri),
            new("response_type", "code"),
            new("code_challenge", codeChallenge),
            new("code_challenge_method", "S256"),
            new("state", state),
        };
        if (!string.IsNullOrEmpty(scope))
            query.Add(new KeyValuePair<string, string>("scope", scope));

        var baseUrl = (string.IsNullOrEmpty(origin) ? DefaultOrigin : origin).TrimEnd('/');
        var encoded = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{baseUrl}/oauth/authorize?{encoded}";
    }

    /// <summary>
    /// Exchange an authorization <c>code</c> for an access + refresh token pair
    /// on the OAuth token endpoint. Hits <c>${origin}/api/oauth/token</c>
    /// directly — does not route through <see cref="EPostakConfig.BaseUrl"/>.
    /// <para>
    /// The returned access token is a 15-minute JWT; the refresh token is
    /// 30-day rotating. Persist both server-side keyed by your firm record.
    /// </para>
    /// </summary>
    /// <param name="code">The authorization code from the callback.</param>
    /// <param name="codeVerifier">The verifier paired with the
    /// <c>code_challenge</c> used when starting the flow.</param>
    /// <param name="clientId">Integrator OAuth client id.</param>
    /// <param name="clientSecret">Integrator OAuth client secret.</param>
    /// <param name="redirectUri">Must match the URI used in
    /// <see cref="BuildAuthorizeUrl"/>.</param>
    /// <param name="origin">Override the host (defaults to <see cref="DefaultOrigin"/>).</param>
    /// <param name="httpClient">Optional shared <see cref="HttpClient"/>. When
    /// <c>null</c>, a transient client is created for this call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access + refresh token pair.</returns>
    /// <exception cref="EPostakException">On non-2xx response or transport error.</exception>
    public static async Task<TokenResponse> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string clientId,
        string clientSecret,
        string redirectUri,
        string? origin = null,
        HttpClient? httpClient = null,
        CancellationToken ct = default)
    {
        var baseUrl = (string.IsNullOrEmpty(origin) ? DefaultOrigin : origin).TrimEnd('/');
        var url = $"{baseUrl}/api/oauth/token";

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var ownClient = httpClient is null;
        var client = httpClient ?? new HttpClient();
        try
        {
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new EPostakException($"Network error: {ex.Message}", ex);
            }

            using (response)
            {
                var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new EPostakException(
                        (int)response.StatusCode,
                        string.IsNullOrEmpty(payload) ? "OAuth token exchange failed" : payload);
                }
                if (string.IsNullOrEmpty(payload))
                {
                    return new TokenResponse();
                }
                return JsonSerializer.Deserialize<TokenResponse>(payload, HttpRequestor.JsonOptions)
                    ?? new TokenResponse();
            }
        }
        finally
        {
            if (ownClient) client.Dispose();
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
