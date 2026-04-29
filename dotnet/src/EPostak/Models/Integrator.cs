using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Integrator licenses / billing aggregate
// ---------------------------------------------------------------------------

/// <summary>
/// Integrator-level metadata returned inside <see cref="IntegratorLicenseInfo"/>.
/// </summary>
public sealed class IntegratorLicenseInfoIntegrator
{
    /// <summary>Integrator UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Integrator display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Plan code (e.g. <c>integrator</c>).</summary>
    [JsonPropertyName("plan")]
    public string Plan { get; set; } = "";

    /// <summary>Hard monthly document cap, or <c>null</c> for unlimited.</summary>
    [JsonPropertyName("monthlyDocumentLimit")]
    public int? MonthlyDocumentLimit { get; set; }
}

/// <summary>
/// Aggregate over firms on the <c>integrator-managed</c> plan (the integrator
/// is the payer). Tier rates are applied to <see cref="OutboundCount"/> /
/// <see cref="InboundApiCount"/> as a single AGGREGATE — not per-firm.
/// </summary>
public sealed class IntegratorBillableUsage
{
    /// <summary>Number of firms on the <c>integrator-managed</c> plan.</summary>
    [JsonPropertyName("managedFirms")]
    public int ManagedFirms { get; set; }

    /// <summary>Aggregate outbound document count for the period.</summary>
    [JsonPropertyName("outboundCount")]
    public int OutboundCount { get; set; }

    /// <summary>Aggregate inbound API document count for the period.</summary>
    [JsonPropertyName("inboundApiCount")]
    public int InboundApiCount { get; set; }

    /// <summary>Tier rates applied to the aggregate <see cref="OutboundCount"/>.</summary>
    [JsonPropertyName("outboundCharge")]
    public decimal OutboundCharge { get; set; }

    /// <summary>Tier rates applied to the aggregate <see cref="InboundApiCount"/>.</summary>
    [JsonPropertyName("inboundApiCharge")]
    public decimal InboundApiCharge { get; set; }

    /// <summary>Sum of <see cref="OutboundCharge"/> + <see cref="InboundApiCharge"/>, rounded to cents.</summary>
    [JsonPropertyName("totalCharge")]
    public decimal TotalCharge { get; set; }

    /// <summary>Always <c>EUR</c>.</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "EUR";
}

/// <summary>
/// Linked firms that pay their own plan (not billed to the integrator).
/// </summary>
public sealed class IntegratorNonManagedUsage
{
    /// <summary>Number of non-managed firms.</summary>
    [JsonPropertyName("firms")]
    public int Firms { get; set; }

    /// <summary>Their aggregate outbound count for the period.</summary>
    [JsonPropertyName("outboundCount")]
    public int OutboundCount { get; set; }

    /// <summary>Their aggregate inbound API count for the period.</summary>
    [JsonPropertyName("inboundApiCount")]
    public int InboundApiCount { get; set; }
}

/// <summary>
/// One row in the pricing tier table. The last entry is open-ended:
/// <see cref="UpTo"/> and <see cref="Rate"/> are both <c>null</c> and
/// <see cref="ContactRequired"/> is <c>true</c>.
/// </summary>
public sealed class IntegratorPricingTier
{
    /// <summary>Inclusive upper bound of the tier, or <c>null</c> on the open tier.</summary>
    [JsonPropertyName("upTo")]
    public int? UpTo { get; set; }

    /// <summary>Per-document rate in EUR, or <c>null</c> on the open tier.</summary>
    [JsonPropertyName("rate")]
    public decimal? Rate { get; set; }

    /// <summary>Display label on the open tier (e.g. <c>Individuálne</c>); <c>null</c> on the auto-billed tiers.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary><c>true</c> when this tier requires a sales contract.</summary>
    [JsonPropertyName("contactRequired")]
    public bool? ContactRequired { get; set; }
}

/// <summary>
/// Pricing table — separate tiers for outbound and inbound API.
/// </summary>
public sealed class IntegratorPricing
{
    /// <summary>Always <c>tiered</c>.</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "tiered";

    /// <summary>Always <c>EUR</c>.</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "EUR";

    /// <summary>Outbound tier table.</summary>
    [JsonPropertyName("outboundTiers")]
    public List<IntegratorPricingTier> OutboundTiers { get; set; } = new();

    /// <summary>Inbound API tier table.</summary>
    [JsonPropertyName("inboundApiTiers")]
    public List<IntegratorPricingTier> InboundApiTiers { get; set; } = new();
}

/// <summary>
/// Per-firm row in the <see cref="IntegratorLicenseInfo.Firms"/> page.
/// </summary>
public sealed class IntegratorFirmUsage
{
    /// <summary>Firm UUID.</summary>
    [JsonPropertyName("firmId")]
    public string FirmId { get; set; } = "";

    /// <summary>Firm display name, or <c>null</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Slovak ICO, or <c>null</c>.</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>
    /// <c>true</c> → counts in <see cref="IntegratorLicenseInfo.Billable"/>;
    /// <c>false</c> → counts in <see cref="IntegratorLicenseInfo.NonManaged"/>.
    /// </summary>
    [JsonPropertyName("managed")]
    public bool Managed { get; set; }

    /// <summary>Firm's outbound count for the period.</summary>
    [JsonPropertyName("outboundCount")]
    public int OutboundCount { get; set; }

    /// <summary>Firm's inbound API count for the period.</summary>
    [JsonPropertyName("inboundApiCount")]
    public int InboundApiCount { get; set; }
}

/// <summary>
/// Pagination envelope for the <see cref="IntegratorLicenseInfo.Firms"/> list.
/// </summary>
public sealed class IntegratorLicensePagination
{
    /// <summary>Page size used for this response.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>Offset used for this response.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>Total firm count linked to the integrator.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// Response of <c>GET /api/v1/integrator/licenses/info</c>.
/// <para>
/// Tier rates are applied to the AGGREGATE document count across all the
/// integrator's <c>integrator-managed</c> firms. A 100-firm × 50-doc
/// integrator lands in tier 2–3, not tier 1 like a standalone firm would.
/// Volumes above <see cref="ContactThreshold"/> (5 000 / month) flip
/// <see cref="ExceedsAutoTier"/> to <c>true</c>; auto-billing pauses there
/// and sales handles invoicing manually.
/// </para>
/// </summary>
public sealed class IntegratorLicenseInfo
{
    /// <summary>Integrator metadata (id, name, plan, optional cap).</summary>
    [JsonPropertyName("integrator")]
    public IntegratorLicenseInfoIntegrator Integrator { get; set; } = new();

    /// <summary>Current billing period in <c>YYYY-MM</c> (SK timezone).</summary>
    [JsonPropertyName("period")]
    public string Period { get; set; } = "";

    /// <summary>ISO 8601 — when counters reset (1st of next month, SK midnight in UTC).</summary>
    [JsonPropertyName("nextResetAt")]
    public string NextResetAt { get; set; } = "";

    /// <summary>Aggregate over firms on the <c>integrator-managed</c> plan (the integrator pays).</summary>
    [JsonPropertyName("billable")]
    public IntegratorBillableUsage Billable { get; set; } = new();

    /// <summary>Linked firms paying their own plan (not billed to the integrator).</summary>
    [JsonPropertyName("nonManaged")]
    public IntegratorNonManagedUsage NonManaged { get; set; } = new();

    /// <summary>
    /// <c>true</c> when outbound or inbound exceeds <see cref="ContactThreshold"/>.
    /// Auto-billing pauses; sales handles invoicing manually.
    /// </summary>
    [JsonPropertyName("exceedsAutoTier")]
    public bool ExceedsAutoTier { get; set; }

    /// <summary>Threshold (5 000) above which auto-billing stops and an individual contract is required.</summary>
    [JsonPropertyName("contactThreshold")]
    public int ContactThreshold { get; set; }

    /// <summary>Pricing table — separate tiers for outbound and inbound API.</summary>
    [JsonPropertyName("pricing")]
    public IntegratorPricing Pricing { get; set; } = new();

    /// <summary>Per-firm breakdown for the requested page (sorted by outbound count, descending).</summary>
    [JsonPropertyName("firms")]
    public List<IntegratorFirmUsage> Firms { get; set; } = new();

    /// <summary>Pagination envelope for the <see cref="Firms"/> list.</summary>
    [JsonPropertyName("pagination")]
    public IntegratorLicensePagination Pagination { get; set; } = new();
}
