using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EPostak.Models;

namespace EPostak;

/// <summary>
/// Manages OAuth JWT tokens for API authentication. Automatically mints tokens
/// via <c>POST /sapi/v1/auth/token</c> and refreshes them via
/// <c>POST /sapi/v1/auth/renew</c> 60 seconds before expiry. Thread-safe.
/// </summary>
internal sealed class TokenManager
{
    private const string SapiTokenPath = "/sapi/v1/auth/token";
    private const string SapiRenewPath = "/sapi/v1/auth/renew";
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _sapiBaseUrl;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    internal TokenManager(HttpClient http, string clientId, string clientSecret, string baseUrl, string? firmId = null)
    {
        _http = http;
        _clientId = clientId;
        _clientSecret = clientSecret;
        // firmId accepted for backward compat but not used for token minting

        // Strip /api/v1 suffix to get the origin for SAPI endpoints
        var idx = baseUrl.IndexOf("/api/v1", StringComparison.Ordinal);
        _sapiBaseUrl = idx >= 0 ? baseUrl[..idx] : baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Get a valid access token, minting or refreshing as needed.
    /// </summary>
    internal async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // Fast path: token still valid
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshBuffer)
            return _accessToken;

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshBuffer)
                return _accessToken;

            await RefreshOrMintAsync(ct).ConfigureAwait(false);
            return _accessToken!;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RefreshOrMintAsync(CancellationToken ct)
    {
        if (_refreshToken is not null && _accessToken is not null)
        {
            try
            {
                await DoRenewAsync(ct).ConfigureAwait(false);
                return;
            }
            catch
            {
                // Refresh failed — fall through to full mint
            }
        }

        await DoMintAsync(ct).ConfigureAwait(false);
    }

    private async Task DoMintAsync(CancellationToken ct)
    {
        var url = $"{_sapiBaseUrl}{SapiTokenPath}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        };

        var json = JsonSerializer.Serialize(body, HttpRequestor.JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new EPostakException($"Token mint network error: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new EPostakException((int)response.StatusCode, $"Token mint failed: {errBody}");
            }

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var data = (await JsonSerializer.DeserializeAsync<TokenResponse>(stream, HttpRequestor.JsonOptions, ct).ConfigureAwait(false))!;
            ApplyTokenResponse(data);
        }
    }

    private async Task DoRenewAsync(CancellationToken ct)
    {
        var url = $"{_sapiBaseUrl}{SapiRenewPath}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _refreshToken!,
        };

        var json = JsonSerializer.Serialize(body, HttpRequestor.JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new EPostakException($"Token renew network error: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new EPostakException((int)response.StatusCode, "Token renew failed");

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var data = (await JsonSerializer.DeserializeAsync<TokenResponse>(stream, HttpRequestor.JsonOptions, ct).ConfigureAwait(false))!;
            ApplyTokenResponse(data);
        }
    }

    private void ApplyTokenResponse(TokenResponse data)
    {
        _accessToken = data.AccessToken;
        _refreshToken = data.RefreshToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn);
    }
}
