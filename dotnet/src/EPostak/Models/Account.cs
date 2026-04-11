using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Account
// ---------------------------------------------------------------------------

/// <summary>
/// The firm linked to the API key, with essential identifiers and Peppol status.
/// </summary>
public sealed class AccountFirm
{
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
}

/// <summary>
/// Subscription plan information for the account.
/// </summary>
public sealed class AccountPlan
{
    /// <summary>Plan name (e.g. "Starter", "Business", "Enterprise").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Plan status (e.g. "active", "trialing", "past_due", "cancelled").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

/// <summary>
/// Document usage counters for the current billing period.
/// </summary>
public sealed class AccountUsage
{
    /// <summary>Number of outbound (sent) documents in the current billing period.</summary>
    [JsonPropertyName("outbound")]
    public int Outbound { get; set; }

    /// <summary>Number of inbound (received) documents in the current billing period.</summary>
    [JsonPropertyName("inbound")]
    public int Inbound { get; set; }
}

/// <summary>
/// Account information combining firm details, subscription plan, and usage data.
/// </summary>
public sealed class Account
{
    /// <summary>The firm linked to the API key.</summary>
    [JsonPropertyName("firm")]
    public AccountFirm Firm { get; set; } = new();

    /// <summary>Active subscription plan and its status.</summary>
    [JsonPropertyName("plan")]
    public AccountPlan Plan { get; set; } = new();

    /// <summary>Document usage counters for the current billing period.</summary>
    [JsonPropertyName("usage")]
    public AccountUsage Usage { get; set; } = new();
}
