using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Peppol network lookups: SMP participant discovery, capability checks,
/// directory search, and Slovak company registry lookups.
/// </summary>
public sealed class PeppolResource
{
    private readonly HttpRequestor _http;

    /// <summary>Access the Peppol Business Card directory for searching registered participants.</summary>
    public PeppolDirectoryResource Directory { get; }

    internal PeppolResource(HttpRequestor http)
    {
        _http = http;
        Directory = new PeppolDirectoryResource(http);
    }

    /// <summary>
    /// Look up a Peppol participant by scheme and identifier via the SMP (Service Metadata Publisher).
    /// Returns participant existence, default invoice routing capability, endpoint,
    /// certificate, and supported document types.
    /// </summary>
    /// <param name="scheme">The Peppol identifier scheme (e.g. "0245" for Slovak DIČ).</param>
    /// <param name="identifier">The identifier value within the scheme (e.g. "12345678").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Participant routing and capability details.</returns>
    /// <example>
    /// <code>
    /// var participant = await client.Peppol.LookupAsync("0245", "12345678");
    /// if (participant.Accepts &amp;&amp; participant.RoutingStatus == "ready")
    ///     Console.WriteLine("Receiver is routable");
    /// </code>
    /// </example>
    public Task<PeppolParticipant> LookupAsync(string scheme, string identifier, CancellationToken ct = default)
        => _http.RequestAsync<PeppolParticipant>(HttpMethod.Get,
            $"/peppol/participants/{Uri.EscapeDataString(scheme)}/{Uri.EscapeDataString(identifier)}", ct);

    /// <summary>
    /// Look up a Slovak company by its ICO (business registration number) in the public registry.
    /// Returns the company name, tax identifiers (DIC, IC DPH), address, and Peppol ID if registered.
    /// Useful for auto-filling invoice recipient details.
    /// </summary>
    /// <param name="ico">Slovak business registration number (ICO), e.g. "12345678".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Company details from the Slovak business registry, including Peppol registration if available.</returns>
    /// <example>
    /// <code>
    /// var company = await client.Peppol.CompanyLookupAsync("12345678");
    /// Console.WriteLine($"{company.Name}");
    /// Console.WriteLine($"  DIC: {company.Dic}, IC DPH: {company.IcDph}");
    /// Console.WriteLine($"  Peppol: {company.PeppolId ?? "not registered"}");
    /// </code>
    /// </example>
    public Task<CompanyLookup> CompanyLookupAsync(string ico, CancellationToken ct = default)
        => _http.RequestAsync<CompanyLookup>(HttpMethod.Get, $"/company/lookup/{Uri.EscapeDataString(ico)}", ct);

    /// <summary>Search Slovak companies by name.</summary>
    public Task<Dictionary<string, object?>> CompanySearchAsync(string q, int? limit = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(("q", q), ("limit", limit?.ToString()));
        return _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Get, $"/company/search{qs}", ct);
    }

    /// <summary>Resolve one ERP identifier to an untyped compatibility response.</summary>
    /// <returns>Raw company identity, Peppol participant, and routing capability fields.</returns>
    public Task<Dictionary<string, object?>> ResolveAsync(
        Dictionary<string, string?> parameters,
        CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(parameters.Select(kv => (kv.Key, kv.Value)).ToArray());
        return _http.RequestAsync<Dictionary<string, object?>>(HttpMethod.Get, $"/peppol/participants/resolve{qs}", ct);
    }

    /// <summary>Resolve one ERP identifier to typed company, participant, and routing details.</summary>
    /// <returns>Resolved company identity, Peppol participant, and exact routing capability.</returns>
    public Task<ErpInfo> ResolveErpInfoAsync(
        Dictionary<string, string?> parameters,
        CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(parameters.Select(kv => (kv.Key, kv.Value)).ToArray());
        return _http.RequestAsync<ErpInfo>(HttpMethod.Get, $"/peppol/participants/resolve{qs}", ct);
    }

    /// <summary>
    /// Probe whether a Peppol participant can receive a specific document type.
    /// The response reports the full set of supported document types and business
    /// processes, plus the matched document type ID when a specific document type
    /// is requested.
    /// </summary>
    /// <param name="request">Participant (scheme + identifier) and optional document type to probe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Capabilities response indicating whether the participant was found and what they accept.</returns>
    /// <example>
    /// <code>
    /// var caps = await client.Peppol.CapabilitiesAsync(new CapabilitiesRequest
    /// {
    ///     Scheme = "0192",
    ///     Identifier = "12345678",
    ///     DocumentType = "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"
    /// });
    /// if (caps.Found &amp;&amp; caps.MatchedDocumentType is not null)
    ///     Console.WriteLine("Receiver can accept this document type");
    /// </code>
    /// </example>
    public Task<CapabilitiesResponse> CapabilitiesAsync(CapabilitiesRequest request, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["participant"] = new Dictionary<string, string>
            {
                ["scheme"] = request.Scheme,
                ["identifier"] = request.Identifier,
            },
        };

        if (request.DocumentType is not null)
            body["documentType"] = request.DocumentType;

        return _http.RequestAsync<CapabilitiesResponse>(HttpMethod.Post, "/peppol/capabilities", body, ct);
    }

    /// <summary>
    /// Look up multiple Peppol participants in a single SMP round-trip. Results are
    /// returned in the same order as the request. Much more efficient than multiple
    /// <see cref="LookupAsync(string, string, CancellationToken)"/> calls.
    /// </summary>
    /// <param name="participants">Participants to look up. Max 100 entries per call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch totals and per-participant results with resolved details or error info.</returns>
    /// <example>
    /// <code>
    /// var batch = await client.Peppol.LookupBatchAsync(new List&lt;ParticipantId&gt;
    /// {
    ///     new() { Scheme = "0192", Identifier = "12345678" },
    ///     new() { Scheme = "0192", Identifier = "87654321" }
    /// });
    /// Console.WriteLine($"{batch.Found}/{batch.Total} registered");
    /// </code>
    /// </example>
    public Task<BatchLookupResponse> LookupBatchAsync(List<ParticipantId> participants, CancellationToken ct = default)
        => _http.RequestAsync<BatchLookupResponse>(
            HttpMethod.Post,
            "/peppol/participants/batch",
            new BatchLookupRequest { Participants = participants },
            ct);
}
