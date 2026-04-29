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
    /// Get document statistics for a specified time period. If no period is provided,
    /// returns stats for the current month. Use <see cref="StatisticsParams.Period"/>
    /// for a convenience selector (<c>Month</c>/<c>Quarter</c>/<c>Year</c>) or
    /// <c>From</c>/<c>To</c> for an explicit date range.
    /// </summary>
    /// <param name="params">Optional period selector or explicit date range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Counts split by direction (sent/received) plus delivery rate and top parties.</returns>
    /// <example>
    /// <code>
    /// var stats = await client.Reporting.StatisticsAsync(new StatisticsParams
    /// {
    ///     Period = ReportingPeriod.Month
    /// });
    /// Console.WriteLine($"{stats.Sent.Total} sent, {stats.Received.Total} received");
    /// Console.WriteLine($"Delivery rate: {stats.DeliveryRate:P1}");
    /// </code>
    /// </example>
    public Task<Statistics> StatisticsAsync(StatisticsParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("period", PeriodToString(@params?.Period)),
            ("from", @params?.From),
            ("to", @params?.To));
        return _http.RequestAsync<Statistics>(HttpMethod.Get, $"/reporting/statistics{qs}", ct);
    }

    private static string? PeriodToString(ReportingPeriod? p) => p switch
    {
        ReportingPeriod.Month => "month",
        ReportingPeriod.Quarter => "quarter",
        ReportingPeriod.Year => "year",
        _ => null,
    };
}
