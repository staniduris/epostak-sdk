package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
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
     * @param id             delivery UUID
     * @param webhookId      the parent webhook UUID
     * @param event          event type that triggered this delivery
     * @param status         delivery status (SUCCESS, FAILED, PENDING, RETRYING)
     * @param attempts       number of delivery attempts made
     * @param responseStatus HTTP status code returned by the webhook URL
     * @param responseBody   truncated response body from the webhook URL
     * @param lastAttemptAt  ISO 8601 timestamp of the last delivery attempt
     * @param nextRetryAt    ISO 8601 timestamp of the next scheduled retry, or {@code null}
     * @param createdAt      ISO 8601 timestamp when the delivery was created
     */
    public record DeliveryDetail(
            String id,
            @SerializedName("webhookId") String webhookId,
            String event,
            String status,
            int attempts,
            @SerializedName("responseStatus") Integer responseStatus,
            @SerializedName("responseBody") String responseBody,
            @SerializedName("lastAttemptAt") String lastAttemptAt,
            @SerializedName("nextRetryAt") String nextRetryAt,
            @SerializedName("createdAt") String createdAt
    ) {}
}
