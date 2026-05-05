using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Polling-based webhook queue for consuming events without exposing a public endpoint.
/// Events remain in the queue until explicitly acknowledged. This is an alternative to
/// push webhooks for systems behind firewalls or without stable public URLs.
/// </summary>
public sealed class WebhookQueueResource
{
    private readonly HttpRequestor _http;

    internal WebhookQueueResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Pull pending events from the webhook queue. Events remain in the queue until
    /// acknowledged via <see cref="AckAsync"/> or <see cref="BatchAckAsync"/>.
    /// Call this periodically to consume new events.
    /// </summary>
    /// <param name="params">Optional filters: max items to return (1-100) and event type filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending queue items and a flag indicating whether more items are available.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Webhooks.Queue.PullAsync(new WebhookQueueParams
    /// {
    ///     Limit = 10,
    ///     EventType = WebhookEvents.DocumentReceived
    /// });
    /// foreach (var item in response.Items)
    /// {
    ///     Console.WriteLine($"Event {item.EventId}: {item.Event}");
    ///     // Process the event...
    ///     await client.Webhooks.Queue.AckAsync(item.EventId);
    /// }
    /// </code>
    /// </example>
    public Task<WebhookQueueResponse> PullAsync(WebhookQueueParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("limit", @params?.Limit?.ToString()),
            ("event_type", @params?.EventType));
        return _http.RequestAsync<WebhookQueueResponse>(HttpMethod.Get, $"/webhook-queue{qs}", ct);
    }

    /// <summary>
    /// Acknowledge and remove a single event from the queue.
    /// Call this after you have successfully processed the event.
    /// </summary>
    /// <param name="eventId">The queue event UUID to acknowledge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Acknowledgement confirmation with <c>Acknowledged = true</c>.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Webhooks.Queue.AckAsync("evt_abc123");
    /// Console.WriteLine(result.Acknowledged); // true
    /// </code>
    /// </example>
    public Task<AckResponse> AckAsync(string eventId, CancellationToken ct = default)
        => _http.RequestAsync<AckResponse>(HttpMethod.Delete, $"/webhook-queue/{Uri.EscapeDataString(eventId)}", ct);

    /// <summary>
    /// Acknowledge and remove multiple events from the queue in a single request.
    /// More efficient than calling <see cref="AckAsync"/> individually for each event.
    /// </summary>
    /// <param name="eventIds">Collection of queue event UUIDs to acknowledge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Object with the count of acknowledged events.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Webhooks.Queue.PullAsync();
    /// // Process all events...
    /// var ids = response.Items.Select(i => i.EventId);
    /// var result = await client.Webhooks.Queue.BatchAckAsync(ids);
    /// Console.WriteLine($"Acknowledged {result.Acknowledged} events");
    /// </code>
    /// </example>
    public Task<BatchAckResponse> BatchAckAsync(IEnumerable<string> eventIds, CancellationToken ct = default)
        => _http.RequestAsync<BatchAckResponse>(HttpMethod.Post, "/webhook-queue/batch-ack", new { event_ids = eventIds }, ct);

    /// <summary>
    /// Pull pending events across all firms managed by the integrator.
    /// Only available with integrator API keys (<c>sk_int_*</c>). Each event includes
    /// the <c>FirmId</c> so you can route it to the correct client.
    /// </summary>
    /// <param name="params">Optional filters: max items (1-500) and ISO 8601 timestamp cutoff.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Cross-firm events with firm IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Webhooks.Queue.PullAllAsync(new WebhookQueueAllParams
    /// {
    ///     Limit = 100,
    ///     Since = "2026-04-01T00:00:00Z"
    /// });
    /// foreach (var evt in response.Items)
    ///     Console.WriteLine($"[Firm {evt.FirmId}] {evt.Event}: {evt.EventId}");
    /// </code>
    /// </example>
    public Task<WebhookQueueAllResponse> PullAllAsync(WebhookQueueAllParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("limit", @params?.Limit?.ToString()),
            ("since", @params?.Since));
        return _http.RequestAsync<WebhookQueueAllResponse>(HttpMethod.Get, $"/webhook-queue/all{qs}", ct);
    }

    /// <summary>
    /// Acknowledge events across all firms in a single batch request.
    /// Only available with integrator API keys (<c>sk_int_*</c>).
    /// </summary>
    /// <param name="eventIds">Collection of cross-firm event UUIDs to acknowledge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The count of successfully acknowledged events.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Webhooks.Queue.PullAllAsync();
    /// // Process events...
    /// var ids = response.Items.Select(e => e.EventId);
    /// var result = await client.Webhooks.Queue.BatchAckAllAsync(ids);
    /// Console.WriteLine($"Acknowledged {result.Acknowledged} events");
    /// </code>
    /// </example>
    public Task<BatchAckAllResponse> BatchAckAllAsync(IEnumerable<string> eventIds, CancellationToken ct = default)
        => _http.RequestAsync<BatchAckAllResponse>(HttpMethod.Post, "/webhook-queue/all/batch-ack", new { event_ids = eventIds }, ct);
}
