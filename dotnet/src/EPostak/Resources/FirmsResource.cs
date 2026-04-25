using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Manage client firms and their Peppol identifiers.
/// For direct API keys (<c>sk_live_*</c>), this returns your own firm.
/// For integrator keys (<c>sk_int_*</c>), this manages all assigned client firms.
/// </summary>
public sealed class FirmsResource
{
    private readonly HttpRequestor _http;

    internal FirmsResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// List all firms accessible by the current API key.
    /// For integrator keys, returns all assigned client firms.
    /// For direct keys, returns just the firm linked to the key.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of firm summaries with Peppol registration status.</returns>
    /// <example>
    /// <code>
    /// var firms = await client.Firms.ListAsync();
    /// foreach (var firm in firms)
    ///     Console.WriteLine($"{firm.Name} (ICO: {firm.Ico}) - Peppol: {firm.PeppolStatus}");
    /// </code>
    /// </example>
    public async Task<List<FirmSummary>> ListAsync(CancellationToken ct = default)
    {
        var res = await _http.RequestAsync<FirmsListResponse>(HttpMethod.Get, "/firms", ct).ConfigureAwait(false);
        return res.Firms;
    }

    /// <summary>
    /// Get detailed information about a firm including address, tax identifiers,
    /// and all registered Peppol identifiers.
    /// </summary>
    /// <param name="id">The firm UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full firm details including Peppol identifiers and registration date.</returns>
    /// <example>
    /// <code>
    /// var firm = await client.Firms.GetAsync("firm_abc123");
    /// Console.WriteLine($"{firm.Name}, ICO: {firm.Ico}, Peppol: {firm.PeppolId}");
    /// foreach (var pid in firm.PeppolIdentifiers)
    ///     Console.WriteLine($"  {pid.Scheme}:{pid.Identifier}");
    /// </code>
    /// </example>
    public Task<FirmDetail> GetAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<FirmDetail>(HttpMethod.Get, $"/firms/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// List documents (inbound and/or outbound) for a specific firm.
    /// Useful for integrators to browse a client firm's document history.
    /// </summary>
    /// <param name="id">The firm UUID.</param>
    /// <param name="params">Optional filters: pagination and direction (inbound/outbound).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated list of documents for the specified firm.</returns>
    /// <example>
    /// <code>
    /// var docs = await client.Firms.DocumentsAsync("firm_abc123", new FirmDocumentsParams
    /// {
    ///     Direction = DocumentDirection.Inbound,
    ///     Limit = 20
    /// });
    /// Console.WriteLine($"Found {docs.Total} inbound documents");
    /// </code>
    /// </example>
    public Task<InboxListResponse> DocumentsAsync(string id, FirmDocumentsParams? @params = null, CancellationToken ct = default)
    {
        var direction = @params?.Direction switch
        {
            DocumentDirection.Inbound => "inbound",
            DocumentDirection.Outbound => "outbound",
            _ => null
        };
        var qs = HttpRequestor.BuildQuery(
            ("offset", @params?.Offset?.ToString()),
            ("limit", @params?.Limit?.ToString()),
            ("direction", direction));
        return _http.RequestAsync<InboxListResponse>(HttpMethod.Get, $"/firms/{Uri.EscapeDataString(id)}/documents{qs}", ct);
    }

    /// <summary>
    /// Register a Peppol identifier for a firm on the SMP (Service Metadata Publisher).
    /// This makes the firm discoverable and able to receive documents on the Peppol network.
    /// Common schemes: <c>0192</c> (Slovak ICO), <c>0088</c> (EAN/GLN).
    /// </summary>
    /// <param name="id">The firm UUID.</param>
    /// <param name="scheme">The Peppol identifier scheme (e.g. "0192" for Slovak ICO).</param>
    /// <param name="identifier">The identifier value within the scheme (e.g. the ICO number "12345678").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The registered Peppol ID, scheme, identifier, and registration timestamp.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Firms.RegisterPeppolIdAsync("firm_abc123", "0192", "12345678");
    /// Console.WriteLine($"Registered: {result.PeppolId} at {result.RegisteredAt}");
    /// </code>
    /// </example>
    public Task<PeppolIdentifierResponse> RegisterPeppolIdAsync(string id, string scheme, string identifier, CancellationToken ct = default)
        => _http.RequestAsync<PeppolIdentifierResponse>(HttpMethod.Post,
            $"/firms/{Uri.EscapeDataString(id)}/peppol-identifiers",
            new { scheme, identifier }, ct);

    /// <summary>
    /// Link this integrator to a Firm that has already completed FS SR signup
    /// and granted consent. <strong>Lookup-only</strong> — this endpoint cannot
    /// create new Firms. The target Firm must have completed FS SR PFS signup
    /// and granted consent to this integrator before the link succeeds.
    /// Integrator keys only.
    /// </summary>
    /// <remarks>
    /// On error, inspect <see cref="EPostakException.Code"/>:
    /// <list type="bullet">
    ///   <item><description><c>FIRM_NOT_REGISTERED</c> (HTTP 404) — no Firm with that ICO exists yet. Direct the firm to complete FS SR PFS signup before retrying.</description></item>
    ///   <item><description><c>CONSENT_REQUIRED</c> (HTTP 403) — Firm exists but has not granted consent for this integrator to act on its behalf.</description></item>
    ///   <item><description><c>ALREADY_LINKED</c> (HTTP 409) — the integrator already has an active link to this Firm.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="ico">Slovak business registration number (ICO), e.g. "12345678".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The linked firm details and link status.</returns>
    /// <exception cref="EPostakException">When the firm is not registered, consent is missing, the link already exists, or the request fails.</exception>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     var result = await client.Firms.AssignAsync("12345678");
    ///     Console.WriteLine($"Firm: {result.Firm.Name} ({result.Status})");
    /// }
    /// catch (EPostakException ex) when (ex.Code == "FIRM_NOT_REGISTERED")
    /// {
    ///     // ask the firm to complete FS SR PFS signup first
    /// }
    /// </code>
    /// </example>
    public Task<AssignFirmResponse> AssignAsync(string ico, CancellationToken ct = default)
        => _http.RequestAsync<AssignFirmResponse>(HttpMethod.Post, "/firms/assign", new { ico }, ct);

    /// <summary>
    /// Assign multiple firms by ICO in a single batch request (max 50).
    /// Each ICO is processed independently -- partial failures don't block other assignments.
    /// Integrator keys only.
    /// </summary>
    /// <param name="icos">Collection of Slovak ICO numbers to assign (max 50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Individual results for each ICO including firm details or error messages.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Firms.AssignBatchAsync(new[] { "12345678", "87654321", "11111111" });
    /// foreach (var r in result.Results)
    /// {
    ///     if (r.Error is not null)
    ///         Console.WriteLine($"ICO {r.Ico}: FAILED - {r.Error}");
    ///     else
    ///         Console.WriteLine($"ICO {r.Ico}: {r.Firm?.Name} ({r.Status})");
    /// }
    /// </code>
    /// </example>
    public Task<BatchAssignFirmsResponse> AssignBatchAsync(IEnumerable<string> icos, CancellationToken ct = default)
        => _http.RequestAsync<BatchAssignFirmsResponse>(HttpMethod.Post, "/firms/assign/batch", new { icos }, ct);
}
