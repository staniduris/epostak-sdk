using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

/// <summary>
/// Convenience period selector for the statistics endpoint.
/// </summary>
public enum ReportingPeriod
{
    /// <summary>Current calendar month (default).</summary>
    Month,
    /// <summary>Current calendar quarter.</summary>
    Quarter,
    /// <summary>Current calendar year.</summary>
    Year,
}

/// <summary>
/// Parameters for the statistics endpoint defining the reporting period.
/// </summary>
public sealed class StatisticsParams
{
    /// <summary>
    /// Convenience period selector — <c>Month</c> (current calendar month, default),
    /// <c>Quarter</c>, or <c>Year</c>. Ignored when both <see cref="From"/> and
    /// <see cref="To"/> are provided.
    /// </summary>
    public ReportingPeriod? Period { get; set; }

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
/// Direction-specific document counts in the reporting period — total plus a
/// breakdown by UBL doc type (<c>invoice</c>, <c>credit_note</c>, <c>correction</c>,
/// <c>self_billing</c>, ...).
/// </summary>
public sealed class StatisticsDirection
{
    /// <summary>Total documents in this direction over the reporting period.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Counts keyed by UBL doc type (e.g. <c>invoice</c>, <c>credit_note</c>).</summary>
    [JsonPropertyName("by_type")]
    public Dictionary<string, int> ByType { get; set; } = new();
}

/// <summary>
/// Top recipient/sender entry in the statistics response. Each entry aggregates
/// by the <c>(party name, peppol id)</c> tuple over the reporting window.
/// </summary>
public sealed class StatisticsTopParty
{
    /// <summary>Legal name of the party, or <c>null</c> when not present in the source UBL.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Peppol participant ID of the party, or <c>null</c>.</summary>
    [JsonPropertyName("peppol_id")]
    public string? PeppolId { get; set; }

    /// <summary>Number of documents exchanged with this party in the period.</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// Aggregated document statistics for a given time period. Counts are split
/// by direction (<see cref="Sent"/> / <see cref="Received"/>) and broken down
/// by <c>doc_type</c>. <see cref="DeliveryRate"/> is
/// <c>delivered / total_sent</c> rounded to three decimals.
/// </summary>
public sealed class Statistics
{
    /// <summary>The date range this statistics report covers.</summary>
    [JsonPropertyName("period")]
    public StatisticsPeriod Period { get; set; } = new();

    /// <summary>Outbound (sent) document statistics.</summary>
    [JsonPropertyName("sent")]
    public StatisticsDirection Sent { get; set; } = new();

    /// <summary>Inbound (received) document statistics.</summary>
    [JsonPropertyName("received")]
    public StatisticsDirection Received { get; set; } = new();

    /// <summary>
    /// Fraction of outbound documents that reached <c>delivered</c> /
    /// <c>accepted</c> / <c>paid</c> status, rounded to three decimals
    /// (e.g. <c>0.987</c>). Zero when nothing was sent in the period.
    /// </summary>
    [JsonPropertyName("delivery_rate")]
    public double DeliveryRate { get; set; }

    /// <summary>Up to five top recipients of outbound documents, ordered by count.</summary>
    [JsonPropertyName("top_recipients")]
    public List<StatisticsTopParty> TopRecipients { get; set; } = new();

    /// <summary>Up to five top senders of inbound documents, ordered by count.</summary>
    [JsonPropertyName("top_senders")]
    public List<StatisticsTopParty> TopSenders { get; set; } = new();
}
