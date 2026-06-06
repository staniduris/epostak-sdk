using System.Text.Json.Serialization;

namespace EPostak.Models;

/// <summary>Request body for <c>POST /connector/preflight</c>.</summary>
public sealed class ConnectorPreflightRequest
{
    [JsonPropertyName("receiverPeppolId")]
    public string ReceiverPeppolId { get; set; } = "";

    [JsonPropertyName("document")]
    public Dictionary<string, object?> Document { get; set; } = [];
}

/// <summary>Request body for <c>POST /connector/send</c>.</summary>
public sealed class ConnectorSendRequest
{
    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; set; } = [];
}

public sealed class ConnectorRepairItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("autoFixable")]
    public bool AutoFixable { get; set; }
}

public sealed class ConnectorSafeFix
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("applied")]
    public bool Applied { get; set; }
}

public sealed class ConnectorRepairReport
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("blocking")]
    public List<ConnectorRepairItem> Blocking { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<ConnectorRepairItem> Warnings { get; set; } = [];
}

public sealed class ConnectorPreflightResponse
{
    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";

    [JsonPropertyName("repairReport")]
    public ConnectorRepairReport RepairReport { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<ConnectorRepairItem> Warnings { get; set; } = [];

    [JsonPropertyName("safeFixes")]
    public List<ConnectorSafeFix> SafeFixes { get; set; } = [];

    [JsonPropertyName("recipient")]
    public Dictionary<string, object?>? Recipient { get; set; }

    [JsonPropertyName("documentProfile")]
    public Dictionary<string, object?>? DocumentProfile { get; set; }

    [JsonPropertyName("checks")]
    public Dictionary<string, object?>? Checks { get; set; }
}

public sealed class ConnectorSendResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, object?>? Links { get; set; }
}

public sealed class ConnectorEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("occurredAt")]
    public string? OccurredAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object?>? Data { get; set; }
}

public sealed class ConnectorStatusResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("events")]
    public List<ConnectorEvent> Events { get; set; } = [];
}

public sealed class ConnectorInboxDocument
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("documentNumber")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("senderPeppolId")]
    public string? SenderPeppolId { get; set; }

    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("payloadFormat")]
    public string? PayloadFormat { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorListParams
{
    public string? Cursor { get; set; }
    public int? Limit { get; set; }
}

public sealed class ConnectorInboxListResponse
{
    [JsonPropertyName("documents")]
    public List<ConnectorInboxDocument> Documents { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class ConnectorAckResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; set; }

    [JsonPropertyName("idempotent")]
    public bool? Idempotent { get; set; }

    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }
}

public sealed class ConnectorEventsResponse
{
    [JsonPropertyName("events")]
    public List<ConnectorEvent> Events { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

/// <summary>Request body for <c>POST /connector/autopilot</c>.</summary>
public sealed class ConnectorAutopilotRequest
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "shadow";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; set; } = [];

    [JsonPropertyName("send")]
    public ConnectorSendPolicyOptions? Send { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, object?>? Options { get; set; }
}

public sealed class ConnectorAutopilotRunResponse
{
    [JsonPropertyName("autopilotId")]
    public string AutopilotId { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("lifecycleStatus")]
    public string LifecycleStatus { get; set; } = "";

    [JsonPropertyName("replayed")]
    public bool Replayed { get; set; }

    [JsonPropertyName("preflight")]
    public Dictionary<string, object?>? Preflight { get; set; }

    [JsonPropertyName("repairReport")]
    public Dictionary<string, object?>? RepairReport { get; set; }

    [JsonPropertyName("safeFixes")]
    public List<ConnectorSafeFix> SafeFixes { get; set; } = [];

    [JsonPropertyName("send")]
    public Dictionary<string, object?>? Send { get; set; }

    [JsonPropertyName("status")]
    public Dictionary<string, object?>? Status { get; set; }

    [JsonPropertyName("lastError")]
    public Dictionary<string, object?>? LastError { get; set; }

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("outboxId")]
    public string? OutboxId { get; set; }

    [JsonPropertyName("sentAt")]
    public string? SentAt { get; set; }

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("nextActions")]
    public List<string> NextActions { get; set; } = [];

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorReconcileParams
{
    public string? Status { get; set; }
    public string? Since { get; set; }
}

public sealed class ConnectorReconcileItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("lifecycleStatus")]
    public string LifecycleStatus { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("repairReport")]
    public Dictionary<string, object?>? RepairReport { get; set; }

    [JsonPropertyName("lastError")]
    public Dictionary<string, object?>? LastError { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorReconcileResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<ConnectorReconcileItem> Items { get; set; } = [];
}

public sealed class ConnectorSendPolicyOptions
{
    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "";

    [JsonPropertyName("sendAt")]
    public string? SendAt { get; set; }
}

public sealed class ConnectorZenInputRequest
{
    [JsonPropertyName("customerRef")]
    public string CustomerRef { get; set; } = "";

    [JsonPropertyName("previewOnly")]
    public bool? PreviewOnly { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    [JsonPropertyName("receiver")]
    public Dictionary<string, object?>? Receiver { get; set; }

    [JsonPropertyName("buyer")]
    public Dictionary<string, object?>? Buyer { get; set; }

    [JsonPropertyName("customer")]
    public Dictionary<string, object?>? Customer { get; set; }

    [JsonPropertyName("lines")]
    public List<Dictionary<string, object?>>? Lines { get; set; }

    [JsonPropertyName("items")]
    public List<Dictionary<string, object?>>? Items { get; set; }

    [JsonPropertyName("send")]
    public ConnectorSendPolicyOptions? Send { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

public sealed class ConnectorMailboxListResponse
{
    [JsonPropertyName("mailboxes")]
    public List<Dictionary<string, object?>> Mailboxes { get; set; } = [];
}

public sealed class ConnectorMailboxRepairRequest
{
    [JsonPropertyName("customerRef")]
    public string? CustomerRef { get; set; }
}

public sealed class ConnectorMailboxUpdateResponse
{
    [JsonPropertyName("mailbox")]
    public Dictionary<string, object?> Mailbox { get; set; } = [];
}

public sealed class ConnectorSyncParams
{
    public string? CustomerRef { get; set; }
    public string? Cursor { get; set; }
    public int? Limit { get; set; }
}

public sealed class ConnectorSyncResponse
{
    [JsonPropertyName("items")]
    public List<Dictionary<string, object?>> Items { get; set; } = [];

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class ConnectorActionRequest
{
    [JsonPropertyName("sendAt")]
    public string? SendAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

public sealed class ConnectorActionResponse
{
    [JsonPropertyName("action")]
    public Dictionary<string, object?> Action { get; set; } = [];
}

/// <summary>One item accepted by <c>POST /connector/outbox</c>.</summary>
public sealed class ConnectorOutboxStageItem
{
    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?> Payload { get; set; } = [];
}

/// <summary>Request body for <c>POST /connector/outbox</c>.</summary>
public sealed class ConnectorOutboxStageRequest
{
    [JsonPropertyName("items")]
    public List<ConnectorOutboxStageItem>? Items { get; set; }

    [JsonPropertyName("payload")]
    public Dictionary<string, object?>? Payload { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }
}

public sealed class ConnectorOutboxItem
{
    [JsonPropertyName("outboxId")]
    public string OutboxId { get; set; } = "";

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }

    [JsonPropertyName("documentId")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("repairReport")]
    public ConnectorRepairReport? RepairReport { get; set; }

    [JsonPropertyName("safeFixes")]
    public List<ConnectorSafeFix> SafeFixes { get; set; } = [];

    [JsonPropertyName("lastError")]
    public Dictionary<string, object?>? LastError { get; set; }

    [JsonPropertyName("attemptCount")]
    public int AttemptCount { get; set; }

    [JsonPropertyName("sentAt")]
    public string? SentAt { get; set; }

    [JsonPropertyName("cancelledAt")]
    public string? CancelledAt { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("links")]
    public Dictionary<string, string>? Links { get; set; }
}

public sealed class ConnectorOutboxStageResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("ready")]
    public int? Ready { get; set; }

    [JsonPropertyName("blocked")]
    public int? Blocked { get; set; }

    [JsonPropertyName("staged")]
    public int? Staged { get; set; }

    [JsonPropertyName("items")]
    public List<ConnectorOutboxItem> Items { get; set; } = [];
}

public sealed class ConnectorOutboxListParams
{
    public string? Status { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public sealed class ConnectorOutboxListResponse
{
    [JsonPropertyName("items")]
    public List<ConnectorOutboxItem> Items { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public sealed class ConnectorOutboxSendOptions
{
    [JsonPropertyName("force")]
    public bool? Force { get; set; }
}

public sealed class ConnectorOutboxBatchSendRequest
{
    [JsonPropertyName("ids")]
    public List<string>? Ids { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("force")]
    public bool? Force { get; set; }
}

public sealed class ConnectorOutboxBatchSendResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("sent")]
    public int Sent { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("results")]
    public List<ConnectorOutboxItem> Results { get; set; } = [];
}
