using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Sub-resource for managing the per-key IP allowlist (Wave 3.1).
/// <para>
/// An empty list means "no IP restriction" — any caller IP is accepted. When
/// the list is non-empty, requests authenticated with this key are rejected
/// (HTTP 403) unless the source IP matches at least one entry. Each entry is
/// either a bare IPv4/IPv6 address or a CIDR block (<c>addr/prefix</c>).
/// </para>
/// </summary>
public sealed class IpAllowlistResource
{
    private readonly HttpRequestor _http;

    internal IpAllowlistResource(HttpRequestor http) => _http = http;

    /// <summary>Read the current IP allowlist for the calling API key.</summary>
    /// <param name="ct">Cancellation token.</param>
    public Task<IpAllowlistResponse> GetAsync(CancellationToken ct = default)
        => _http.RequestAsync<IpAllowlistResponse>(HttpMethod.Get, "/auth/ip-allowlist", ct);

    /// <summary>
    /// Replace the IP allowlist for the calling API key. Pass an empty
    /// collection to clear the restriction. Maximum 50 entries; each must be
    /// either a bare IP or a valid CIDR.
    /// </summary>
    /// <param name="cidrs">IP addresses or CIDR blocks.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<IpAllowlistResponse> UpdateAsync(IEnumerable<string> cidrs, CancellationToken ct = default)
    {
        var body = new IpAllowlistResponse { IpAllowlist = cidrs.ToList() };
        return _http.RequestAsync<IpAllowlistResponse>(HttpMethod.Put, "/auth/ip-allowlist", body, ct);
    }
}

/// <summary>
/// Resource for the OAuth <c>client_credentials</c> flow and key management.
/// <para>
/// The token mint endpoint accepts the API key as the <c>client_secret</c> of
/// an OAuth <c>client_credentials</c> exchange. ePošťák returns a short-lived
/// JWT access token (<c>expires_in: 900</c> seconds) and a 30-day rotating
/// refresh token. Use <see cref="TokenAsync"/> once at startup, cache the
/// access token, and call <see cref="RenewAsync"/> before it expires.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var client = new EPostakClient(new EPostakConfig { ApiKey = "sk_live_xxxxx" });
/// var tokens = await client.Auth.TokenAsync("sk_live_xxxxx");
/// // ... use tokens.AccessToken
/// var renewed = await client.Auth.RenewAsync(tokens.RefreshToken);
/// await client.Auth.RevokeAsync(renewed.RefreshToken, tokenTypeHint: "refresh_token");
/// </code>
/// </example>
public sealed class AuthResource
{
    private readonly HttpRequestor _http;
    private readonly HttpClient _rawHttp;
    private readonly string _baseUrl;

    /// <summary>Sub-resource for the per-key IP allowlist.</summary>
    public IpAllowlistResource IpAllowlist { get; }

    internal AuthResource(HttpRequestor http, HttpClient rawHttp, string baseUrl)
    {
        _http = http;
        _rawHttp = rawHttp;
        _baseUrl = baseUrl.TrimEnd('/');
        IpAllowlist = new IpAllowlistResource(http);
    }

    /// <summary>
    /// Mint an OAuth access token via the <c>client_credentials</c> grant.
    /// <para>
    /// The API key is sent as both the <c>Authorization: Bearer</c> header and
    /// the <c>client_secret</c> body field — the server accepts either, but
    /// doubling up keeps the SDK compatible across spec revisions. For
    /// integrator keys (<c>sk_int_*</c>) you must also pass <paramref name="firmId"/>,
    /// which is forwarded as <c>X-Firm-Id</c> so the issued JWT is bound to the
    /// right tenant.
    /// </para>
    /// </summary>
    /// <param name="apiKey">The API key to mint a token for.</param>
    /// <param name="firmId">Optional firm UUID for integrator keys.</param>
    /// <param name="scope">Optional space-separated scope subset.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TokenResponse> TokenAsync(
        string apiKey,
        string? firmId = null,
        string? scope = null,
        CancellationToken ct = default)
    {
        // The /auth/token call uses an arbitrary apiKey (which may differ from
        // the client's configured key) so we go through the raw HttpClient.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/auth/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (!string.IsNullOrEmpty(firmId))
            request.Headers.Add("X-Firm-Id", firmId);

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = apiKey,
            ["client_secret"] = apiKey,
        };
        if (!string.IsNullOrEmpty(scope))
            body["scope"] = scope;

        var json = JsonSerializer.Serialize(body, HttpRequestor.JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _rawHttp.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new EPostakException($"Network error: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new EPostakException((int)response.StatusCode, errBody);
            }

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return (await JsonSerializer.DeserializeAsync<TokenResponse>(stream, HttpRequestor.JsonOptions, ct).ConfigureAwait(false))!;
        }
    }

    /// <summary>
    /// Exchange a refresh token for a new access + refresh pair. The old
    /// refresh token is invalidated server-side, so always replace your stored
    /// refresh token with the value returned by this call.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<TokenResponse> RenewAsync(string refreshToken, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };
        return _http.RequestAsync<TokenResponse>(HttpMethod.Post, "/auth/renew", body, ct);
    }

    /// <summary>
    /// Revoke an access or refresh token. Idempotent — a 200 is returned even
    /// if the token is unknown or already revoked, so this is safe to call
    /// unconditionally on logout. Pass <paramref name="tokenTypeHint"/> when
    /// you know which variant the token is.
    /// </summary>
    /// <param name="token">The token to revoke.</param>
    /// <param name="tokenTypeHint">Optional hint: <c>"access_token"</c> or <c>"refresh_token"</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<RevokeResponse> RevokeAsync(string token, string? tokenTypeHint = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string> { ["token"] = token };
        if (!string.IsNullOrEmpty(tokenTypeHint))
            body["token_type_hint"] = tokenTypeHint;
        return _http.RequestAsync<RevokeResponse>(HttpMethod.Post, "/auth/revoke", body, ct);
    }

    /// <summary>
    /// Introspect the calling API key without revealing the plaintext secret.
    /// Returns the key metadata, the firm it is bound to, the current plan,
    /// and — for integrator keys — the integrator summary.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task<AuthStatusResponse> StatusAsync(CancellationToken ct = default)
        => _http.RequestAsync<AuthStatusResponse>(HttpMethod.Get, "/auth/status", ct);

    /// <summary>
    /// Rotate the calling API key. The previous key is deactivated immediately
    /// and the new plaintext key is returned ONCE — store it in your secret
    /// manager before continuing. Integrator keys (<c>sk_int_*</c>) are
    /// rejected with HTTP 403; rotate those through the integrator dashboard.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task<RotateSecretResponse> RotateSecretAsync(CancellationToken ct = default)
        => _http.RequestAsync<RotateSecretResponse>(HttpMethod.Post, "/auth/rotate-secret", new { }, ct);
}
