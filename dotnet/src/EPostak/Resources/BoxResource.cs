using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// ePošťák Box durable execution layer for staged, scheduled, and retryable
/// Peppol dispatch.
/// </summary>
public sealed class BoxResource
{
    private readonly HttpRequestor _http;

    internal BoxResource(HttpRequestor http) => _http = http;

    public Task<Dictionary<string, object?>> ListAsync(BoxListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("status", @params?.Status),
            ("direction", @params?.Direction),
            ("limit", @params?.Limit?.ToString()),
            ("offset", @params?.Offset?.ToString()));
        return _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Get, $"/box/items{qs}", ct);
    }

    public Task<Dictionary<string, object?>> CreateAsync(BoxCreateRequest request, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Post, "/box/items", request, ct);

    public Task<Dictionary<string, object?>> GetAsync(string itemId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Get, $"/box/items/{Uri.EscapeDataString(itemId)}", ct);

    public Task<Dictionary<string, object?>> ScheduleAsync(string itemId, BoxScheduleRequest request, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Post,
            $"/box/items/{Uri.EscapeDataString(itemId)}/schedule",
            request,
            ct);

    public Task<Dictionary<string, object?>> SendNowAsync(string itemId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Post,
            $"/box/items/{Uri.EscapeDataString(itemId)}/send-now",
            ct);

    public Task<Dictionary<string, object?>> RetryAsync(string itemId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Post,
            $"/box/items/{Uri.EscapeDataString(itemId)}/retry",
            ct);

    public Task<Dictionary<string, object?>> CancelAsync(string itemId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Post,
            $"/box/items/{Uri.EscapeDataString(itemId)}/cancel",
            ct);
}
