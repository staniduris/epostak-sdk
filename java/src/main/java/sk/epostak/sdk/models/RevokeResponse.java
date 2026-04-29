package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from {@code POST /auth/revoke}. Idempotent — the server returns
 * 200 even if the token is unknown or already revoked, so callers can invoke
 * this unconditionally on logout.
 *
 * @param success                 {@code true} when the call was accepted
 * @param message                 human-readable confirmation message
 * @param accessTokensExpireAt    ISO 8601 timestamp at which any minted access
 *                                tokens for the revoked refresh token will
 *                                fully expire — only present on access-token
 *                                revocations, otherwise {@code null}
 * @param timestamp               ISO 8601 server timestamp
 */
public record RevokeResponse(
        boolean success,
        String message,
        @SerializedName("access_tokens_expire_at") String accessTokensExpireAt,
        String timestamp
) {}
