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

// ---------------------------------------------------------------------------
// Auth status
// ---------------------------------------------------------------------------

/// <summary>
/// Information about the authenticated API key, returned by <c>POST /auth/status</c>.
/// </summary>
public sealed class AuthStatusKey
{
    /// <summary>Key UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Human-visible prefix of the key (e.g. <c>sk_live_abc</c>).</summary>
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = "";

    /// <summary>Key type: <c>direct</c> or <c>integrator</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>ISO 8601 timestamp when the key was created.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>ISO 8601 timestamp of the last successful authentication with this key. Null if never used.</summary>
    [JsonPropertyName("lastUsedAt")]
    public string? LastUsedAt { get; set; }
}

/// <summary>
/// The firm the API key resolves to, as returned by <c>POST /auth/status</c>.
/// </summary>
public sealed class AuthStatusFirm
{
    /// <summary>Firm UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Legal business name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Slovak business registration number (ICO). Null if not a Slovak entity.</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Primary Peppol participant identifier. Null if not yet registered.</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }

    /// <summary>Peppol registration status (e.g. <c>active</c>, <c>pending</c>).</summary>
    [JsonPropertyName("peppolStatus")]
    public string PeppolStatus { get; set; } = "";
}

/// <summary>
/// Rate limit window and remaining budget for the current API key.
/// </summary>
public sealed class AuthStatusRateLimit
{
    /// <summary>Request ceiling per window.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>Requests remaining in the current window.</summary>
    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }

    /// <summary>ISO 8601 timestamp when the window resets.</summary>
    [JsonPropertyName("resetAt")]
    public string ResetAt { get; set; } = "";
}

/// <summary>
/// Integrator metadata, present only when the key is an integrator key (<c>sk_int_*</c>).
/// </summary>
public sealed class AuthStatusIntegrator
{
    /// <summary>Number of client firms managed under this integrator key.</summary>
    [JsonPropertyName("firmsManaged")]
    public int FirmsManaged { get; set; }
}

/// <summary>
/// Response from <c>POST /auth/status</c>. Describes the authenticated key, the
/// firm it resolves to, the current subscription plan, applicable rate limit,
/// and optional integrator info.
/// </summary>
public sealed class AuthStatusResponse
{
    /// <summary>Information about the authenticated API key.</summary>
    [JsonPropertyName("key")]
    public AuthStatusKey Key { get; set; } = new();

    /// <summary>The firm the key resolves to.</summary>
    [JsonPropertyName("firm")]
    public AuthStatusFirm Firm { get; set; } = new();

    /// <summary>Current subscription plan identifier (e.g. <c>starter</c>, <c>business</c>).</summary>
    [JsonPropertyName("plan")]
    public string Plan { get; set; } = "";

    /// <summary>Rate limit window and remaining budget for this key.</summary>
    [JsonPropertyName("rateLimit")]
    public AuthStatusRateLimit RateLimit { get; set; } = new();

    /// <summary>Integrator metadata when the key is <c>sk_int_*</c>. Null for direct keys.</summary>
    [JsonPropertyName("integrator")]
    public AuthStatusIntegrator? Integrator { get; set; }
}

// ---------------------------------------------------------------------------
// Rotate secret
// ---------------------------------------------------------------------------

/// <summary>
/// Response from <c>POST /auth/rotate-secret</c>. The new <see cref="Key"/> value
/// is returned ONCE — store it immediately. The previous secret is invalidated
/// server-side.
/// </summary>
/// <remarks>
/// Not available for integrator subkeys (<c>sk_int_*</c>); the server responds
/// with HTTP 409 in that case.
/// </remarks>
public sealed class RotateSecretResponse
{
    /// <summary>Key UUID whose secret was rotated.</summary>
    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = "";

    /// <summary>The new API key value. Only returned once.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>Human-visible prefix of the new key (e.g. <c>sk_live_abc</c>).</summary>
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = "";

    /// <summary>ISO 8601 timestamp when the rotation completed.</summary>
    [JsonPropertyName("rotatedAt")]
    public string RotatedAt { get; set; } = "";
}
