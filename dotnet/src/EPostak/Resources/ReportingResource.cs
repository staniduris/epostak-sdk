using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Document statistics and usage reports. Provides aggregate counts for
/// inbound and outbound documents over a configurable time period.
/// </summary>
public sealed class ReportingResource
{
    private readonly HttpRequestor _http;

    internal ReportingResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Get document statistics (sent, delivered, failed, received, acknowledged, pending)
    /// for a specified time period. If no period is provided, returns stats for the current month.
    /// </summary>
    /// <param name="params">Optional date range: <c>From</c> and <c>To</c> in ISO 8601 format (e.g. "2026-01-01").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Outbound and inbound document counts for the specified period.</returns>
    /// <example>
    /// <code>
    /// var stats = await client.Reporting.StatisticsAsync(new StatisticsParams
    /// {
    ///     From = "2026-01-01",
    ///     To = "2026-03-31"
    /// });
    /// Console.WriteLine($"Q1 2026: {stats.Outbound.Total} sent, {stats.Inbound.Total} received");
    /// Console.WriteLine($"  Delivered: {stats.Outbound.Delivered}, Failed: {stats.Outbound.Failed}");
    /// </code>
    /// </example>
    public Task<Statistics> StatisticsAsync(StatisticsParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("from", @params?.From),
            ("to", @params?.To));
        return _http.RequestAsync<Statistics>(HttpMethod.Get, $"/reporting/statistics{qs}", ct);
    }
}
