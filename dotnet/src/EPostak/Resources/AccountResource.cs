using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Resource for retrieving account information — firm details, subscription
/// plan, and document usage for the current billing period.
/// <para>
/// For key introspection, OAuth token minting, and key rotation see
/// <see cref="AuthResource"/> on <c>client.Auth.*</c>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var account = await client.Account.GetAsync();
/// Console.WriteLine($"Plan: {account.Plan.Name} ({account.Plan.Status})");
/// Console.WriteLine($"Usage: {account.Usage.Outbound} sent, {account.Usage.Inbound} received");
/// </code>
/// </example>
public sealed class AccountResource
{
    private readonly HttpRequestor _http;

    internal AccountResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Get account information for the authenticated API key.
    /// Returns the associated firm, current subscription plan, and usage counters.
    /// </summary>
    public Task<Account> GetAsync(CancellationToken ct = default)
        => _http.RequestAsync<Account>(HttpMethod.Get, "/account", ct);

    /// <summary>Get per-firm plan and current-period license usage.</summary>
    public Task<Dictionary<string, object?>> LicenseInfoAsync(CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Get, "/licenses/info", ct);
}
