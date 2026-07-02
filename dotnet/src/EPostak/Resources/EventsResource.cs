using EPostak.Models;

namespace EPostak.Resources;

/// <summary>Preferred pull/ack event facade over the webhook queue.</summary>
public sealed class EventsResource
{
    private readonly HttpRequestor _http;

    internal EventsResource(HttpRequestor http) => _http = http;

    public Task<WebhookQueueResponse> PullAsync(WebhookQueueParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("limit", @params?.Limit?.ToString()),
            ("event_type", @params?.EventType));
        return _http.RequestAsync<WebhookQueueResponse>(HttpMethod.Get, $"/events/pull{qs}", ct);
    }

    public Task<AckResponse> AckAsync(string eventId, CancellationToken ct = default)
        => _http.RequestAsync<AckResponse>(HttpMethod.Post, $"/events/{Uri.EscapeDataString(eventId)}/ack", new { }, ct);

    public Task<BatchAckResponse> BatchAckAsync(IEnumerable<string> eventIds, CancellationToken ct = default)
        => _http.RequestAsync<BatchAckResponse>(HttpMethod.Post, "/events/batch-ack", new { event_ids = eventIds }, ct);
}
