package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * Response from pulling the webhook event queue.
 *
 * @param items   the list of pending events
 * @param hasMore {@code true} if more events are available beyond this page
 */
public record WebhookQueueResponse(
        List<WebhookQueueItem> items,
        @SerializedName("has_more") boolean hasMore
) {
    /**
     * A single event in the webhook queue.
     *
     * @param id        the event UUID (use this to acknowledge the event)
     * @param type      the event type, e.g. {@code "document.received"}
     * @param createdAt ISO 8601 timestamp of when the event was created
     * @param payload   event payload data as a map
     */
    public record WebhookQueueItem(
            String id,
            String type,
            @SerializedName("created_at") String createdAt,
            Map<String, Object> payload
    ) {}
}
