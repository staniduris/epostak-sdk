using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Account information including the linked firm, active subscription plan,
/// and current billing period usage counters.
/// </summary>
public sealed class AccountResource
{
    private readonly HttpRequestor _http;

    internal AccountResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Get account information including the linked firm details, subscription plan status,
    /// and usage counters (inbound/outbound document counts) for the current billing period.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Account details with firm info, plan, and usage data.</returns>
    /// <example>
    /// <code>
    /// var account = await client.Account.GetAsync();
    /// Console.WriteLine($"Firm: {account.Firm.Name} (ICO: {account.Firm.Ico})");
    /// Console.WriteLine($"Plan: {account.Plan.Name} ({account.Plan.Status})");
    /// Console.WriteLine($"Usage: {account.Usage.Outbound} sent, {account.Usage.Inbound} received");
    /// </code>
    /// </example>
    public Task<Account> GetAsync(CancellationToken ct = default)
        => _http.RequestAsync<Account>(HttpMethod.Get, "/account", ct);
}
