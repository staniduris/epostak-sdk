namespace EPostak;

/// <summary>
/// Configuration for the ePosťak Enterprise API client. Provide <see cref="ClientId"/>
/// and <see cref="ClientSecret"/> for OAuth JWT authentication. The SDK automatically
/// mints and refreshes JWT tokens via the SAPI token endpoint.
/// </summary>
/// <example>
/// Direct API key (single firm):
/// <code>
/// var config = new EPostakConfig { ClientId = "sk_live_xxxxx", ClientSecret = "sk_live_xxxxx" };
/// </code>
/// Integrator key (multi-tenant) scoped to a specific firm:
/// <code>
/// var config = new EPostakConfig { ClientId = "sk_int_xxxxx", ClientSecret = "sk_int_xxxxx", FirmId = "firm-uuid-here" };
/// </code>
/// </example>
public sealed class EPostakConfig
{
    /// <summary>
    /// OAuth client ID for the <c>client_credentials</c> grant. Typically your
    /// Enterprise API key (<c>sk_live_*</c> or <c>sk_int_*</c>).
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// OAuth client secret for the <c>client_credentials</c> grant. Typically the
    /// same value as <see cref="ClientId"/> for API-key-based authentication.
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Base URL for the API. Defaults to the production endpoint <c>https://epostak.sk/api/v1</c>.
    /// Override this for staging environments or local development.
    /// </summary>
    public string BaseUrl { get; init; } = "https://epostak.sk/api/v1";

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
