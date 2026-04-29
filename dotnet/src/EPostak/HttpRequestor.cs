using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPostak;

/// <summary>
/// Internal HTTP helper used by all resource classes to make authenticated API requests.
/// Handles Bearer token auth, <c>X-Firm-Id</c> header injection, JSON serialization
/// with snake_case naming, multipart file uploads, and error response parsing into
/// <see cref="EPostakException"/> instances.
/// </summary>
internal sealed class HttpRequestor
{
    private readonly HttpClient _http;
    private readonly TokenManager _tokenManager;
    private readonly string? _firmId;
    private readonly string _baseUrl;
    private readonly int _maxRetries;

    /// <summary>HTTP methods that are safe to retry by default.</summary>
    private static readonly HashSet<HttpMethod> RetryableMethods = new() { HttpMethod.Get, HttpMethod.Delete };

    /// <summary>
    /// Shared JSON serializer options used across all requests: snake_case naming,
    /// null properties omitted, case-insensitive deserialization.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Random Rng = new();

    /// <summary>
    /// Create a new requestor bound to a TokenManager and optional firm scope.
    /// </summary>
    /// <param name="http">The underlying HttpClient for sending requests.</param>
    /// <param name="tokenManager">Token manager that provides JWT access tokens.</param>
    /// <param name="baseUrl">Base URL of the API (trailing slash is trimmed).</param>
    /// <param name="firmId">Optional firm UUID to include as <c>X-Firm-Id</c> header on every request.</param>
    /// <param name="maxRetries">Maximum number of retries on 429/5xx for GET/DELETE requests (default 3).</param>
    internal HttpRequestor(HttpClient http, TokenManager tokenManager, string baseUrl, string? firmId, int maxRetries = 3)
    {
        _http = http;
        _tokenManager = tokenManager;
        _baseUrl = baseUrl.TrimEnd('/');
        _firmId = firmId;
        _maxRetries = maxRetries;
    }

    /// <summary>
    /// Send a request without a body and deserialize the JSON response.
    /// </summary>
    /// <typeparam name="T">The response model type to deserialize into.</typeparam>
    /// <param name="method">HTTP method (GET, DELETE, etc.).</param>
    /// <param name="path">API path appended to the base URL (e.g. "/documents").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized API response.</returns>
    internal async Task<T> RequestAsync<T>(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        return await SendAsync<T>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request with a JSON body and deserialize the JSON response.
    /// </summary>
    /// <typeparam name="T">The response model type to deserialize into.</typeparam>
    /// <param name="method">HTTP method (POST, PATCH, PUT, etc.).</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="body">Request body object, serialized to JSON with snake_case naming.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized API response.</returns>
    internal async Task<T> RequestAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
        => await RequestAsync<T>(method, path, body, idempotencyKey: null, ct).ConfigureAwait(false);

    /// <summary>
    /// Send a request with a JSON body and an optional <c>Idempotency-Key</c> header,
    /// then deserialize the JSON response.
    /// </summary>
    internal async Task<T> RequestAsync<T>(HttpMethod method, string path, object body, string? idempotencyKey, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(idempotencyKey))
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        var json = JsonSerializer.Serialize(body, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await SendAsync<T>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request without a body and discard the response (for DELETE/POST that return 204).
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="ct">Cancellation token.</param>
    internal async Task RequestVoidAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        await SendVoidAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request with a JSON body and discard the response.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="body">Request body object, serialized to JSON.</param>
    /// <param name="ct">Cancellation token.</param>
    internal async Task RequestVoidAsync(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(body, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        await SendVoidAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request with a raw string body and a custom Content-Type, then deserialize the JSON response.
    /// Used for endpoints that accept raw XML (e.g. document parsing, validation).
    /// </summary>
    /// <typeparam name="T">The response model type to deserialize into.</typeparam>
    /// <param name="method">HTTP method (typically POST).</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="body">Raw body string to send.</param>
    /// <param name="contentType">Content-Type header value (e.g. <c>application/xml</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized API response.</returns>
    internal async Task<T> RequestRawAsync<T>(HttpMethod method, string path, string body, string contentType, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        request.Content = new StringContent(body, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType) { CharSet = "utf-8" };
        return await SendAsync<T>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a multipart/form-data request (for file uploads) and deserialize the JSON response.
    /// </summary>
    /// <typeparam name="T">The response model type to deserialize into.</typeparam>
    /// <param name="method">HTTP method (typically POST).</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="content">The multipart form data content including file streams.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized API response.</returns>
    internal async Task<T> RequestMultipartAsync<T>(HttpMethod method, string path, MultipartFormDataContent content, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        request.Content = content;
        return await SendAsync<T>(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request and return the raw response body as a byte array.
    /// Used for binary downloads (e.g. PDF files).
    /// </summary>
    /// <param name="method">HTTP method (typically GET).</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw response body bytes.</returns>
    internal async Task<byte[]> RequestBytesAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        using var response = await SendRawAsync(request, ct).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a request and return the raw response body as a string.
    /// Used for text downloads (e.g. UBL XML).
    /// </summary>
    /// <param name="method">HTTP method (typically GET).</param>
    /// <param name="path">API path appended to the base URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw response body string.</returns>
    internal async Task<string> RequestStringAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = await BuildRequestAsync(method, path, ct).ConfigureAwait(false);
        using var response = await SendRawAsync(request, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Build an authenticated HTTP request with a JWT Bearer token and optional X-Firm-Id header.
    /// </summary>
    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string path, CancellationToken ct = default)
    {
        var token = await _tokenManager.GetAccessTokenAsync(ct).ConfigureAwait(false);
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (_firmId is not null)
            request.Headers.Add("X-Firm-Id", _firmId);
        return request;
    }

    /// <summary>
    /// Send a request, check for errors, and deserialize the JSON response body.
    /// Returns <c>default</c> for 204 No Content responses.
    /// Retries on 429/5xx for GET/DELETE methods with exponential backoff.
    /// </summary>
    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        var retryable = RetryableMethods.Contains(request.Method);

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            HttpResponseMessage response;
            // Clone the request for retries (HttpRequestMessage can only be sent once)
            using var req = attempt == 0 ? request : CloneRequest(request);
            try
            {
                response = await _http.SendAsync(req, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new EPostakException($"Network error: {ex.Message}", ex);
            }

            using (response)
            {
                if (retryable && attempt < _maxRetries && ShouldRetry(response))
                {
                    await DelayForRetry(attempt, response, ct).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    await ThrowApiError(response, ct).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return default!;

                var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                return (await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct).ConfigureAwait(false))!;
            }
        }

        throw new EPostakException(0, "Max retries exceeded");
    }

    /// <summary>
    /// Send a request and check for errors, discarding the response body.
    /// Retries on 429/5xx for GET/DELETE methods with exponential backoff.
    /// </summary>
    private async Task SendVoidAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var retryable = RetryableMethods.Contains(request.Method);

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            HttpResponseMessage response;
            using var req = attempt == 0 ? request : CloneRequest(request);
            try
            {
                response = await _http.SendAsync(req, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new EPostakException($"Network error: {ex.Message}", ex);
            }

            using (response)
            {
                if (retryable && attempt < _maxRetries && ShouldRetry(response))
                {
                    await DelayForRetry(attempt, response, ct).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    await ThrowApiError(response, ct).ConfigureAwait(false);
            }

            return;
        }
    }

    /// <summary>
    /// Send a request and return the raw response (caller owns disposal).
    /// Throws <see cref="EPostakException"/> on non-success status codes.
    /// Retries on 429/5xx for GET/DELETE methods with exponential backoff.
    /// </summary>
    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var retryable = RetryableMethods.Contains(request.Method);

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            HttpResponseMessage response;
            using var req = attempt == 0 ? request : CloneRequest(request);
            try
            {
                response = await _http.SendAsync(req, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new EPostakException($"Network error: {ex.Message}", ex);
            }

            if (retryable && attempt < _maxRetries && ShouldRetry(response))
            {
                var retryDelay = GetRetryDelay(attempt, response);
                response.Dispose();
                await Task.Delay(retryDelay, ct).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                using (response)
                    await ThrowApiError(response, ct).ConfigureAwait(false);
            }

            return response;
        }

        throw new EPostakException(0, "Max retries exceeded");
    }

    /// <summary>Check if a response status indicates a retryable condition (429 or 5xx).</summary>
    private static bool ShouldRetry(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        return code == 429 || code >= 500;
    }

    /// <summary>
    /// Calculate and await the backoff delay. Respects Retry-After header on 429.
    /// Formula: min(base_delay * 2^attempt + jitter, 30s).
    /// </summary>
    private static async Task DelayForRetry(int attempt, HttpResponseMessage response, CancellationToken ct)
    {
        await Task.Delay(GetRetryDelay(attempt, response), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculate the backoff delay as a TimeSpan. Safe to call before disposing the response.
    /// </summary>
    private static TimeSpan GetRetryDelay(int attempt, HttpResponseMessage response)
    {
        const double baseDelay = 0.5;
        const double maxDelay = 30.0;

        double delay;

        if ((int)response.StatusCode == 429 && response.Headers.RetryAfter is not null)
        {
            if (response.Headers.RetryAfter.Delta is { } delta)
            {
                delay = Math.Min(delta.TotalSeconds, maxDelay);
            }
            else if (response.Headers.RetryAfter.Date is { } date)
            {
                delay = Math.Min(Math.Max(0, (date - DateTimeOffset.UtcNow).TotalSeconds), maxDelay);
            }
            else
            {
                delay = Math.Min(baseDelay * Math.Pow(2, attempt) + Rng.NextDouble(), maxDelay);
            }
        }
        else
        {
            delay = Math.Min(baseDelay * Math.Pow(2, attempt) + Rng.NextDouble(), maxDelay);
        }

        return TimeSpan.FromSeconds(delay);
    }

    /// <summary>Clone an HttpRequestMessage for retry (original can only be sent once).</summary>
    private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        clone.Content = original.Content;
        return clone;
    }

    /// <summary>
    /// Parse an API error response and throw a structured <see cref="EPostakException"/>.
    /// Handles both the legacy <c>{"error": {"message", "code", "details"}}</c> envelope
    /// and the RFC 7807 <c>application/problem+json</c> envelope (<c>{type, title, detail,
    /// instance, status, ...}</c>). Forwards <c>X-Request-Id</c> and parses
    /// <c>WWW-Authenticate</c> for <c>insufficient_scope</c> rejections.
    /// </summary>
    private static async Task ThrowApiError(HttpResponseMessage response, CancellationToken ct)
    {
        string? message = null;
        string? code = null;
        object? details = null;
        string? type = null;
        string? title = null;
        string? detail = null;
        string? instance = null;
        string? requestId = null;
        string? requiredScope = null;

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // RFC 7807 envelope: { type, title, status, detail, instance, ... }
                    var hasError = root.TryGetProperty("error", out var errorProp);
                    var looksLikeProblem =
                        !hasError &&
                        (root.TryGetProperty("title", out _) || root.TryGetProperty("detail", out _));

                    if (looksLikeProblem)
                    {
                        if (root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                            type = t.GetString();
                        if (root.TryGetProperty("title", out var ti) && ti.ValueKind == JsonValueKind.String)
                            title = ti.GetString();
                        if (root.TryGetProperty("detail", out var dt) && dt.ValueKind == JsonValueKind.String)
                            detail = dt.GetString();
                        if (root.TryGetProperty("instance", out var ins) && ins.ValueKind == JsonValueKind.String)
                            instance = ins.GetString();
                        if (root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                            code = c.GetString();
                        if (root.TryGetProperty("errors", out var errs))
                            details = errs.ToString();
                        message = title ?? detail;
                    }
                    else if (hasError)
                    {
                        if (errorProp.ValueKind == JsonValueKind.String)
                        {
                            message = errorProp.GetString();
                        }
                        else if (errorProp.ValueKind == JsonValueKind.Object)
                        {
                            if (errorProp.TryGetProperty("message", out var msgProp))
                                message = msgProp.GetString();
                            if (errorProp.TryGetProperty("code", out var codeProp))
                                code = codeProp.GetString();
                            if (errorProp.TryGetProperty("details", out var detailsProp))
                                details = detailsProp.ToString();
                            if (errorProp.TryGetProperty("required_scope", out var scopeProp) && scopeProp.ValueKind == JsonValueKind.String)
                                requiredScope = scopeProp.GetString();
                            if (errorProp.TryGetProperty("requestId", out var ridProp) && ridProp.ValueKind == JsonValueKind.String)
                                requestId = ridProp.GetString();
                        }
                    }

                    // Both envelope variants may carry these top-level fields.
                    if (requestId is null && root.TryGetProperty("requestId", out var ridTop) && ridTop.ValueKind == JsonValueKind.String)
                        requestId = ridTop.GetString();
                    if (requiredScope is null && root.TryGetProperty("required_scope", out var rsTop) && rsTop.ValueKind == JsonValueKind.String)
                        requiredScope = rsTop.GetString();
                }
            }
        }
        catch
        {
            // Ignore parse errors — use fallback message
        }

        // Header-derived: X-Request-Id wins as fallback if not in body.
        if (requestId is null && response.Headers.TryGetValues("X-Request-Id", out var ridValues))
        {
            foreach (var v in ridValues) { requestId = v; break; }
        }

        // Parse WWW-Authenticate: Bearer error="insufficient_scope" scope="documents:send"
        if (requiredScope is null && response.Headers.WwwAuthenticate.Count > 0)
        {
            foreach (var auth in response.Headers.WwwAuthenticate)
            {
                var raw = auth.Parameter ?? "";
                if (raw.IndexOf("insufficient_scope", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var match = System.Text.RegularExpressions.Regex.Match(raw, "scope\\s*=\\s*\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    requiredScope = match.Groups[1].Value;
                    break;
                }
            }
        }

        throw new EPostakException(
            (int)response.StatusCode,
            message ?? $"API request failed with status {(int)response.StatusCode}",
            code,
            details,
            type,
            title,
            detail,
            instance,
            requestId,
            requiredScope);
    }

    /// <summary>
    /// Build a URL query string from key-value pairs. Pairs with null values are omitted.
    /// Returns an empty string if no pairs have values.
    /// </summary>
    /// <param name="pairs">Key-value pairs where null values are skipped.</param>
    /// <returns>A query string starting with "?" or an empty string if no values are present.</returns>
    internal static string BuildQuery(params (string key, string? value)[] pairs)
    {
        var parts = new List<string>();
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
        return parts.Count > 0 ? "?" + string.Join("&", parts) : "";
    }
}
