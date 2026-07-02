package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.AckResponse;
import sk.epostak.sdk.models.BatchAckResponse;
import sk.epostak.sdk.models.WebhookQueueResponse;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Preferred pull/ack event facade over the webhook queue. */
public final class EventsResource {

    private final HttpClient http;

    public EventsResource(HttpClient http) {
        this.http = http;
    }

    public WebhookQueueResponse pull(Integer limit, String eventType) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("limit", limit);
        params.put("event_type", eventType);
        return http.get("/events/pull" + HttpClient.buildQuery(params), WebhookQueueResponse.class);
    }

    public WebhookQueueResponse pull() {
        return pull(null, null);
    }

    public AckResponse ack(String eventId) {
        return http.post("/events/" + HttpClient.encode(eventId) + "/ack", Map.of(), AckResponse.class);
    }

    public BatchAckResponse batchAck(List<String> eventIds) {
        return http.post("/events/batch-ack", Map.of("event_ids", eventIds), BatchAckResponse.class);
    }
}
