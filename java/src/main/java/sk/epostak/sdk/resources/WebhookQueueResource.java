package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.AckResponse;
import sk.epostak.sdk.models.BatchAckResponse;
import sk.epostak.sdk.models.WebhookQueueAllResponse;
import sk.epostak.sdk.models.WebhookQueueResponse;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Webhook pull queue for polling-based event consumption.
 * <p>
 * Alternative to push webhooks -- pull events from a queue at your own pace.
 * Events remain in the queue until explicitly acknowledged.
 * <p>
 * Access via {@code client.webhooks().queue()}.
 *
 * <pre>{@code
 * // Poll for new events and acknowledge them
 * WebhookQueueResponse response = client.webhooks().queue().pull(50, null);
 * List<String> ids = response.items().stream()
 *     .map(WebhookQueueResponse.WebhookQueueItem::id)
 *     .toList();
 * client.webhooks().queue().batchAck(ids);
 * }</pre>
 */
public final class WebhookQueueResource {

    private final HttpClient http;

    /**
     * Creates a new webhook queue resource.
     *
     * @param http the HTTP client used for API communication
     */
    WebhookQueueResource(HttpClient http) {
        this.http = http;
    }

    /**
     * Fetch pending events from the queue.
     *
     * @param limit     max items to return (1-100, default 20), or {@code null} for default
     * @param eventType optional event type filter (e.g. {@code "document.received"}), or {@code null} for all
     * @return the queue response with items and a {@code hasMore} flag
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public WebhookQueueResponse pull(Integer limit, String eventType) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("limit", limit);
        params.put("event_type", eventType);
        return http.get("/webhook-queue" + HttpClient.buildQuery(params), WebhookQueueResponse.class);
    }

    /**
     * Fetch pending events from the queue with default parameters.
     *
     * @return the queue response (limit 20, all event types)
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public WebhookQueueResponse pull() {
        return pull(null, null);
    }

    /**
     * Acknowledge (remove) a single event from the queue.
     *
     * @param eventId the event UUID to acknowledge
     * @return acknowledgement confirmation with {@code acknowledged = true}
     * @throws sk.epostak.sdk.EPostakException if the event is not found or the request fails
     */
    public AckResponse ack(String eventId) {
        return http.delete("/webhook-queue/" + HttpClient.encode(eventId), AckResponse.class);
    }

    /**
     * Batch acknowledge (remove) multiple events from the queue.
     *
     * @param eventIds list of event UUIDs to acknowledge
     * @return response containing the count of acknowledged events
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public BatchAckResponse batchAck(List<String> eventIds) {
        Map<String, Object> body = Map.of("event_ids", eventIds);
        return http.post("/webhook-queue/batch-ack", body, BatchAckResponse.class);
    }

    /**
     * Fetch events across all firms. Requires an integrator key ({@code sk_int_*}).
     *
     * @param limit max items to return (1-500, default 100), or {@code null} for default
     * @param since only return events created after this ISO 8601 timestamp, or {@code null}
     * @return the cross-firm queue response with events and count
     * @throws sk.epostak.sdk.EPostakException if the request fails or the key is not an integrator key
     */
    public WebhookQueueAllResponse pullAll(Integer limit, String since) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("limit", limit);
        params.put("since", since);
        return http.get("/webhook-queue/all" + HttpClient.buildQuery(params), WebhookQueueAllResponse.class);
    }

    /**
     * Fetch events across all firms with default parameters.
     *
     * @return the cross-firm queue response (limit 100)
     * @throws sk.epostak.sdk.EPostakException if the request fails or the key is not an integrator key
     */
    public WebhookQueueAllResponse pullAll() {
        return pullAll(null, null);
    }

    /**
     * Batch acknowledge events across all firms. Requires an integrator key ({@code sk_int_*}).
     *
     * @param eventIds list of event UUIDs to acknowledge
     * @return the response containing the count of acknowledged events
     * @throws sk.epostak.sdk.EPostakException if the request fails or the key is not an integrator key
     */
    public BatchAckAllResponse batchAckAll(List<String> eventIds) {
        Map<String, Object> body = Map.of("event_ids", eventIds);
        return http.post("/webhook-queue/all/batch-ack", body, BatchAckAllResponse.class);
    }

    /**
     * Response from cross-firm batch acknowledge.
     *
     * @param acknowledged the number of events that were successfully acknowledged
     */
    public record BatchAckAllResponse(int acknowledged) {}
}
