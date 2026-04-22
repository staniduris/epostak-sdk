package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from rotating the current API key's secret via {@code POST /auth/rotate-secret}.
 * <p>
 * The new {@code key} is returned ONCE — store it immediately. The previous
 * secret is invalidated on the server side; any in-flight requests signed with
 * it will be rejected with HTTP 401.
 * <p>
 * Rotation is not available for integrator subkeys ({@code sk_int_*}); the
 * server returns HTTP 409 in that case.
 *
 * @param keyId     the key UUID whose secret was rotated
 * @param key       the new API key value (only returned once)
 * @param prefix    the human-visible prefix of the new key, e.g. {@code "sk_live_abc"}
 * @param rotatedAt ISO 8601 timestamp when the rotation completed
 */
public record RotateSecretResponse(
        @SerializedName("key_id") String keyId,
        String key,
        String prefix,
        @SerializedName("rotated_at") String rotatedAt
) {}
