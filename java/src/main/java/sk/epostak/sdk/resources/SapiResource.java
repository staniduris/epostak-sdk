package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;

import java.util.LinkedHashMap;
import java.util.Map;

/** SAPI-SK 1.0 interoperable document send/receive endpoints. */
public final class SapiResource {

    private final HttpClient http;

    public SapiResource(HttpClient http) {
        this.http = http;
    }

    @SuppressWarnings("unchecked")
    public Map<String, Object> send(Map<String, Object> body, String participantId, String idempotencyKey) {
        return http.postWithHeaders(
                "/sapi/v1/document/send",
                body,
                Map.class,
                headers(participantId, idempotencyKey)
        );
    }

    @SuppressWarnings("unchecked")
    public Map<String, Object> receive(String participantId, Integer limit, String status, String pageToken) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("limit", limit);
        params.put("status", status);
        params.put("pageToken", pageToken);
        return http.getWithHeaders(
                "/sapi/v1/document/receive" + HttpClient.buildQuery(params),
                Map.class,
                headers(participantId, null)
        );
    }

    public Map<String, Object> receive(String participantId) {
        return receive(participantId, null, null, null);
    }

    @SuppressWarnings("unchecked")
    public Map<String, Object> get(String documentId, String participantId) {
        return http.getWithHeaders(
                "/sapi/v1/document/receive/" + HttpClient.encode(documentId),
                Map.class,
                headers(participantId, null)
        );
    }

    @SuppressWarnings("unchecked")
    public Map<String, Object> acknowledge(String documentId, String participantId) {
        return http.postWithHeaders(
                "/sapi/v1/document/receive/" + HttpClient.encode(documentId) + "/acknowledge",
                null,
                Map.class,
                headers(participantId, null)
        );
    }

    private static Map<String, String> headers(String participantId, String idempotencyKey) {
        Map<String, String> headers = new LinkedHashMap<>();
        headers.put("X-Peppol-Participant-Id", participantId);
        if (idempotencyKey != null) {
            headers.put("Idempotency-Key", idempotencyKey);
        }
        return headers;
    }
}
