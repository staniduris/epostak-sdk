using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Pull API — Inbound documents
// ---------------------------------------------------------------------------

/// <summary>
/// Query parameters for <c>GET /inbound/documents</c> (Pull API).
/// </summary>
public sealed class InboundListParams
{
    /// <summary>Cursor from the previous page's <c>next_cursor</c>. Omit for the first page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Maximum number of documents to return (1–500, default 100).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter by document kind (e.g. <c>"invoice"</c>, <c>"credit_note"</c>).</summary>
    public string? Kind { get; set; }

    /// <summary>Filter by sender Peppol ID (e.g. <c>"0245:1234567890"</c>).</summary>
    public string? Sender { get; set; }

    /// <summary>ISO 8601 timestamp — only return documents created after this date.</summary>
    public string? Since { get; set; }
}

/// <summary>
/// A received inbound document returned by the Pull API (<c>GET /inbound/documents</c>
/// and <c>GET /inbound/documents/{id}</c>).
/// </summary>
public sealed class InboundDocument
{
    /// <summary>Unique document UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Document kind (e.g. <c>"invoice"</c>, <c>"credit_note"</c>, <c>"self_billing"</c>).</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    /// <summary>Invoice number as declared in the UBL XML.</summary>
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    /// <summary>Peppol participant identifier of the sender.</summary>
    [JsonPropertyName("sender_peppol_id")]
    public string SenderPeppolId { get; set; } = "";

    /// <summary>Peppol participant identifier of the receiver (your firm).</summary>
    [JsonPropertyName("receiver_peppol_id")]
    public string ReceiverPeppolId { get; set; } = "";

    /// <summary>
    /// Pull API processing status: <c>"received"</c> (unacknowledged) or
    /// <c>"acked"</c> (acknowledged via <c>POST /inbound/documents/{id}/ack</c>).
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Invoice issue date (YYYY-MM-DD), or <c>null</c> for non-invoice types.</summary>
    [JsonPropertyName("issue_date")]
    public string? IssueDate { get; set; }

    /// <summary>Payment due date (YYYY-MM-DD), or <c>null</c> if not present.</summary>
    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    /// <summary>ISO 4217 currency code (e.g. <c>"EUR"</c>).</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Total payable amount including VAT, or <c>null</c> for non-monetary document types.</summary>
    [JsonPropertyName("amount_due")]
    public decimal? AmountDue { get; set; }

    /// <summary>Supplier (sender) party details.</summary>
    [JsonPropertyName("supplier")]
    public Party? Supplier { get; set; }

    /// <summary>Customer (receiver) party details.</summary>
    [JsonPropertyName("customer")]
    public Party? Customer { get; set; }

    /// <summary>ISO 8601 timestamp when this document was received on the Peppol network.</summary>
    [JsonPropertyName("received_at")]
    public string ReceivedAt { get; set; } = "";

    /// <summary>ISO 8601 timestamp when your system acknowledged this document. <c>null</c> if not yet acked.</summary>
    [JsonPropertyName("client_acked_at")]
    public string? ClientAckedAt { get; set; }

    /// <summary>Your optional reference set during acknowledgement. <c>null</c> if not set.</summary>
    [JsonPropertyName("client_reference")]
    public string? ClientReference { get; set; }

    /// <summary>Peppol AS4 message identifier.</summary>
    [JsonPropertyName("peppol_message_id")]
    public string? PeppolMessageId { get; set; }
}

/// <summary>
/// Cursor-paginated response from <c>GET /inbound/documents</c>.
/// </summary>
public sealed class InboundListResponse
{
    /// <summary>Documents in the current page, newest first.</summary>
    [JsonPropertyName("documents")]
    public List<InboundDocument> Documents { get; set; } = [];

    /// <summary>Opaque cursor for the next page, or <c>null</c> when this is the last page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }

    /// <summary>Whether more pages are available beyond this one.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

/// <summary>
/// Request body for <c>POST /inbound/documents/{id}/ack</c>.
/// All fields are optional — an empty body is accepted.
/// </summary>
public sealed class InboundAckParams
{
    /// <summary>
    /// Your own reference string stored alongside the acknowledgement (max 256 chars).
    /// Useful for correlating documents with your internal system IDs.
    /// </summary>
    [JsonPropertyName("client_reference")]
    public string? ClientReference { get; set; }
}

// ---------------------------------------------------------------------------
// Pull API — Outbound documents
// ---------------------------------------------------------------------------

/// <summary>
/// Query parameters for <c>GET /outbound/documents</c> (Pull API).
/// </summary>
public sealed class OutboundListParams
{
    /// <summary>Cursor from the previous page's <c>next_cursor</c>. Omit for the first page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Maximum number of documents to return (1–500, default 100).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter by document kind (e.g. <c>"invoice"</c>, <c>"credit_note"</c>).</summary>
    public string? Kind { get; set; }

    /// <summary>Filter by transport/delivery status (e.g. <c>"sent"</c>, <c>"delivered"</c>, <c>"failed"</c>).</summary>
    public string? Status { get; set; }

    /// <summary>Filter by business status (e.g. <c>"accepted"</c>, <c>"rejected"</c>).</summary>
    public string? BusinessStatus { get; set; }

    /// <summary>Filter by recipient Peppol ID.</summary>
    public string? Recipient { get; set; }

    /// <summary>ISO 8601 timestamp — only return documents created after this date.</summary>
    public string? Since { get; set; }
}

/// <summary>
/// A sent outbound document returned by the Pull API (<c>GET /outbound/documents</c>
/// and <c>GET /outbound/documents/{id}</c>).
/// </summary>
public sealed class OutboundDocument
{
    /// <summary>Unique document UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Document kind (e.g. <c>"invoice"</c>, <c>"credit_note"</c>).</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    /// <summary>Invoice number.</summary>
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    /// <summary>Peppol participant identifier of the sender (your firm).</summary>
    [JsonPropertyName("sender_peppol_id")]
    public string SenderPeppolId { get; set; } = "";

    /// <summary>Peppol participant identifier of the receiver.</summary>
    [JsonPropertyName("receiver_peppol_id")]
    public string ReceiverPeppolId { get; set; } = "";

    /// <summary>
    /// Transport/delivery status of the document
    /// (e.g. <c>"draft"</c>, <c>"sent"</c>, <c>"delivered"</c>, <c>"failed"</c>).
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>
    /// Business status based on the receiver's Invoice Response
    /// (e.g. <c>"accepted"</c>, <c>"rejected"</c>, <c>"under_query"</c>).
    /// <c>null</c> until a response is received.
    /// </summary>
    [JsonPropertyName("business_status")]
    public string? BusinessStatus { get; set; }

    /// <summary>Invoice issue date (YYYY-MM-DD).</summary>
    [JsonPropertyName("issue_date")]
    public string? IssueDate { get; set; }

    /// <summary>Payment due date (YYYY-MM-DD).</summary>
    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Total payable amount including VAT.</summary>
    [JsonPropertyName("amount_due")]
    public decimal? AmountDue { get; set; }

    /// <summary>Supplier (your firm) party details.</summary>
    [JsonPropertyName("supplier")]
    public Party? Supplier { get; set; }

    /// <summary>Customer (receiver) party details.</summary>
    [JsonPropertyName("customer")]
    public Party? Customer { get; set; }

    /// <summary>Peppol AS4 message identifier.</summary>
    [JsonPropertyName("peppol_message_id")]
    public string? PeppolMessageId { get; set; }

    /// <summary>ISO 8601 timestamp when this document was created.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    /// <summary>ISO 8601 timestamp when this document was sent. <c>null</c> if still in draft/queued.</summary>
    [JsonPropertyName("sent_at")]
    public string? SentAt { get; set; }

    /// <summary>
    /// Delivery attempt history. Only present in the single-document response
    /// (<c>GET /outbound/documents/{id}</c>), not in list responses.
    /// </summary>
    [JsonPropertyName("attempt_history")]
    public List<Dictionary<string, object>>? AttemptHistory { get; set; }
}

/// <summary>
/// Cursor-paginated response from <c>GET /outbound/documents</c>.
/// </summary>
public sealed class OutboundListResponse
{
    /// <summary>Documents in the current page, newest first.</summary>
    [JsonPropertyName("documents")]
    public List<OutboundDocument> Documents { get; set; } = [];

    /// <summary>Opaque cursor for the next page, or <c>null</c> when this is the last page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }

    /// <summary>Whether more pages are available beyond this one.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

// ---------------------------------------------------------------------------
// Pull API — Outbound events cursor stream
// ---------------------------------------------------------------------------

/// <summary>
/// Query parameters for <c>GET /outbound/events</c>.
/// </summary>
public sealed class OutboundEventsParams
{
    /// <summary>Cursor from the previous page's <c>next_cursor</c>. Omit for the first page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Maximum number of events to return (1–500, default 100).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter to events belonging to a specific outbound document UUID.</summary>
    public string? DocumentId { get; set; }
}

/// <summary>
/// A single entry in the outbound events cursor stream
/// (<c>GET /outbound/events</c>).
/// </summary>
public sealed class OutboundEvent
{
    /// <summary>Event UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>UUID of the outbound document this event belongs to.</summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = "";

    /// <summary>Event type identifier (e.g. <c>"status_changed"</c>, <c>"delivered"</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>Actor that triggered the event (e.g. <c>"system"</c>, <c>"api_key"</c>).</summary>
    [JsonPropertyName("actor")]
    public string? Actor { get; set; }

    /// <summary>Human-readable detail about the event.</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>Arbitrary structured metadata attached to the event.</summary>
    [JsonPropertyName("meta")]
    public Dictionary<string, object> Meta { get; set; } = [];

    /// <summary>ISO 8601 timestamp when the event occurred.</summary>
    [JsonPropertyName("occurred_at")]
    public string OccurredAt { get; set; } = "";
}

/// <summary>
/// Cursor-paginated response from <c>GET /outbound/events</c>.
/// </summary>
public sealed class OutboundEventsResponse
{
    /// <summary>Events in the current page, newest first.</summary>
    [JsonPropertyName("events")]
    public List<OutboundEvent> Events { get; set; } = [];

    /// <summary>Opaque cursor for the next page, or <c>null</c> when this is the last page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }

    /// <summary>Whether more pages are available beyond this one.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
