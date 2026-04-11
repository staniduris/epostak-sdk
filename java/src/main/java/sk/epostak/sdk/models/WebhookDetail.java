package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Webhook detail with HMAC signing secret and recent delivery history.
 *
 * @param id         the webhook UUID
 * @param url        the endpoint URL receiving webhook payloads
 * @param events     list of subscribed event types
 * @param isActive   {@code true} if the webhook is actively delivering events
 * @param createdAt  ISO 8601 timestamp of webhook creation
 * @param secret     HMAC-SHA256 signing secret -- only returned on creation, {@code null} on subsequent reads
 * @param deliveries recent delivery attempts (most recent first)
 */
public record WebhookDetail(
        String id,
        String url,
        List<String> events,
        @SerializedName("is_active") boolean isActive,
        @SerializedName("created_at") String createdAt,
        String secret,
        List<WebhookDelivery> deliveries
) {
    /**
     * A single webhook delivery attempt.
     *
     * @param id             the delivery UUID
     * @param webhookId      the parent webhook UUID
     * @param event          the event type, e.g. {@code "document.received"}
     * @param status         delivery status: {@code "success"}, {@code "failed"}, {@code "pending"}
     * @param attempts       number of delivery attempts so far
     * @param responseStatus the HTTP response status from the endpoint, or {@code null} if no response
     * @param createdAt      ISO 8601 timestamp of the delivery attempt
     */
    public record WebhookDelivery(
            String id,
            @SerializedName("webhook_id") String webhookId,
            String event,
            String status,
            int attempts,
            @SerializedName("response_status") Integer responseStatus,
            @SerializedName("created_at") String createdAt
    ) {}
}
