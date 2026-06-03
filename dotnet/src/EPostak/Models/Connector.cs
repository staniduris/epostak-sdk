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
