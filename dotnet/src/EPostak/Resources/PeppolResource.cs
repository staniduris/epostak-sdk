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
}
