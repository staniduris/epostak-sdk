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
        => CreateAsync(request, idempotencyKey: null, ct);

    /// <summary>
    /// Create a new webhook subscription with an optional <c>Idempotency-Key</c>
    /// header so retried calls return the original webhook (and signing secret)
    /// instead of provisioning a duplicate subscription.
    /// </summary>
    /// <param name="request">The webhook URL and list of event types to subscribe to.</param>
    /// <param name="idempotencyKey">Optional idempotency key for safe retries. Null disables the header.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<WebhookDetail> CreateAsync(CreateWebhookRequest request, string? idempotencyKey, CancellationToken ct = default)
        => _http.RequestAsync<WebhookDetail>(HttpMethod.Post, "/webhooks", request, idempotencyKey, ct);

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

    /// <summary>
    /// Send a test event to a webhook endpoint. Useful for verifying your
    /// webhook URL is reachable and responding correctly.
    /// </summary>
    /// <param name="id">The webhook subscription UUID to test.</param>
    /// <param name="webhookEvent">Optional event type to simulate (e.g. "document.created").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test result with success status, HTTP status code, and response time.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Webhooks.TestAsync("wh_abc123");
    /// Console.WriteLine($"Success: {result.Success}, Time: {result.ResponseTime}ms");
    /// </code>
    /// </example>
    public Task<WebhookTestResponse> TestAsync(string id, string? webhookEvent = null, CancellationToken ct = default)
    {
        var qs = webhookEvent != null ? $"?event={Uri.EscapeDataString(webhookEvent)}" : "";
        var body = new Dictionary<string, string>();
        if (webhookEvent != null) body["event"] = webhookEvent;
        return _http.RequestAsync<WebhookTestResponse>(HttpMethod.Post, $"/webhooks/{Uri.EscapeDataString(id)}/test{qs}", body, ct);
    }

    /// <summary>
    /// Send a test event to a webhook endpoint using a typed <see cref="WebhookTestParams"/>.
    /// The event type is sent as a <c>?event=</c> query parameter (server-side gives it
    /// precedence over the body field).
    /// </summary>
    /// <param name="id">The webhook subscription UUID to test.</param>
    /// <param name="params">Test parameters including the optional event type enum value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test result with success status, HTTP status code, and response time.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Webhooks.TestAsync("wh_abc123", new WebhookTestParams
    /// {
    ///     Event = WebhookEvent.DocumentDelivered
    /// });
    /// Console.WriteLine($"Success: {result.Success}, Time: {result.ResponseTime}ms");
    /// </code>
    /// </example>
    public Task<WebhookTestResponse> TestAsync(string id, WebhookTestParams? @params, CancellationToken ct = default)
    {
        var wireEvent = @params?.Event != null ? WebhookEventToString(@params.Event.Value) : null;
        return TestAsync(id, wireEvent, ct);
    }

    /// <summary>Convert a <see cref="WebhookEvent"/> enum value to its wire string (e.g. <c>"document.delivered"</c>).</summary>
    private static string WebhookEventToString(WebhookEvent e) => e switch
    {
        WebhookEvent.DocumentCreated => WebhookEvents.DocumentCreated,
        WebhookEvent.DocumentSent => WebhookEvents.DocumentSent,
        WebhookEvent.DocumentReceived => WebhookEvents.DocumentReceived,
        WebhookEvent.DocumentValidated => WebhookEvents.DocumentValidated,
        WebhookEvent.DocumentDelivered => WebhookEvents.DocumentDelivered,
        WebhookEvent.DocumentRejected => WebhookEvents.DocumentRejected,
        WebhookEvent.DocumentResponseReceived => WebhookEvents.DocumentResponseReceived,
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, "Unknown WebhookEvent value")
    };

    /// <summary>
    /// Get paginated delivery history for a webhook. Use this to inspect
    /// individual delivery attempts, filter by status, and debug failures.
    /// </summary>
    /// <param name="id">The webhook subscription UUID.</param>
    /// <param name="parameters">Optional pagination and filter parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated list of delivery records with total count.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Webhooks.DeliveriesAsync("wh_abc123", new WebhookDeliveriesParams
    /// {
    ///     Status = "FAILED",
    ///     Limit = 50
    /// });
    /// foreach (var d in result.Deliveries)
    ///     Console.WriteLine($"{d.Event}: {d.Status} ({d.Attempts} attempts)");
    /// </code>
    /// </example>
    public Task<WebhookDeliveriesResponse> DeliveriesAsync(string id, WebhookDeliveriesParams? parameters = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (parameters?.Limit != null) query.Add($"limit={parameters.Limit}");
        if (parameters?.Offset != null) query.Add($"offset={parameters.Offset}");
        if (parameters?.Status != null) query.Add($"status={Uri.EscapeDataString(parameters.Status)}");
        if (parameters?.Event != null) query.Add($"event={Uri.EscapeDataString(parameters.Event)}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return _http.RequestAsync<WebhookDeliveriesResponse>(HttpMethod.Get, $"/webhooks/{Uri.EscapeDataString(id)}/deliveries{qs}", ct);
    }

    /// <summary>
    /// Rotate a webhook's HMAC-SHA256 signing secret. Issues a fresh secret
    /// and invalidates the previous one immediately. The new secret is
    /// returned ONCE — store it right away; there is no way to retrieve it
    /// later. Any in-flight deliveries signed with the old secret will stop
    /// verifying on the receiving side. Non-destructive alternative to
    /// delete+recreate when a secret leaks.
    /// </summary>
    /// <param name="id">Webhook UUID whose secret to rotate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new signing secret (only shown once) and a confirmation message.</returns>
    /// <example>
    /// <code>
    /// var res = await client.Webhooks.RotateSecretAsync("wh_abc123");
    /// secretsManager.Save("epostak_webhook_secret", res.Secret);
    /// </code>
    /// </example>
    public Task<WebhookRotateSecretResponse> RotateSecretAsync(string id, CancellationToken ct = default)
    {
        return _http.RequestAsync<WebhookRotateSecretResponse>(
            HttpMethod.Post,
            $"/webhooks/{Uri.EscapeDataString(id)}/rotate-secret",
            ct);
    }

}
