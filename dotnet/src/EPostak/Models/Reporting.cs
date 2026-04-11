using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

/// <summary>
/// Parameters for the statistics endpoint defining the reporting period.
/// </summary>
public sealed class StatisticsParams
{
    /// <summary>Start date of the reporting period in ISO 8601 format (e.g. "2026-01-01"). Defaults to the start of the current month.</summary>
    public string? From { get; set; }

    /// <summary>End date of the reporting period in ISO 8601 format (e.g. "2026-03-31"). Defaults to today.</summary>
    public string? To { get; set; }
}

/// <summary>
/// The date range of the reporting period.
/// </summary>
public sealed class StatisticsPeriod
{
    /// <summary>Start date of the reporting period (ISO 8601).</summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    /// <summary>End date of the reporting period (ISO 8601).</summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = "";
}

/// <summary>
/// Statistics for outbound (sent) documents in the reporting period.
/// </summary>
public sealed class OutboundStats
{
    /// <summary>Total number of outbound documents sent.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Number of documents successfully delivered to the receiver's access point.</summary>
    [JsonPropertyName("delivered")]
    public int Delivered { get; set; }

    /// <summary>Number of documents that failed delivery.</summary>
    [JsonPropertyName("failed")]
    public int Failed { get; set; }
}

/// <summary>
/// Statistics for inbound (received) documents in the reporting period.
/// </summary>
public sealed class InboundStats
{
    /// <summary>Total number of inbound documents received.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Number of received documents that have been acknowledged as processed.</summary>
    [JsonPropertyName("acknowledged")]
    public int Acknowledged { get; set; }

    /// <summary>Number of received documents still awaiting acknowledgement.</summary>
    [JsonPropertyName("pending")]
    public int Pending { get; set; }
}

/// <summary>
/// Aggregate document statistics for a reporting period, broken down by direction (outbound/inbound).
/// </summary>
public sealed class Statistics
{
    /// <summary>The date range this statistics report covers.</summary>
    [JsonPropertyName("period")]
    public StatisticsPeriod Period { get; set; } = new();

    /// <summary>Statistics for outbound (sent) documents.</summary>
    [JsonPropertyName("outbound")]
    public OutboundStats Outbound { get; set; } = new();

    /// <summary>Statistics for inbound (received) documents.</summary>
    [JsonPropertyName("inbound")]
    public InboundStats Inbound { get; set; } = new();
}
