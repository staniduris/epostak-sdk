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
