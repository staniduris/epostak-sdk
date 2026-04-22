using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Peppol SMP lookup
// ---------------------------------------------------------------------------

/// <summary>
/// A single capability (document type + process + transport) advertised by a
/// Peppol participant in their SMP entry.
/// </summary>
public sealed class SmpParticipantCapability
{
    /// <summary>Peppol document type identifier (e.g. the UBL Invoice 2.1 document type ID).</summary>
    [JsonPropertyName("documentTypeId")]
    public string DocumentTypeId { get; set; } = "";

    /// <summary>Peppol process identifier (e.g. "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0").</summary>
    [JsonPropertyName("processId")]
    public string ProcessId { get; set; } = "";

    /// <summary>AS4 transport profile identifier (e.g. "peppol-transport-as4-v2_0").</summary>
    [JsonPropertyName("transportProfile")]
    public string TransportProfile { get; set; } = "";
}

/// <summary>
/// A Peppol participant as returned by an SMP lookup, including identity
/// and all supported document types/transport capabilities.
/// </summary>
public sealed class PeppolParticipant
{
    /// <summary>Peppol participant identifier in "scheme:id" format (e.g. "0192:12345678").</summary>
    [JsonPropertyName("peppolId")]
    public string PeppolId { get; set; } = "";

    /// <summary>Business name of the participant. Null if not available in the SMP record.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. "SK"). Null if not available.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>List of document types and transport profiles the participant can receive.</summary>
    [JsonPropertyName("capabilities")]
    public List<SmpParticipantCapability> Capabilities { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Peppol Directory search
// ---------------------------------------------------------------------------

/// <summary>
/// Parameters for searching the Peppol Business Card directory.
/// </summary>
public sealed class DirectorySearchParams
{
    /// <summary>Free-text search query (searches participant names and identifiers).</summary>
    public string? Q { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code to filter results (e.g. "SK", "CZ", "DE").</summary>
    public string? Country { get; set; }

    /// <summary>Page number for pagination (1-based, default 1).</summary>
    public int? Page { get; set; }

    /// <summary>Number of results per page (default 20, max 100).</summary>
    public int? PageSize { get; set; }
}

/// <summary>
/// A single entry in the Peppol Business Card directory.
/// </summary>
public sealed class DirectoryEntry
{
    /// <summary>Peppol participant identifier in "scheme:id" format.</summary>
    [JsonPropertyName("peppolId")]
    public string PeppolId { get; set; } = "";

    /// <summary>Registered business name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    /// <summary>Date when the participant was registered in the directory (ISO 8601). Null if not available.</summary>
    [JsonPropertyName("registeredAt")]
    public string? RegisteredAt { get; set; }
}

/// <summary>
/// Paginated result from a Peppol directory search.
/// </summary>
public sealed class DirectorySearchResult
{
    /// <summary>Directory entries matching the search criteria on the current page.</summary>
    [JsonPropertyName("results")]
    public List<DirectoryEntry> Results { get; set; } = [];

    /// <summary>Total number of matching entries across all pages.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Current page number (1-based).</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>Number of results per page.</summary>
    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }
}

// ---------------------------------------------------------------------------
// Company lookup
// ---------------------------------------------------------------------------

/// <summary>
/// Slovak company information retrieved from public registries (ORSR, FinStat, slovensko.digital).
/// Includes legal identifiers, address, and Peppol registration status.
/// </summary>
public sealed class CompanyLookup
{
    /// <summary>Slovak business registration number (ICO) -- the 8-digit identifier that was looked up.</summary>
    [JsonPropertyName("ico")]
    public string Ico { get; set; } = "";

    /// <summary>Legal business name from the registry.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Tax identification number (DIC) for income tax. Null if not available.</summary>
    [JsonPropertyName("dic")]
    public string? Dic { get; set; }

    /// <summary>VAT identification number (IC DPH) in "SK" + 10-digit format. Null if not a VAT payer.</summary>
    [JsonPropertyName("icDph")]
    public string? IcDph { get; set; }

    /// <summary>Registered address from the Slovak business registry.</summary>
    [JsonPropertyName("address")]
    public PartyAddress? Address { get; set; }

    /// <summary>Peppol participant identifier if the company is registered on the Peppol network. Null if not registered.</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }
}

// ---------------------------------------------------------------------------
// Capabilities probe
// ---------------------------------------------------------------------------

/// <summary>
/// Request to probe whether a Peppol participant can receive a specific document type
/// via <c>POST /peppol/capabilities</c>.
/// </summary>
public sealed class CapabilitiesRequest
{
    /// <summary>Peppol identifier scheme (e.g. <c>0245</c> for Slovak DIC). Required.</summary>
    [JsonPropertyName("scheme")]
    public required string Scheme { get; set; }

    /// <summary>Identifier value within the scheme (e.g. <c>12345678</c>). Required.</summary>
    [JsonPropertyName("identifier")]
    public required string Identifier { get; set; }

    /// <summary>
    /// UBL document type identifier to probe for. When null, the response returns the
    /// full set of supported document types without a specific match.
    /// </summary>
    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }
}

/// <summary>
/// Response from <c>POST /peppol/capabilities</c>.
/// </summary>
public sealed class CapabilitiesResponse
{
    /// <summary>True if the participant was found in the SMP.</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>Business process IDs the participant advertises. Null when <see cref="Found"/> is false.</summary>
    [JsonPropertyName("accepts")]
    public List<string>? Accepts { get; set; }

    /// <summary>UBL document type identifiers the participant can receive. Null when <see cref="Found"/> is false.</summary>
    [JsonPropertyName("supportedDocumentTypes")]
    public List<string>? SupportedDocumentTypes { get; set; }

    /// <summary>
    /// The document type ID that matched the requested <see cref="CapabilitiesRequest.DocumentType"/>.
    /// Null if no specific type was requested or no match was found.
    /// </summary>
    [JsonPropertyName("matchedDocumentType")]
    public string? MatchedDocumentType { get; set; }
}

// ---------------------------------------------------------------------------
// Batch participant lookup
// ---------------------------------------------------------------------------

/// <summary>
/// A single participant identifier in a batch SMP lookup.
/// </summary>
public sealed class ParticipantId
{
    /// <summary>Peppol identifier scheme (e.g. <c>0245</c>). Required.</summary>
    [JsonPropertyName("scheme")]
    public required string Scheme { get; set; }

    /// <summary>Identifier value within the scheme (e.g. <c>12345678</c>). Required.</summary>
    [JsonPropertyName("identifier")]
    public required string Identifier { get; set; }
}

/// <summary>
/// Request body for <c>POST /peppol/participants/batch</c>, performing SMP lookups
/// for multiple participants in a single call.
/// </summary>
public sealed class BatchLookupRequest
{
    /// <summary>Participants to look up. Max 100 entries per call.</summary>
    [JsonPropertyName("participants")]
    public required List<ParticipantId> Participants { get; set; }
}

/// <summary>
/// Result of a single participant lookup in a batch.
/// </summary>
public sealed class BatchLookupResult
{
    /// <summary>The scheme that was queried.</summary>
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = "";

    /// <summary>The identifier that was queried.</summary>
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = "";

    /// <summary>True if the participant was found in the SMP.</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>The resolved participant details. Null when <see cref="Found"/> is false.</summary>
    [JsonPropertyName("participant")]
    public PeppolParticipant? Participant { get; set; }

    /// <summary>Error message when lookup failed for reasons other than "not found". Null on success or plain not-found.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Response from a batch SMP participant lookup.
/// </summary>
public sealed class BatchLookupResponse
{
    /// <summary>Total number of participants queried.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Number of participants that were found in the SMP.</summary>
    [JsonPropertyName("found")]
    public int Found { get; set; }

    /// <summary>Number of participants that were not found in the SMP.</summary>
    [JsonPropertyName("notFound")]
    public int NotFound { get; set; }

    /// <summary>Per-participant results in request order.</summary>
    [JsonPropertyName("results")]
    public List<BatchLookupResult> Results { get; set; } = [];
}
