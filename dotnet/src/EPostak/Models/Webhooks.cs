using System.Text.Json.Serialization;

namespace EPostak.Models;

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
}

// ---------------------------------------------------------------------------
// Webhooks
// ---------------------------------------------------------------------------

/// <summary>
/// Request to create a new webhook subscription.
/// </summary>
public sealed class CreateWebhookRequest
{
    /// <summary>The URL that will receive POST requests with event payloads. Must be HTTPS.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

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

    /// <summary>The URL receiving event payloads.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

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

    /// <summary>The URL receiving event payloads.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

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

    /// <summary>Delivery status (e.g. "delivered", "failed", "pending").</summary>
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
}

/// <summary>
/// A webhook subscription with its recent delivery history for debugging.
/// </summary>
public sealed class WebhookWithDeliveries
{
    /// <summary>Unique webhook subscription UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>The URL receiving event payloads.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

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
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Event type (e.g. "document.received", "document.sent").</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>Timestamp when the event was created (ISO 8601).</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    /// <summary>Event payload containing event-specific data (e.g. document ID, status).</summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = [];
}

/// <summary>
/// Response from pulling events from the webhook queue.
/// </summary>
public sealed class WebhookQueueResponse
{
    /// <summary>List of pending queue events.</summary>
    [JsonPropertyName("items")]
    public List<WebhookQueueItem> Items { get; set; } = [];

    /// <summary>True if there are more events available beyond this batch. Pull again to get more.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
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

    /// <summary>Event payload containing event-specific data.</summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = [];

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
    [JsonPropertyName("events")]
    public List<WebhookQueueAllEvent> Events { get; set; } = [];

    /// <summary>Total number of events returned in this response.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
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
