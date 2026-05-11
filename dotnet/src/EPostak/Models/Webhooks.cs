using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Webhook event enum
// ---------------------------------------------------------------------------

/// <summary>
/// Well-typed webhook event identifier. Use when creating subscriptions or
/// sending test events via <c>client.Webhooks.TestAsync</c>.
/// </summary>
public enum WebhookEvent
{
    /// <summary>Fired when a new document is created (e.g. draft saved).</summary>
    DocumentCreated,
    /// <summary>Fired when a document is successfully sent via the Peppol network.</summary>
    DocumentSent,
    /// <summary>Fired when a new document is received from the Peppol network.</summary>
    DocumentReceived,
    /// <summary>Fired when a document passes or fails Peppol BIS 3.0 validation.</summary>
    DocumentValidated,
    /// <summary>Fired when the receiver's access point confirms AS4 delivery.</summary>
    DocumentDelivered,
    /// <summary>Fired when AS4 delivery fails after all retries.</summary>
    DocumentDeliveryFailed,
    /// <summary>Fired when a sent document is rejected by the receiver or validation.</summary>
    DocumentRejected,
    /// <summary>Fired when a Peppol Invoice Response is received for a sent document.</summary>
    DocumentResponseReceived
}

/// <summary>
/// Parameters for <c>client.Webhooks.TestAsync</c>. The <c>Event</c> type is sent
/// as a <c>?event=</c> query parameter (server-side takes the query param over
/// any body field), letting you simulate specific event types against your endpoint.
/// </summary>
public sealed class WebhookTestParams
{
    /// <summary>
    /// Event type to simulate. Defaults to <c>document.created</c> on the server when omitted.
    /// </summary>
    public WebhookEvent? Event { get; set; }
}

// ---------------------------------------------------------------------------
// Webhook events
// ---------------------------------------------------------------------------

/// <summary>
/// Constants for webhook event types. Use these when creating or filtering webhook subscriptions.
/// </summary>
public static class WebhookEvents
{
    /// <summary>Fired when a new document is created (e.g. draft saved).</summary>
    public const string DocumentCreated = "document.created";
    /// <summary>Fired when a document is successfully sent via the Peppol network.</summary>
    public const string DocumentSent = "document.sent";
    /// <summary>Fired when a new document is received from the Peppol network.</summary>
    public const string DocumentReceived = "document.received";
    /// <summary>Fired when a document passes or fails Peppol BIS 3.0 validation.</summary>
    public const string DocumentValidated = "document.validated";
    /// <summary>Fired when the receiver's access point confirms AS4 delivery.</summary>
    public const string DocumentDelivered = "document.delivered";
    /// <summary>Fired when AS4 delivery fails after all retries.</summary>
    public const string DocumentDeliveryFailed = "document.delivery_failed";
    /// <summary>Fired when a sent document is rejected by the receiver or validation.</summary>
    public const string DocumentRejected = "document.rejected";
    /// <summary>Fired when a Peppol Invoice Response is received for a previously sent document.</summary>
    public const string DocumentResponseReceived = "document.response_received";
}

/// <summary>
/// Webhook delivery status values as reported by <c>GET /webhooks/{id}/deliveries</c>.
/// Values are UPPERCASE strings returned verbatim by the API.
/// </summary>
public static class WebhookDeliveryStatus
{
    /// <summary>Delivery is queued and has not been attempted yet.</summary>
    public const string Pending = "PENDING";
    /// <summary>Delivery succeeded (2xx response from the webhook URL).</summary>
    public const string Success = "SUCCESS";
    /// <summary>Delivery failed permanently after all retries were exhausted.</summary>
    public const string Failed = "FAILED";
    /// <summary>Delivery failed and is scheduled for another retry.</summary>
    public const string Retrying = "RETRYING";
}

// ---------------------------------------------------------------------------
// Webhooks
// ---------------------------------------------------------------------------

/// <summary>
/// Request to create a new webhook subscription.
/// </summary>
public sealed class CreateWebhookRequest
{
    /// <summary>HTTPS URL where payloads will be POSTed, or <c>null</c> for a pull-only subscription.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>List of event types to subscribe to (e.g. "document.received"). Null subscribes to all events.</summary>
    [JsonPropertyName("events")]
    public List<string>? Events { get; set; }
}

/// <summary>
/// Request to update an existing webhook subscription. Only non-null fields are changed.
/// </summary>
public sealed class UpdateWebhookRequest
{
    /// <summary>New webhook URL. Must be HTTPS.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Updated list of event types to subscribe to.</summary>
    [JsonPropertyName("events")]
    public List<string>? Events { get; set; }

    /// <summary>Set to false to pause the webhook (deliveries are queued but not sent).</summary>
    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

/// <summary>
/// A webhook subscription with its configuration and status.
/// </summary>
public sealed class Webhook
{
    /// <summary>Unique webhook subscription UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>HTTPS URL receiving event payloads, or <c>null</c> for pull-only subscriptions.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Event types this webhook is subscribed to.</summary>
    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = [];

    /// <summary>Whether the webhook is currently active and delivering events.</summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    /// <summary>Timestamp when the webhook was created (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// Webhook details returned on creation, including the one-time signing secret.
/// </summary>
public sealed class WebhookDetail
{
    /// <summary>Unique webhook subscription UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>HTTPS URL receiving event payloads, or <c>null</c> for pull-only subscriptions.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Event types this webhook is subscribed to.</summary>
    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = [];

    /// <summary>Whether the webhook is currently active.</summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    /// <summary>Timestamp when the webhook was created (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>HMAC-SHA256 signing secret for verifying webhook payloads. Only returned once on creation -- store it securely.</summary>
    [JsonPropertyName("secret")]
    public string? Secret { get; set; }
}

/// <summary>
/// A single webhook delivery attempt, recording whether the event was successfully
/// delivered to the webhook URL.
/// </summary>
public sealed class WebhookDelivery
{
    /// <summary>Unique delivery UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>The webhook subscription UUID this delivery belongs to.</summary>
    [JsonPropertyName("webhookId")]
    public string WebhookId { get; set; } = "";

    /// <summary>Event type that triggered this delivery (e.g. "document.received").</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Delivery status (UPPERCASE): one of <c>PENDING</c>, <c>SUCCESS</c>, <c>FAILED</c>, <c>RETRYING</c>. See <see cref="WebhookDeliveryStatus"/>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Number of delivery attempts made (retried on failure).</summary>
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    /// <summary>HTTP status code returned by the webhook URL. Null if delivery has not been attempted.</summary>
    [JsonPropertyName("responseStatus")]
    public int? ResponseStatus { get; set; }

    /// <summary>Timestamp when the delivery was created (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>
    /// Idempotency key supplied by the caller on the triggering API request, if any.
    /// <c>null</c> for deliveries triggered by system events or requests without an
    /// idempotency key.
    /// </summary>
    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// A webhook subscription with its recent delivery history for debugging.
/// </summary>
public sealed class WebhookWithDeliveries
{
    /// <summary>Unique webhook subscription UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>HTTPS URL receiving event payloads, or <c>null</c> for pull-only subscriptions.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Event types this webhook is subscribed to.</summary>
    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = [];

    /// <summary>Whether the webhook is currently active.</summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    /// <summary>Timestamp when the webhook was created (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>Recent delivery attempts for this webhook, ordered newest first.</summary>
    [JsonPropertyName("deliveries")]
    public List<WebhookDelivery> Deliveries { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Webhook test & delivery history
// ---------------------------------------------------------------------------

/// <summary>
/// Response from sending a test event to a webhook endpoint.
/// </summary>
public sealed class WebhookTestResponse
{
    /// <summary>Whether the test delivery was successful.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>HTTP status code returned by the webhook URL, or null if the request failed.</summary>
    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    /// <summary>Round-trip response time in milliseconds.</summary>
    [JsonPropertyName("responseTime")]
    public double ResponseTime { get; set; }

    /// <summary>The webhook UUID that was tested.</summary>
    [JsonPropertyName("webhookId")]
    public string WebhookId { get; set; } = "";

    /// <summary>The event type used for the test.</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Error message if the test delivery failed.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// A single delivery record with full detail, used in paginated delivery history.
/// </summary>
public sealed class WebhookDeliveryDetail
{
    /// <summary>Delivery UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>The parent webhook UUID.</summary>
    [JsonPropertyName("webhookId")]
    public string WebhookId { get; set; } = "";

    /// <summary>Event type that triggered this delivery.</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Delivery status (UPPERCASE): <c>PENDING</c>, <c>SUCCESS</c>, <c>FAILED</c>, or <c>RETRYING</c>. See <see cref="WebhookDeliveryStatus"/>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Number of delivery attempts made.</summary>
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    /// <summary>HTTP status code returned by the webhook URL.</summary>
    [JsonPropertyName("responseStatus")]
    public int? ResponseStatus { get; set; }

    /// <summary>Truncated response body from the webhook URL.</summary>
    [JsonPropertyName("responseBody")]
    public string? ResponseBody { get; set; }

    /// <summary>ISO 8601 timestamp of the last delivery attempt.</summary>
    [JsonPropertyName("lastAttemptAt")]
    public string? LastAttemptAt { get; set; }

    /// <summary>ISO 8601 timestamp of the next scheduled retry, or null.</summary>
    [JsonPropertyName("nextRetryAt")]
    public string? NextRetryAt { get; set; }

    /// <summary>ISO 8601 timestamp when the delivery was created.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// Parameters for fetching paginated webhook delivery history.
/// </summary>
public sealed class WebhookDeliveriesParams
{
    /// <summary>Max deliveries to return (1-100, default 20).</summary>
    public int? Limit { get; set; }
    /// <summary>Number of deliveries to skip (default 0).</summary>
    public int? Offset { get; set; }
    /// <summary>Filter by status (UPPERCASE): <c>PENDING</c>, <c>SUCCESS</c>, <c>FAILED</c>, or <c>RETRYING</c>. See <see cref="WebhookDeliveryStatus"/>.</summary>
    public string? Status { get; set; }
    /// <summary>Filter by event type.</summary>
    public string? Event { get; set; }
}

/// <summary>
/// Paginated response of webhook delivery history.
/// </summary>
public sealed class WebhookDeliveriesResponse
{
    /// <summary>Array of delivery records.</summary>
    [JsonPropertyName("deliveries")]
    public List<WebhookDeliveryDetail> Deliveries { get; set; } = [];

    /// <summary>Total number of deliveries matching the filter.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Number of deliveries returned.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>Offset used for pagination.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

/// <summary>
/// Response from rotating a webhook's HMAC-SHA256 signing secret.
/// The new <see cref="Secret"/> is returned ONCE — store it immediately.
/// The previous secret is invalidated; in-flight deliveries signed with
/// it will no longer verify on the receiving side.
/// </summary>
public sealed class WebhookRotateSecretResponse
{
    /// <summary>The webhook UUID whose secret was rotated.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The new HMAC-SHA256 signing secret (only returned once).</summary>
    [JsonPropertyName("secret")]
    public string Secret { get; set; } = string.Empty;

    /// <summary>Human-readable confirmation message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Internal wrapper for the webhooks list API response.
/// </summary>
public sealed class WebhookListResponse
{
    /// <summary>List of webhook subscriptions.</summary>
    [JsonPropertyName("data")]
    public List<Webhook> Data { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Webhook payload types (v1 contract)
// ---------------------------------------------------------------------------

/// <summary>
/// Common envelope shape for every v1 webhook payload, whether received via
/// push (POST to your URL) or pull (one item from the queue).
/// </summary>
public sealed class WebhookPayloadEnvelope
{
    /// <summary>Event type, e.g. <c>"document.sent"</c>.</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Payload schema version. Always <c>"1"</c> for v1.</summary>
    [JsonPropertyName("event_version")]
    public string EventVersion { get; set; } = "1";

    /// <summary>
    /// Per-delivery UUID prefixed with <c>whk_</c>. Echoed in the
    /// <c>X-Webhook-Id</c> header. <c>null</c> for pull-queue items.
    /// </summary>
    [JsonPropertyName("webhook_id")]
    public string? WebhookId { get; set; }

    /// <summary>
    /// Pull-queue row UUID. Use directly with
    /// <c>webhooks.queue.ack(webhookEventId)</c> to acknowledge without an
    /// extra round-trip. <c>null</c> when no pull subscription exists.
    /// </summary>
    [JsonPropertyName("webhook_event_id")]
    public string? WebhookEventId { get; set; }

    /// <summary>ISO 8601 timestamp when the dispatcher emitted this event.</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    /// <summary>Business payload.</summary>
    [JsonPropertyName("data")]
    public WebhookPayloadData Data { get; set; } = new();
}

/// <summary>
/// Business-data shape carried by webhook events. Common fields are always
/// present; event-specific extras are nullable and present only on the
/// relevant event type.
/// </summary>
public sealed class WebhookPayloadData
{
    // --- Always present ---

    /// <summary>Document UUID.</summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = "";

    /// <summary><c>"inbound"</c> or <c>"outbound"</c>.</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    /// <summary>Peppol doctype key (e.g. <c>"invoice"</c>, <c>"credit_note"</c>).</summary>
    [JsonPropertyName("doctype_key")]
    public string DoctypeKey { get; set; } = "";

    /// <summary>Document status after this event's state transition.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Document status before this event, or <c>null</c> for create-type events.</summary>
    [JsonPropertyName("previous_status")]
    public string? PreviousStatus { get; set; }

    // --- Often-present (billing events) ---

    /// <summary>Human invoice/document number (billing events).</summary>
    [JsonPropertyName("document_number")]
    public string? DocumentNumber { get; set; }

    /// <summary>Total amount as string-encoded decimal (billing events).</summary>
    [JsonPropertyName("total_amount")]
    public string? TotalAmount { get; set; }

    /// <summary>ISO 4217 currency code (billing events).</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>YYYY-MM-DD issue date (billing events).</summary>
    [JsonPropertyName("issue_date")]
    public string? IssueDate { get; set; }

    /// <summary>YYYY-MM-DD due date (billing events).</summary>
    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    /// <summary>Sender Peppol participant identifier (e.g. <c>"0245:1122334455"</c>).</summary>
    [JsonPropertyName("sender_peppol_id")]
    public string? SenderPeppolId { get; set; }

    /// <summary>Receiver Peppol participant identifier.</summary>
    [JsonPropertyName("receiver_peppol_id")]
    public string? ReceiverPeppolId { get; set; }

    // --- Event-specific extras ---

    /// <summary><c>document.sent</c> — wall-clock time the AS4 send succeeded.</summary>
    [JsonPropertyName("sent_at")]
    public string? SentAt { get; set; }

    /// <summary><c>document.received</c> — AS4 ingest moment.</summary>
    [JsonPropertyName("received_at")]
    public string? ReceivedAt { get; set; }

    /// <summary><c>document.delivered</c> — when the receiving AP confirmed delivery.</summary>
    [JsonPropertyName("delivered_at")]
    public string? DeliveredAt { get; set; }

    /// <summary><c>document.rejected</c> — when the rejection arrived.</summary>
    [JsonPropertyName("rejected_at")]
    public string? RejectedAt { get; set; }

    /// <summary><c>document.response_received</c> — when the buyer response arrived.</summary>
    [JsonPropertyName("responded_at")]
    public string? RespondedAt { get; set; }

    /// <summary>AS4 EBMS message ID (<c>document.delivered</c>, <c>document.sent</c>, <c>document.received</c>).</summary>
    [JsonPropertyName("as4_message_id")]
    public string? As4MessageId { get; set; }

    /// <summary>
    /// Buyer response code (<c>document.rejected</c> / <c>document.response_received</c>).
    /// One of <c>"RE"</c>, <c>"AB"</c>, <c>"IP"</c>, <c>"UQ"</c>, <c>"CA"</c>, <c>"AP"</c>, <c>"PD"</c>.
    /// </summary>
    [JsonPropertyName("response_code")]
    public string? ResponseCode { get; set; }

    /// <summary>Human-readable rejection / response note.</summary>
    [JsonPropertyName("response_reason")]
    public string? ResponseReason { get; set; }

    /// <summary>
    /// Which side produced the rejection response (<c>document.rejected</c>):
    /// <c>"peer_ap"</c>, <c>"buyer"</c>, or <c>"loopback"</c>.
    /// </summary>
    [JsonPropertyName("responder")]
    public string? Responder { get; set; }

    /// <summary><c>document.delivery_failed</c> — final error message from the queue (truncated 400 chars).</summary>
    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    /// <summary><c>document.delivery_failed</c> — total number of attempts before giving up.</summary>
    [JsonPropertyName("attempts")]
    public int? Attempts { get; set; }
}

// ---------------------------------------------------------------------------
// Webhook pull queue
// ---------------------------------------------------------------------------

/// <summary>
/// Parameters for pulling events from the webhook queue.
/// </summary>
public sealed class WebhookQueueParams
{
    /// <summary>Maximum number of events to return (1-100, default 20).</summary>
    public int? Limit { get; set; }
    /// <summary>Filter by event type (e.g. "document.received"). Null returns all event types.</summary>
    public string? EventType { get; set; }
}

/// <summary>
/// A single event in the webhook pull queue, containing the event type and payload.
/// </summary>
public sealed class WebhookQueueItem
{
    /// <summary>Unique queue event UUID. Use this to acknowledge the event after processing.</summary>
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = "";

    /// <summary>UUID of the firm this event belongs to.</summary>
    [JsonPropertyName("firm_id")]
    public string FirmId { get; set; } = "";

    /// <summary>Event type (e.g. "document.received", "document.sent").</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Timestamp when the event was created (ISO 8601).</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    /// <summary>Event payload. Deserialize as <see cref="WebhookPayloadEnvelope"/>.</summary>
    [JsonPropertyName("payload")]
    public WebhookPayloadEnvelope Payload { get; set; } = new();
}

/// <summary>
/// Response from pulling events from the webhook queue.
/// </summary>
public sealed class WebhookQueueResponse
{
    /// <summary>List of pending queue events.</summary>
    [JsonPropertyName("items")]
    public List<WebhookQueueItem> Items { get; set; } = [];

    /// <summary>Whether more events remain in the queue beyond this page.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

/// <summary>
/// Response from acknowledging a single event from the webhook queue.
/// </summary>
public sealed class AckResponse
{
    /// <summary>Always true on success.</summary>
    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; set; }
}

/// <summary>
/// Response from batch-acknowledging events from the webhook queue.
/// </summary>
public sealed class BatchAckResponse
{
    /// <summary>Number of events successfully acknowledged.</summary>
    [JsonPropertyName("acknowledged")]
    public int Acknowledged { get; set; }
}

// ---------------------------------------------------------------------------
// Webhook queue all (integrator -- cross-firm)
// ---------------------------------------------------------------------------

/// <summary>
/// Parameters for pulling events across all firms (integrator keys only).
/// </summary>
public sealed class WebhookQueueAllParams
{
    /// <summary>Maximum number of events to return (1-500, default 100).</summary>
    public int? Limit { get; set; }
    /// <summary>ISO 8601 timestamp -- only return events created after this date (e.g. "2026-04-01T00:00:00Z").</summary>
    public string? Since { get; set; }
}

/// <summary>
/// A single event in the cross-firm webhook queue, including the firm it belongs to.
/// </summary>
public sealed class WebhookQueueAllEvent
{
    /// <summary>Unique event UUID. Use this to acknowledge the event.</summary>
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = "";

    /// <summary>UUID of the firm this event belongs to.</summary>
    [JsonPropertyName("firm_id")]
    public string FirmId { get; set; } = "";

    /// <summary>Event type (e.g. "document.received", "document.sent").</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Event payload. Deserialize as <see cref="WebhookPayloadEnvelope"/>.</summary>
    [JsonPropertyName("payload")]
    public WebhookPayloadEnvelope Payload { get; set; } = new();

    /// <summary>Timestamp when the event was created (ISO 8601).</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// Response from pulling events across all firms (integrator keys only).
/// </summary>
public sealed class WebhookQueueAllResponse
{
    /// <summary>Cross-firm queue events with firm IDs.</summary>
    [JsonPropertyName("items")]
    public List<WebhookQueueAllEvent> Items { get; set; } = [];

    /// <summary>Whether more events remain in the queue beyond this page.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

/// <summary>
/// Response from batch-acknowledging events across all firms.
/// </summary>
public sealed class BatchAckAllResponse
{
    /// <summary>Number of events successfully acknowledged.</summary>
    [JsonPropertyName("acknowledged")]
    public int Acknowledged { get; set; }
}
