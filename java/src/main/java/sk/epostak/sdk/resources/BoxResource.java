package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.BoxCreateRequest;
import sk.epostak.sdk.models.BoxListParams;
import sk.epostak.sdk.models.BoxScheduleRequest;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * ePošťák Box durable execution layer for staged, scheduled, and retryable
 * Peppol dispatch.
 */
public final class BoxResource {

    private final HttpClient http;

    public BoxResource(HttpClient http) {
        this.http = http;
    }

    public Map<String, Object> list(BoxListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("status", params.status());
            qp.put("direction", params.direction());
            qp.put("limit", params.limit());
            qp.put("offset", params.offset());
        }
        return (Map<String, Object>) http.get("/box/items" + HttpClient.buildQuery(qp), Map.class);
    }

    public Map<String, Object> list() {
        return list(null);
    }

    public Map<String, Object> create(BoxCreateRequest request) {
        return (Map<String, Object>) http.post("/box/items", request, Map.class);
    }

    public Map<String, Object> get(String itemId) {
        return (Map<String, Object>) http.get("/box/items/" + HttpClient.encode(itemId), Map.class);
    }

    public Map<String, Object> schedule(String itemId, BoxScheduleRequest request) {
        return (Map<String, Object>) http.post(
                "/box/items/" + HttpClient.encode(itemId) + "/schedule",
                request,
                Map.class
        );
    }

    public Map<String, Object> sendNow(String itemId) {
        return (Map<String, Object>) http.post(
                "/box/items/" + HttpClient.encode(itemId) + "/send-now",
                Map.of(),
                Map.class
        );
    }

    public Map<String, Object> retry(String itemId) {
        return (Map<String, Object>) http.post(
                "/box/items/" + HttpClient.encode(itemId) + "/retry",
                Map.of(),
                Map.class
        );
    }

    public Map<String, Object> cancel(String itemId) {
        return (Map<String, Object>) http.post(
                "/box/items/" + HttpClient.encode(itemId) + "/cancel",
                Map.of(),
                Map.class
        );
    }
}
