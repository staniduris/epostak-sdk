package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * Cross-firm webhook queue response for integrator keys.
 *
 * @param items   the list of events across all firms
 * @param hasMore whether more events remain in the queue beyond this page
 */
public record WebhookQueueAllResponse(
        List<WebhookQueueAllEvent> items,
        @SerializedName("has_more") boolean hasMore
) {
    /**
     * A single event in the cross-firm webhook queue, with firm identification.
     *
     * @param eventId   the event UUID (use this to acknowledge the event)
     * @param firmId    the firm UUID this event belongs to
     * @param event     the event type, e.g. {@code "document.received"}
     * @param payload   event payload data as a map
     * @param createdAt ISO 8601 timestamp of when the event was created
     */
    public record WebhookQueueAllEvent(
            @SerializedName("event_id") String eventId,
            @SerializedName("firm_id") String firmId,
            String event,
            Map<String, Object> payload,
            @SerializedName("created_at") String createdAt
    ) {}
}
