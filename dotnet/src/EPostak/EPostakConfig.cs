namespace EPostak;

/// <summary>
/// Configuration for the ePosťak Enterprise API client. At minimum, provide an <see cref="ApiKey"/>.
/// For integrator (multi-tenant) usage, also set <see cref="FirmId"/> or use
/// <see cref="EPostakClient.WithFirm"/> to scope requests to a specific client firm.
/// </summary>
/// <example>
/// Direct API key (single firm):
/// <code>
/// var config = new EPostakConfig { ApiKey = "sk_live_xxxxx" };
/// </code>
/// Integrator key (multi-tenant) scoped to a specific firm:
/// <code>
/// var config = new EPostakConfig { ApiKey = "sk_int_xxxxx", FirmId = "firm-uuid-here" };
/// </code>
/// </example>
public sealed class EPostakConfig
{
    /// <summary>
    /// Your Enterprise API key. Use <c>sk_live_*</c> for direct access (single firm)
    /// or <c>sk_int_*</c> for integrator access (multi-tenant, managing multiple client firms).
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Base URL for the API. Defaults to the production endpoint <c>https://epostak.sk/api/enterprise</c>.
    /// Override this for staging environments or local development.
    /// </summary>
    public string BaseUrl { get; init; } = "https://epostak.sk/api/enterprise";

    /// <summary>
    /// Firm UUID to act on behalf of. When set, every API request includes an <c>X-Firm-Id</c> header
    /// so the server knows which client firm the operation targets. Required for integrator keys
    /// (<c>sk_int_*</c>) on most endpoints. Ignored for direct keys (<c>sk_live_*</c>).
    /// </summary>
    public string? FirmId { get; init; }

    /// <summary>
    /// Maximum number of retries on 429 (Too Many Requests) and 5xx responses.
    /// Uses exponential backoff with jitter. Defaults to 3. Set to 0 to disable retries.
    /// Only GET and DELETE requests are retried by default.
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}
