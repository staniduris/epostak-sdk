package sk.epostak.sdk.models;

/**
 * Response from rotating the current API key via {@code POST /auth/rotate-secret}.
 * <p>
 * The new {@code key} value is returned ONCE — store it immediately. The
 * previous key is deactivated server-side; any in-flight requests signed with
 * it will be rejected with HTTP 401.
 * <p>
 * Rotation is not available for integrator subkeys ({@code sk_int_*}); the
 * server returns HTTP 403 in that case.
 *
 * @param key     the new API key value (only returned once)
 * @param prefix  the human-visible prefix of the new key, e.g. {@code "sk_live_abc"}
 * @param message confirmation message, e.g. "Key rotated. Save it — it will not be shown again."
 */
public record RotateSecretResponse(
        String key,
        String prefix,
        String message
) {}
