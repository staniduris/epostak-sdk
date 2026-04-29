using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Auth — OAuth client_credentials flow + key management
// ---------------------------------------------------------------------------

/// <summary>
/// Response from <c>POST /auth/token</c> and <c>POST /auth/renew</c> — OAuth
/// <c>client_credentials</c> access + refresh token pair.
///
/// Access tokens expire in 15 minutes (<c>expires_in: 900</c>); refresh tokens
/// are valid for 30 days and rotate on every renewal call. Always replace your
/// stored refresh token with the value returned by <c>RenewAsync</c>.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>Short-lived JWT access token (sent as <c>Authorization: Bearer ...</c>).</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    /// <summary>Refresh token used by <c>auth.renew()</c> to mint a new access token.</summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    /// <summary>Token type — always <c>"Bearer"</c>.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Lifetime of the access token in seconds (<c>900</c> = 15 minutes).</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>Resolved scope — space-separated list, or <c>"*"</c> for wildcard keys.</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    /// <summary>
    /// Server-recommended timestamp at which the client should renew the
    /// access token. Only present on <c>auth.renew()</c> responses.
    /// </summary>
    [JsonPropertyName("refresh_recommended_at")]
    public string? RefreshRecommendedAt { get; set; }

    /// <summary>Whether the server is requesting a renew on the next call.</summary>
    [JsonPropertyName("should_refresh")]
    public bool? ShouldRefresh { get; set; }
}

/// <summary>
/// Response from <c>POST /auth/revoke</c>. Idempotent — returns 200 even on
/// misses so this is safe to call unconditionally on logout.
/// </summary>
public sealed class RevokeResponse
{
    /// <summary>Whether the revocation request was processed.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Human-readable confirmation message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>ISO 8601 timestamp when the access token will fully expire (access-token revocations).</summary>
    [JsonPropertyName("access_tokens_expire_at")]
    public string? AccessTokensExpireAt { get; set; }

    /// <summary>ISO 8601 server timestamp.</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// Response from <c>GET</c> / <c>PUT /auth/ip-allowlist</c>. An empty list
/// means no IP restriction is in effect.
/// </summary>
public sealed class IpAllowlistResponse
{
    /// <summary>Bare IP addresses or CIDR blocks (<c>addr/prefix</c>). Max 50 entries.</summary>
    [JsonPropertyName("ip_allowlist")]
    public List<string> IpAllowlist { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Cursor pagination
// ---------------------------------------------------------------------------

/// <summary>
/// Generic cursor-paginated page. Used by endpoints that walk
/// <c>(timestamp DESC, id DESC)</c> keysets — pass <see cref="NextCursor"/>
/// from one page back into the next call until <see cref="HasMore"/> is
/// <c>false</c> (or <see cref="NextCursor"/> comes back as <c>null</c>).
/// </summary>
public sealed class CursorPage<T>
{
    /// <summary>Items in the current page, newest first.</summary>
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    /// <summary>Opaque cursor for the next page, or <c>null</c> when there is no next page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }

    /// <summary>Whether more pages are available.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

// ---------------------------------------------------------------------------
// Audit
// ---------------------------------------------------------------------------

/// <summary>Actor type recorded against an audit row.</summary>
public enum AuditActorType
{
    /// <summary>A human user.</summary>
    User,
    /// <summary>A direct API key (<c>sk_live_*</c>).</summary>
    ApiKey,
    /// <summary>An integrator API key (<c>sk_int_*</c>).</summary>
    IntegratorKey,
    /// <summary>Internal system action (no human/key actor).</summary>
    System,
}

/// <summary>
/// A single row in the per-firm audit feed. Field naming follows the wire
/// format (snake_case JSON) — these rows are JSON-stringified and forwarded
/// to SIEMs so the property layout is kept stable.
/// </summary>
public sealed class AuditEvent
{
    /// <summary>Audit row UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>ISO 8601 timestamp when the event occurred.</summary>
    [JsonPropertyName("occurred_at")]
    public string OccurredAt { get; set; } = "";

    /// <summary>Type of actor that triggered the event.</summary>
    [JsonPropertyName("actor_type")]
    public string ActorType { get; set; } = "";

    /// <summary>UUID of the actor (key ID, user ID, or <c>null</c> for <c>system</c>).</summary>
    [JsonPropertyName("actor_id")]
    public string? ActorId { get; set; }

    /// <summary>Event name (e.g. <c>jwt.issued</c>, <c>api_key.rotated</c>, <c>refresh.revoked</c>).</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    /// <summary>Type of the target object (e.g. <c>jwt</c>, <c>apiKey</c>, <c>refreshToken</c>).</summary>
    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    /// <summary>UUID of the target object, when applicable.</summary>
    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }

    /// <summary>Source IP, when captured.</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Caller User-Agent, when captured.</summary>
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>Free-form metadata (e.g. <c>{ scope, key_type }</c>).</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}

/// <summary>Query parameters for <c>GET /audit</c>.</summary>
public sealed class AuditListParams
{
    /// <summary>Exact match on the <c>event</c> field (e.g. <c>"jwt.issued"</c>).</summary>
    public string? Event { get; set; }

    /// <summary>Exact match on the <c>actor_type</c> field.</summary>
    public AuditActorType? ActorType { get; set; }

    /// <summary>ISO 8601 timestamp — only return rows newer than or equal to this.</summary>
    public string? Since { get; set; }

    /// <summary>ISO 8601 timestamp — only return rows older than or equal to this.</summary>
    public string? Until { get; set; }

    /// <summary>Opaque cursor from a previous page's <c>next_cursor</c>.</summary>
    public string? Cursor { get; set; }

    /// <summary>Page size (1–100). Defaults to 20.</summary>
    public int? Limit { get; set; }
}
