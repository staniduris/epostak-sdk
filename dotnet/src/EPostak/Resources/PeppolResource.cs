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
    /// Returns the participant's name, country, and supported document types/transport profiles.
    /// Use this to verify a receiver exists and check what they can accept before sending.
    /// </summary>
    /// <param name="scheme">The Peppol identifier scheme (e.g. "0192" for Slovak ICO, "0088" for EAN/GLN).</param>
    /// <param name="identifier">The identifier value within the scheme (e.g. "12345678").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Participant details including Peppol ID, name, country, and list of supported capabilities.</returns>
    /// <example>
    /// <code>
    /// var participant = await client.Peppol.LookupAsync("0192", "12345678");
    /// Console.WriteLine($"{participant.Name} ({participant.Country})");
    /// foreach (var cap in participant.Capabilities)
    ///     Console.WriteLine($"  Supports: {cap.DocumentTypeId} via {cap.TransportProfile}");
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
        => _http.RequestAsync<CapabilitiesResponse>(HttpMethod.Post, "/peppol/capabilities", request, ct);

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
