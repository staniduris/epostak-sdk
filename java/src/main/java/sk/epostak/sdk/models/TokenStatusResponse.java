package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from {@code GET /auth/token/status} (also available at
 * {@code /sapi/v1/auth/status}). Introspects the calling JWT access token
 * and returns its metadata without revealing the underlying API key.
 *
 * @param valid                  {@code true} when the token is currently valid
 * @param tokenType              always {@code "access"}
 * @param clientId               the OAuth client ID (API key ID) that minted this token
 * @param firmId                 the firm UUID the token is scoped to
 * @param keyType                key prefix type: {@code "sk_live"}, {@code "sk_int"}, or {@code "sk_peppol"}
 * @param scope                  space-separated scope string granted to this token
 * @param issuedAt               ISO 8601 timestamp when the token was issued
 * @param expiresAt              ISO 8601 timestamp when the token expires
 * @param expiresInSeconds       seconds remaining until expiry (floored to 0 when expired)
 * @param shouldRefresh          {@code true} when the server recommends renewing now
 * @param refreshRecommendedAt   ISO 8601 timestamp at which the client should renew
 */
public record TokenStatusResponse(
        boolean valid,
        @SerializedName("token_type") String tokenType,
        @SerializedName("client_id") String clientId,
        @SerializedName("firm_id") String firmId,
        @SerializedName("key_type") String keyType,
        String scope,
        @SerializedName("issued_at") String issuedAt,
        @SerializedName("expires_at") String expiresAt,
        @SerializedName("expires_in_seconds") int expiresInSeconds,
        @SerializedName("should_refresh") boolean shouldRefresh,
        @SerializedName("refresh_recommended_at") String refreshRecommendedAt
) {}
