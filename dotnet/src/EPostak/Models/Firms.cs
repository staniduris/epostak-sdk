using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Firms
// ---------------------------------------------------------------------------

/// <summary>
/// Summary of a firm as returned by the firms list endpoint.
/// Contains the essential identifiers and Peppol registration status.
/// </summary>
public sealed class FirmSummary
{
    /// <summary>Unique firm UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Legal business name of the firm.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Slovak business registration number (ICO). Null if not a Slovak entity.</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Primary Peppol participant identifier (e.g. "0192:12345678"). Null if not yet registered.</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }

    /// <summary>Peppol registration status (e.g. "active", "pending", "inactive").</summary>
    [JsonPropertyName("peppolStatus")]
    public string PeppolStatus { get; set; } = "";
}

/// <summary>
/// A Peppol identifier registered for a firm, consisting of a scheme and identifier value.
/// </summary>
public sealed class FirmPeppolIdentifier
{
    /// <summary>Peppol identifier scheme (e.g. "0192" for Slovak ICO, "0088" for EAN/GLN).</summary>
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = "";

    /// <summary>Identifier value within the scheme (e.g. the ICO number "12345678").</summary>
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = "";
}

/// <summary>
/// Full details of a firm including address, tax identifiers, and all registered Peppol identifiers.
/// </summary>
public sealed class FirmDetail
{
    /// <summary>Unique firm UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Legal business name of the firm.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Slovak business registration number (ICO). Null if not a Slovak entity.</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Primary Peppol participant identifier. Null if not yet registered.</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }

    /// <summary>Peppol registration status (e.g. "active", "pending", "inactive").</summary>
    [JsonPropertyName("peppolStatus")]
    public string PeppolStatus { get; set; } = "";

    /// <summary>Tax identification number (DIC) for income tax purposes.</summary>
    [JsonPropertyName("dic")]
    public string? Dic { get; set; }

    /// <summary>VAT identification number (IC DPH) in the format "SK" + 10 digits.</summary>
    [JsonPropertyName("icDph")]
    public string? IcDph { get; set; }

    /// <summary>Registered postal address of the firm.</summary>
    [JsonPropertyName("address")]
    public PartyAddress? Address { get; set; }

    /// <summary>All Peppol identifiers registered for this firm (may include multiple schemes).</summary>
    [JsonPropertyName("peppolIdentifiers")]
    public List<FirmPeppolIdentifier> PeppolIdentifiers { get; set; } = [];

    /// <summary>Timestamp when the firm was created in ePosťak (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// Internal wrapper for the firms list API response.
/// </summary>
public sealed class FirmsListResponse
{
    /// <summary>List of firms accessible by the current API key.</summary>
    [JsonPropertyName("firms")]
    public List<FirmSummary> Firms { get; set; } = [];
}

/// <summary>
/// Parameters for listing documents belonging to a specific firm.
/// </summary>
public sealed class FirmDocumentsParams
{
    /// <summary>Number of documents to skip for pagination (default 0).</summary>
    public int? Offset { get; set; }

    /// <summary>Maximum number of documents to return (default 20, max 100).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter by document direction: inbound (received) or outbound (sent). Null returns both.</summary>
    public DocumentDirection? Direction { get; set; }
}

/// <summary>
/// Response from registering a Peppol identifier for a firm.
/// </summary>
public sealed class PeppolIdentifierResponse
{
    /// <summary>The full Peppol participant ID in "scheme:identifier" format (e.g. "0192:12345678").</summary>
    [JsonPropertyName("peppolId")]
    public string PeppolId { get; set; } = "";

    /// <summary>The identifier scheme (e.g. "0192").</summary>
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; } = "";

    /// <summary>The identifier value (e.g. "12345678").</summary>
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = "";

    /// <summary>Timestamp when the identifier was registered on the SMP (ISO 8601).</summary>
    [JsonPropertyName("registeredAt")]
    public string RegisteredAt { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Firm assignment (integrator)
// ---------------------------------------------------------------------------

/// <summary>
/// Response from assigning a firm to an integrator account.
/// </summary>
public sealed class AssignFirmResponse
{
    /// <summary>The assigned firm's details.</summary>
    [JsonPropertyName("firm")]
    public AssignedFirm Firm { get; set; } = new();

    /// <summary>Assignment status (e.g. "created", "existing").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

/// <summary>
/// Firm details as returned by the assignment endpoint.
/// </summary>
public sealed class AssignedFirm
{
    /// <summary>Unique firm UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Legal business name. Null if not yet resolved from the registry.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Slovak business registration number (ICO).</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Primary Peppol participant identifier. Null if not yet registered.</summary>
    [JsonPropertyName("peppol_id")]
    public string? PeppolId { get; set; }

    /// <summary>Peppol registration status (e.g. "active", "pending", "inactive").</summary>
    [JsonPropertyName("peppol_status")]
    public string PeppolStatus { get; set; } = "";
}

/// <summary>
/// Result for a single ICO in a batch firm assignment. Contains either a firm
/// (on success) or an error message (on failure).
/// </summary>
public sealed class BatchAssignFirmResult
{
    /// <summary>The ICO that was submitted for assignment.</summary>
    [JsonPropertyName("ico")]
    public string Ico { get; set; } = "";

    /// <summary>The assigned firm details. Null if the assignment failed.</summary>
    [JsonPropertyName("firm")]
    public AssignedFirm? Firm { get; set; }

    /// <summary>Assignment status for this ICO (e.g. "created", "existing"). Null on error.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Error code if the assignment failed. Null on success.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>Human-readable error message. Null on success.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response from batch firm assignment containing individual results per ICO.
/// </summary>
public sealed class BatchAssignFirmsResponse
{
    /// <summary>Individual assignment results for each submitted ICO.</summary>
    [JsonPropertyName("results")]
    public List<BatchAssignFirmResult> Results { get; set; } = [];
}
