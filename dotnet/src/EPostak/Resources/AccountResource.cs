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

    /// <summary>
    /// Describe the authenticated API key, the firm it resolves to, the current subscription plan,
    /// applicable rate limit, and optional integrator info. Useful as a lightweight credential
    /// health check without consuming document quota.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Auth status snapshot: key info, firm info, plan, rate limit, and integrator info.</returns>
    /// <example>
    /// <code>
    /// var status = await client.Account.StatusAsync();
    /// Console.WriteLine($"Firm: {status.Firm.Id} ({status.Firm.PeppolStatus})");
    /// Console.WriteLine($"Plan: {status.Plan.Name} (active: {status.Plan.Active})");
    /// Console.WriteLine($"Rate: {status.RateLimit.PerMinute}/{status.RateLimit.Window}");
    /// </code>
    /// </example>
    public Task<AuthStatusResponse> StatusAsync(CancellationToken ct = default)
        => _http.RequestAsync<AuthStatusResponse>(HttpMethod.Get, "/auth/status", ct);

    /// <summary>
    /// Rotate the current API key's secret. The new key value is returned only once --
    /// store it immediately. The previous secret is invalidated server-side.
    /// </summary>
    /// <remarks>
    /// Not available for integrator subkeys (<c>sk_int_*</c>); the server responds with
    /// HTTP 409 in that case.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rotation result including the new key value, its prefix, and the rotation timestamp.</returns>
    /// <example>
    /// <code>
    /// var rotated = await client.Account.RotateSecretAsync();
    /// SaveSecurely(rotated.Key); // only chance to see it
    /// </code>
    /// </example>
    public Task<RotateSecretResponse> RotateSecretAsync(CancellationToken ct = default)
        => _http.RequestAsync<RotateSecretResponse>(HttpMethod.Post, "/auth/rotate-secret", new { }, ct);
}
