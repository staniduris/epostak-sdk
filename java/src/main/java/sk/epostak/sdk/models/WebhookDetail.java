package sk.epostak.sdk.models;

import java.util.List;

/**
 * Webhook detail with HMAC signing secret and recent delivery history.
 *
 * @param id             the webhook UUID
 * @param url            the HTTPS endpoint URL receiving webhook payloads,
 *                       or {@code null} for a pull-only subscription
 * @param events         list of subscribed event types
 * @param isActive       {@code true} if the webhook is actively delivering events
 * @param failedAttempts count of consecutive failed delivery attempts, or {@code null}
 *                       when returned from {@code POST /webhooks} at creation time
 * @param createdAt      ISO 8601 timestamp of webhook creation
 * @param secret         HMAC-SHA256 signing secret -- only returned on creation, {@code null} on subsequent reads
 * @param deliveries     recent delivery attempts (most recent first), or {@code null} on creation
 */
public record WebhookDetail(
        String id,
        String url,    // nullable — null for pull-only subscriptions
        List<String> events,
        boolean isActive,
        Integer failedAttempts,
        String createdAt,
        String secret,
        List<WebhookDelivery> deliveries
) {
    /**
     * A single webhook delivery attempt.
     *
     * @param id             the delivery UUID
     * @param webhookId      the parent webhook UUID
     * @param event          the event type, e.g. {@code "document.received"}
     * @param status         delivery status: {@code "PENDING"}, {@code "SUCCESS"},
     *                       {@code "FAILED"}, or {@code "RETRYING"} (UPPERCASE)
     * @param attempts       number of delivery attempts so far
     * @param responseStatus the HTTP response status from the endpoint, or {@code null} if no response
     * @param createdAt      ISO 8601 timestamp of the delivery attempt
     */
    public record WebhookDelivery(
            String id,
            String webhookId,
            String event,
            String status,
            int attempts,
            Integer responseStatus,
            String createdAt
    ) {}
}
