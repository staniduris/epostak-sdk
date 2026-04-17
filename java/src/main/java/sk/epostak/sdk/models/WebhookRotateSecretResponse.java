package sk.epostak.sdk.models;

/**
 * Response from rotating a webhook's HMAC-SHA256 signing secret.
 * <p>
 * The {@code secret} field is returned ONCE — store it immediately. The
 * previous secret is invalidated on the server side; any in-flight
 * deliveries signed with it will no longer verify on the receiving side.
 *
 * @param id      the webhook UUID whose secret was rotated
 * @param secret  the new HMAC-SHA256 signing secret (only returned once)
 * @param message human-readable confirmation message
 */
public record WebhookRotateSecretResponse(
        String id,
        String secret,
        String message
) {}
