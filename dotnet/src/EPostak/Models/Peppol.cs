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
    /// <summary>True when the participant was found in the SMP.</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>True when the participant can receive the default/probed Peppol document type.</summary>
    [JsonPropertyName("accepts")]
    public bool Accepts { get; set; }

    /// <summary>Routing status such as <c>ready</c>, <c>document_type_not_supported</c>, or <c>lookup_failed</c>.</summary>
    [JsonPropertyName("routingStatus")]
    public string? RoutingStatus { get; set; }

    /// <summary>Peppol participant ID in <c>scheme:identifier</c> format.</summary>
    [JsonPropertyName("participantId")]
    public string? ParticipantId { get; set; }

    /// <summary>Peppol identifier scheme, e.g. <c>0245</c>.</summary>
    [JsonPropertyName("scheme")]
    public string? Scheme { get; set; }

    /// <summary>Identifier value within the scheme.</summary>
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>SMP access point metadata, when available.</summary>
    [JsonPropertyName("accessPoint")]
    public PeppolAccessPoint? AccessPoint { get; set; }

    /// <summary>AS4 certificate metadata, when available.</summary>
    [JsonPropertyName("certificate")]
    public PeppolCertificateInfo? Certificate { get; set; }

    /// <summary>UBL document type identifiers the participant can receive.</summary>
    [JsonPropertyName("supportedDocumentTypes")]
    public List<string>? SupportedDocumentTypes { get; set; }

    /// <summary>Origin of the lookup data, such as <c>sml</c> or <c>internal</c>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>True when a transient SMP/SML lookup failure prevented a conclusive result.</summary>
    [JsonPropertyName("temporaryFailure")]
    public bool? TemporaryFailure { get; set; }

    /// <summary>True when the participant is hosted on our own AP.</summary>
    [JsonPropertyName("internal")]
    public bool? Internal { get; set; }

    /// <summary>Peppol participant identifier in "scheme:id" format (e.g. "0245:12345678").</summary>
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

/// <summary>SMP access point metadata.</summary>
public sealed class PeppolAccessPoint
{
    /// <summary>AS4 endpoint URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Peppol transport profile identifier.</summary>
    [JsonPropertyName("transportProfile")]
    public string? TransportProfile { get; set; }
}

/// <summary>AS4 certificate metadata from the SMP endpoint.</summary>
public sealed class PeppolCertificateInfo
{
    /// <summary>Whether certificate metadata was present in the SMP response.</summary>
    [JsonPropertyName("present")]
    public bool? Present { get; set; }

    /// <summary>SHA-256 certificate fingerprint, when available.</summary>
    [JsonPropertyName("fingerprintSha256")]
    public string? FingerprintSha256 { get; set; }

    /// <summary>Certificate subject, when available.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>Certificate issuer, when available.</summary>
    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    /// <summary>Certificate serial number, when available.</summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>Certificate not-before timestamp, when available.</summary>
    [JsonPropertyName("notBefore")]
    public string? NotBefore { get; set; }

    /// <summary>Certificate not-after timestamp, when available.</summary>
    [JsonPropertyName("notAfter")]
    public string? NotAfter { get; set; }

    /// <summary>SMP service activation timestamp, when available.</summary>
    [JsonPropertyName("serviceActivationDate")]
    public string? ServiceActivationDate { get; set; }

    /// <summary>SMP service expiration timestamp, when available.</summary>
    [JsonPropertyName("serviceExpirationDate")]
    public string? ServiceExpirationDate { get; set; }

    /// <summary>Whether the endpoint certificate is currently valid.</summary>
    [JsonPropertyName("valid")]
    public bool? Valid { get; set; }

    /// <summary>Certificate expiration timestamp, when available.</summary>
    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }

    /// <summary>Certificate parsing/validation error code, when present.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
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

    /// <summary>True when the participant accepts the probed document type.</summary>
    [JsonPropertyName("accepts")]
    public bool Accepts { get; set; }

    /// <summary>Participant identifier echoed by the API.</summary>
    [JsonPropertyName("participant")]
    public ParticipantId? Participant { get; set; }

    /// <summary>SMP access point metadata, when available.</summary>
    [JsonPropertyName("accessPoint")]
    public PeppolAccessPoint? AccessPoint { get; set; }

    /// <summary>True when the participant is hosted on our own AP.</summary>
    [JsonPropertyName("internal")]
    public bool? Internal { get; set; }

    /// <summary>UBL document type identifiers the participant can receive. Null when <see cref="Found"/> is false.</summary>
    [JsonPropertyName("supportedDocumentTypes")]
    public List<string>? SupportedDocumentTypes { get; set; }

    /// <summary>
    /// The document type ID that matched the requested <see cref="CapabilitiesRequest.DocumentType"/>.
    /// Null if no specific type was requested or no match was found.
    /// </summary>
    [JsonPropertyName("matchedDocumentType")]
    public string? MatchedDocumentType { get; set; }

    /// <summary>Origin of the lookup data, such as <c>sml</c> or <c>internal</c>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Human-readable rejection reason when the participant cannot receive the request.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>AS4 certificate metadata, when available.</summary>
    [JsonPropertyName("certificate")]
    public PeppolCertificateInfo? Certificate { get; set; }

    /// <summary>Matched capability details, when the backend returns them.</summary>
    [JsonPropertyName("capability")]
    public Dictionary<string, object?>? Capability { get; set; }
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

    /// <summary>Combined form <c>scheme:identifier</c>, returned by lookup responses.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
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
    /// <summary>Zero-based position of this participant in the request.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>The participant identifier echoed by the API.</summary>
    [JsonPropertyName("participant")]
    public ParticipantId? Participant { get; set; }

    /// <summary>True if the participant was found in the SMP.</summary>
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    /// <summary>True when the participant can receive the default/probed Peppol document type.</summary>
    [JsonPropertyName("accepts")]
    public bool Accepts { get; set; }

    /// <summary>Routing status such as <c>ready</c>, <c>participant_not_found</c>, or <c>lookup_failed</c>.</summary>
    [JsonPropertyName("routingStatus")]
    public string? RoutingStatus { get; set; }

    /// <summary>SMP access point metadata, when available.</summary>
    [JsonPropertyName("accessPoint")]
    public PeppolAccessPoint? AccessPoint { get; set; }

    /// <summary>AS4 certificate metadata, when available.</summary>
    [JsonPropertyName("certificate")]
    public PeppolCertificateInfo? Certificate { get; set; }

    /// <summary>True when the participant is hosted on our own AP.</summary>
    [JsonPropertyName("internal")]
    public bool? Internal { get; set; }

    /// <summary>UBL document type identifiers the participant advertises.</summary>
    [JsonPropertyName("supportedDocumentTypes")]
    public List<string>? SupportedDocumentTypes { get; set; }

    /// <summary>Origin of the lookup data, such as <c>sml</c> or <c>internal</c>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>True when a transient SMP/SML lookup failure prevented a conclusive result.</summary>
    [JsonPropertyName("temporaryFailure")]
    public bool? TemporaryFailure { get; set; }

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

// ---------------------------------------------------------------------------
// ERP participant resolution
// ---------------------------------------------------------------------------

/// <summary>
/// Company identity and Peppol routing details returned by
/// <c>GET /peppol/participants/resolve</c>.
/// </summary>
public sealed class ErpInfo
{
    /// <summary>The identifier form that was used for the lookup.</summary>
    [JsonPropertyName("query")]
    public ErpQuery? Query { get; set; }

    /// <summary>Recommended next action, such as <c>sendable</c> or <c>retry_later</c>.</summary>
    [JsonPropertyName("nextAction")]
    public string NextAction { get; set; } = "";

    /// <summary>Resolved company details. Null for a direct Peppol-ID lookup.</summary>
    [JsonPropertyName("company")]
    public ErpCompany? Company { get; set; }

    /// <summary>Resolved Peppol participant and endpoint details.</summary>
    [JsonPropertyName("participant")]
    public ErpParticipant? Participant { get; set; }

    /// <summary>Routing capability for the requested document type and process.</summary>
    [JsonPropertyName("capability")]
    public ErpCapability? Capability { get; set; }
}

/// <summary>Identifier used by the ERP participant resolver.</summary>
public sealed class ErpQuery
{
    /// <summary>Lookup type, such as <c>ico</c>, <c>dic</c>, or <c>peppolId</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>Normalized lookup value.</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

/// <summary>Company data enriched from the Slovak registries and Peppol directory.</summary>
public sealed class ErpCompany
{
    /// <summary>Slovak business registration number.</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Slovak tax identification number.</summary>
    [JsonPropertyName("dic")]
    public string? Dic { get; set; }

    /// <summary>VAT identification number.</summary>
    [JsonPropertyName("icDph")]
    public string? IcDph { get; set; }

    /// <summary>Legal company name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Registered company address.</summary>
    [JsonPropertyName("address")]
    public PartyAddress? Address { get; set; }

    /// <summary>Whether the company is active in the source registry.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>Whether the company has a matching Peppol directory entry.</summary>
    [JsonPropertyName("inPeppol")]
    public bool InPeppol { get; set; }

    /// <summary>Company-data source, such as <c>fs_tax_subjects</c> or <c>merged</c>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

/// <summary>Resolved Peppol participant and routing endpoint.</summary>
public sealed class ErpParticipant
{
    /// <summary>Normalized Peppol participant ID.</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }

    /// <summary>Peppol identifier scheme.</summary>
    [JsonPropertyName("scheme")]
    public string? Scheme { get; set; }

    /// <summary>Identifier value within the scheme.</summary>
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>Whether the participant is registered on Peppol.</summary>
    [JsonPropertyName("registered")]
    public bool Registered { get; set; }

    /// <summary>Lookup source, such as <c>sml</c> or <c>internal</c>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Resolved AS4 endpoint metadata.</summary>
    [JsonPropertyName("accessPoint")]
    public PeppolAccessPoint? AccessPoint { get; set; }

    /// <summary>Resolved endpoint certificate metadata.</summary>
    [JsonPropertyName("certificate")]
    public PeppolCertificateInfo? Certificate { get; set; }

    /// <summary>Document types advertised by the participant.</summary>
    [JsonPropertyName("supportedDocumentTypes")]
    public List<string>? SupportedDocumentTypes { get; set; }
}

/// <summary>Exact document/process routing capability resolved for the participant.</summary>
public sealed class ErpCapability
{
    /// <summary>Peppol document type identifier that was checked.</summary>
    [JsonPropertyName("documentTypeId")]
    public string DocumentTypeId { get; set; } = "";

    /// <summary>Peppol process identifier that was checked.</summary>
    [JsonPropertyName("processId")]
    public string ProcessId { get; set; } = "";

    /// <summary>Whether the participant accepts the document/process pair.</summary>
    [JsonPropertyName("accepts")]
    public bool Accepts { get; set; }

    /// <summary>Routing status, such as <c>ready</c> or <c>document_type_not_supported</c>.</summary>
    [JsonPropertyName("routingStatus")]
    public string? RoutingStatus { get; set; }

    /// <summary>Whether the participant is registered and has a usable route.</summary>
    [JsonPropertyName("networkReady")]
    public bool NetworkReady { get; set; }
}
