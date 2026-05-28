using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Integrator-aggregate endpoints. Reachable only with an <c>sk_int_*</c>
/// integrator key; per-firm endpoints stay under their existing resources
/// (see <see cref="AccountResource"/>, <see cref="DocumentsResource"/>, etc.).
/// <para>
/// Tier rates are applied to the AGGREGATE document count across all firms
/// the integrator manages, not per-firm. A 100-firm × 50-doc integrator
/// lands in tier 2–3, not tier 1 like a standalone firm would.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var integrator = new EPostakClient(new EPostakConfig { ClientId = "sk_int_xxxxx", ClientSecret = "sk_int_xxxxx" });
/// var info = await integrator.Integrator.Licenses.InfoAsync();
/// Console.WriteLine($"Managed firms: {info.Billable.ManagedFirms}");
/// Console.WriteLine($"Outbound charge: {info.Billable.OutboundCharge} EUR");
/// if (info.ExceedsAutoTier)
///     Console.WriteLine("Manual review – sales handles invoicing");
/// </code>
/// </example>
public sealed class IntegratorResource
{
    /// <summary>Integrator API-key management (<c>/integrator/keys</c>).</summary>
    public IntegratorKeysResource Keys { get; }

    /// <summary>License/billing aggregate views (<c>/integrator/licenses/*</c>).</summary>
    public IntegratorLicensesResource Licenses { get; }

    internal IntegratorResource(HttpRequestor http)
    {
        Keys = new IntegratorKeysResource(http);
        Licenses = new IntegratorLicensesResource(http);
    }
}

/// <summary><c>/integrator/keys</c> — integrator API-key management.</summary>
public sealed class IntegratorKeysResource
{
    private readonly HttpRequestor _http;

    internal IntegratorKeysResource(HttpRequestor http) => _http = http;

    /// <summary>List all API keys for the current integrator.</summary>
    public Task<IntegratorKeysResponse> ListAsync(CancellationToken ct = default)
        => _http.RequestAsync<IntegratorKeysResponse>(HttpMethod.Get, "/integrator/keys", ct);

    /// <summary>Deactivate an integrator API key by UUID or <c>sk_int_*</c> prefix.</summary>
    public Task<DeactivateIntegratorKeyResponse> DeactivateAsync(
        string? keyId = null,
        string? clientId = null,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>();
        if (keyId is not null) body["keyId"] = keyId;
        if (clientId is not null) body["client_id"] = clientId;
        return _http.RequestAsync<DeactivateIntegratorKeyResponse>(HttpMethod.Delete, "/integrator/keys", body, ct);
    }
}

/// <summary>
/// <c>/integrator/licenses/*</c> — billing aggregate views.
/// <para>
/// Volumes above <c>contactThreshold</c> (5 000 / month) flip
/// <see cref="IntegratorLicenseInfo.ExceedsAutoTier"/> to <c>true</c>;
/// auto-billing pauses and sales handles invoicing manually.
/// </para>
/// </summary>
public sealed class IntegratorLicensesResource
{
    private readonly HttpRequestor _http;

    internal IntegratorLicensesResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// Get aggregate plan + current-period usage across every firm an
    /// integrator manages. Tier rates apply to the AGGREGATE counts (not
    /// per-firm summed) — a 100-firm × 50-doc integrator lands in tier 2–3,
    /// not tier 1 like a standalone firm would.
    /// </summary>
    /// <param name="offset">Pagination offset for the per-firm list (default <c>0</c>).</param>
    /// <param name="limit">Page size for the per-firm list (default <c>50</c>, max <c>100</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Plan, period, billable aggregate, non-managed aggregate, tiers, and a paginated per-firm list (sorted by outbound count desc).</returns>
    /// <remarks>
    /// Requires an <c>sk_int_*</c> integrator key with the
    /// <c>account:read</c> scope. The endpoint is integrator-scoped, so no
    /// <c>X-Firm-Id</c> header is sent.
    /// </remarks>
    public Task<IntegratorLicenseInfo> InfoAsync(int? offset = null, int? limit = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("offset", offset?.ToString()),
            ("limit", limit?.ToString()));
        return _http.RequestAsync<IntegratorLicenseInfo>(HttpMethod.Get, $"/integrator/licenses/info{qs}", ct);
    }
}
