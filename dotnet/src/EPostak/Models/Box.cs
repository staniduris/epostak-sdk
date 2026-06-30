using System.Text.Json.Serialization;

namespace EPostak.Models;

public sealed class BoxListParams
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("offset")]
    public int? Offset { get; set; }
}

public sealed class BoxScheduleRequest
{
    [JsonPropertyName("scheduledFor")]
    public string ScheduledFor { get; set; } = "";
}

public sealed class BoxCreateRequest
{
    [JsonPropertyName("payloadXml")]
    public string PayloadXml { get; set; } = "";

    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}
