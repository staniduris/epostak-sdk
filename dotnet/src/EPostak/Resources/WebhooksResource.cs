using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Manage webhook subscriptions for push-based event delivery, or use the
/// <see cref="Queue"/> for pull-based polling. Webhooks notify your server
/// when documents are created, sent, received, or validated.
/// </summary>
public sealed class WebhooksResource
{
    private readonly HttpRequestor _http;

    /// <summary>
    /// Access the webhook pull queue for polling-based event consumption.
    /// Use this instead of push webhooks if you cannot expose a public endpoint.
    /// </summary>
    public WebhookQueueResource Queue { get; }

    internal WebhooksResource(HttpRequestor http)
    {
        _http = http;
        Queue = new WebhookQueueResource(http);
    }

    /// <summary>
    /// Create a new webhook subscription. The response includes a one-time <c>Secret</c>
    /// field containing the HMAC-SHA256 signing key -- store it securely, as it cannot
    /// be retrieved again.
    /// </summary>
    /// <param name="request">The webhook URL and list of event types to subscribe to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created webhook with its signing secret (only returned on creation).</returns>
    /// <example>
    /// <code>
    /// var webhook = await client.Webhooks.CreateAsync(new CreateWebhookRequest
    /// {
    ///     Url = "https://example.com/webhooks/epostak",
    ///     Events = new List&lt;string&gt; { WebhookEvents.DocumentReceived, WebhookEvents.DocumentSent }
    /// });
    /// // Store webhook.Secret securely -- it won't be returned again
    /// Console.WriteLine($"Webhook {webhook.Id} created, secret: {webhook.Secret}");
    /// </code>
    /// </example>
    public Task<WebhookDetail> CreateAsync(CreateWebhookRequest request, CancellationToken ct = default)
        => _http.RequestAsync<WebhookDetail>(HttpMethod.Post, "/webhooks", request, ct);

    /// <summary>
    /// List all webhook subscriptions for the current API key.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All webhook subscriptions with their URL, events, and active status.</returns>
    /// <example>
    /// <code>
    /// var webhooks = await client.Webhooks.ListAsync();
    /// foreach (var wh in webhooks)
    ///     Console.WriteLine($"{wh.Id}: {wh.Url} (active: {wh.IsActive})");
    /// </code>
    /// </example>
    public async Task<List<Webhook>> ListAsync(CancellationToken ct = default)
    {
        var res = await _http.RequestAsync<WebhookListResponse>(HttpMethod.Get, "/webhooks", ct).ConfigureAwait(false);
        return res.Data;
    }

    /// <summary>
    /// Get a webhook subscription with its recent delivery history.
    /// Use this to debug delivery failures and inspect response status codes.
    /// </summary>
    /// <param name="id">The webhook subscription UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook details including recent deliveries with status and attempt counts.</returns>
    /// <example>
    /// <code>
    /// var wh = await client.Webhooks.GetAsync("wh_abc123");
    /// foreach (var delivery in wh.Deliveries)
    ///     Console.WriteLine($"  {delivery.Event}: {delivery.Status} (attempts: {delivery.Attempts})");
    /// </code>
    /// </example>
    public Task<WebhookWithDeliveries> GetAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<WebhookWithDeliveries>(HttpMethod.Get, $"/webhooks/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// Update a webhook subscription. Change the URL, subscribed events, or active status.
    /// Only provided fields are updated; null fields remain unchanged.
    /// </summary>
    /// <param name="id">The webhook subscription UUID.</param>
    /// <param name="request">Fields to update: URL, events, and/or active status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated webhook subscription.</returns>
    /// <example>
    /// <code>
    /// var updated = await client.Webhooks.UpdateAsync("wh_abc123", new UpdateWebhookRequest
    /// {
    ///     IsActive = false  // Pause the webhook
    /// });
    /// </code>
    /// </example>
    public Task<Webhook> UpdateAsync(string id, UpdateWebhookRequest request, CancellationToken ct = default)
        => _http.RequestAsync<Webhook>(HttpMethod.Patch, $"/webhooks/{Uri.EscapeDataString(id)}", request, ct);

    /// <summary>
    /// Delete a webhook subscription permanently. Pending deliveries will be cancelled.
    /// </summary>
    /// <param name="id">The webhook subscription UUID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task on success.</returns>
    /// <example>
    /// <code>
    /// await client.Webhooks.DeleteAsync("wh_abc123");
    /// </code>
    /// </example>
    public Task DeleteAsync(string id, CancellationToken ct = default)
        => _http.RequestVoidAsync(HttpMethod.Delete, $"/webhooks/{Uri.EscapeDataString(id)}", ct);
}
