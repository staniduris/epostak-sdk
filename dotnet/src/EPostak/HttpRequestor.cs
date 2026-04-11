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
    private readonly string _apiKey;
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
    /// Create a new requestor bound to an API key and optional firm scope.
    /// </summary>
    /// <param name="http">The underlying HttpClient for sending requests.</param>
    /// <param name="apiKey">Bearer token for API authentication.</param>
    /// <param name="baseUrl">Base URL of the API (trailing slash is trimmed).</param>
    /// <param name="firmId">Optional firm UUID to include as <c>X-Firm-Id</c> header on every request.</param>
    /// <param name="maxRetries">Maximum number of retries on 429/5xx for GET/DELETE requests (default 3).</param>
    internal HttpRequestor(HttpClient http, string apiKey, string baseUrl, string? firmId, int maxRetries = 3)
    {
        _http = http;
        _apiKey = apiKey;
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
        using var request = BuildRequest(method, path);
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
    {
        using var request = BuildRequest(method, path);
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
        using var request = BuildRequest(method, path);
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
        using var request = BuildRequest(method, path);
        var json = JsonSerializer.Serialize(body, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        await SendVoidAsync(request, ct).ConfigureAwait(false);
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
        using var request = BuildRequest(method, path);
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
        using var request = BuildRequest(method, path);
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
        using var request = BuildRequest(method, path);
        using var response = await SendRawAsync(request, ct).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Build an authenticated HTTP request with Bearer token and optional X-Firm-Id header.
    /// </summary>
    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
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
    /// Handles both <c>{"error": "message"}</c> and <c>{"error": {"message": "...", "code": "...", "details": ...}}</c> formats.
    /// </summary>
    private static async Task ThrowApiError(HttpResponseMessage response, CancellationToken ct)
    {
        string? message = null;
        string? code = null;
        object? details = null;

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorProp))
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
                }
            }
        }
        catch
        {
            // Ignore parse errors — use fallback message
        }

        throw new EPostakException(
            (int)response.StatusCode,
            message ?? $"API request failed with status {(int)response.StatusCode}",
            code,
            details);
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
