package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from {@code POST /auth/token} and {@code POST /auth/renew} — OAuth
 * {@code client_credentials} access + refresh token pair.
 * <p>
 * Access tokens expire in 15 minutes ({@code expiresIn == 900}); refresh
 * tokens are valid for 30 days and rotate on every renewal call. Always
 * replace your stored refresh token with the value returned by
 * {@link sk.epostak.sdk.resources.AuthResource#renew(String)}.
 *
 * @param accessToken             short-lived JWT access token (sent as
 *                                {@code Authorization: Bearer ...})
 * @param refreshToken            refresh token used to mint a new access token
 * @param tokenType               token type — always {@code "Bearer"}
 * @param expiresIn               lifetime of the access token in seconds (e.g. {@code 900})
 * @param scope                   resolved scope — space-separated list, or
 *                                {@code "*"} for wildcard keys
 * @param refreshRecommendedAt    server-recommended timestamp at which the
 *                                client should renew. Only present on renew
 *                                responses; {@code null} otherwise.
 * @param shouldRefresh           whether the server is requesting a renew on
 *                                the next call. {@code null} when not present.
 */
public record TokenResponse(
        @SerializedName("access_token") String accessToken,
        @SerializedName("refresh_token") String refreshToken,
        @SerializedName("token_type") String tokenType,
        @SerializedName("expires_in") long expiresIn,
        String scope,
        @SerializedName("refresh_recommended_at") String refreshRecommendedAt,
        @SerializedName("should_refresh") Boolean shouldRefresh
) {}
