package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Response from pulling the webhook event queue.
 *
 * @param items   the list of pending events
 * @param hasMore whether more events remain in the queue beyond this page
 */
public record WebhookQueueResponse(
        List<WebhookQueueItem> items,
        @SerializedName("has_more") boolean hasMore
) {
    /**
     * A single event in the webhook queue.
     *
     * @param eventId   the event UUID (use this to acknowledge the event)
     * @param firmId    the firm UUID this event belongs to
     * @param event     the event type, e.g. {@code "document.received"}
     * @param createdAt ISO 8601 timestamp of when the event was created
     * @param payload   typed v1 webhook payload envelope
     */
    public record WebhookQueueItem(
            @SerializedName("event_id") String eventId,
            @SerializedName("firm_id") String firmId,
            String event,
            @SerializedName("created_at") String createdAt,
            WebhookPayloadEnvelope payload
    ) {}
}
