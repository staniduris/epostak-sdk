package sk.epostak.sdk.models;

import java.util.List;

/**
 * Paginated response of webhook delivery history.
 *
 * @param deliveries list of delivery records
 * @param total      total number of deliveries matching the filter
 * @param limit      number of deliveries returned
 * @param offset     offset used for pagination
 */
public record WebhookDeliveriesResponse(
        List<DeliveryDetail> deliveries,
        int total,
        int limit,
        int offset
) {
    /**
     * A single delivery record with full detail.
     *
     * @param id              delivery UUID
     * @param webhookId       the parent webhook UUID
     * @param event           event type that triggered this delivery
     * @param status          delivery status (UPPERCASE): one of {@code "PENDING"},
     *                        {@code "SUCCESS"}, {@code "FAILED"}, {@code "RETRYING"}
     * @param attempts        number of delivery attempts made
     * @param responseStatus  HTTP status code returned by the webhook URL, or {@code null}
     * @param responseBody    truncated response body from the webhook URL (up to 1000 chars);
     *                        only present when the deliveries endpoint was called with
     *                        {@code ?includeResponseBody=true}
     * @param lastAttemptAt   ISO 8601 timestamp of the last delivery attempt, or {@code null}
     * @param nextRetryAt     ISO 8601 timestamp of the next scheduled retry, or {@code null}
     * @param createdAt       ISO 8601 timestamp when the delivery was created
     * @param idempotencyKey  client-supplied idempotency key for the event that triggered
     *                        this delivery, or {@code null} when not set
     */
    public record DeliveryDetail(
            String id,
            String webhookId,
            String event,
            String status,
            int attempts,
            Integer responseStatus,
            String responseBody,
            String lastAttemptAt,
            String nextRetryAt,
            String createdAt,
            String idempotencyKey
    ) {}
}
