package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.Webhook;
import sk.epostak.sdk.models.WebhookDeliveriesResponse;
import sk.epostak.sdk.models.WebhookDetail;
import sk.epostak.sdk.models.WebhookRotateSecretResponse;
import sk.epostak.sdk.models.WebhookTestResponse;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.StringJoiner;

/**
 * Manage webhook subscriptions for push-based event delivery.
 * <p>
 * Supports creating, listing, updating, and deleting webhook endpoints.
 * For polling-based consumption, use the {@link #queue()} sub-resource instead.
 * <p>
 * Access via {@code client.webhooks()}.
 *
 * <pre>{@code
 * // Register a webhook for document events
 * WebhookDetail hook = client.webhooks().create(
 *     "https://example.com/webhook",
 *     List.of("document.received", "document.delivered"));
 * System.out.println("Secret: " + hook.secret()); // Save this for signature verification
 * }</pre>
 */
public final class WebhooksResource {

    private final HttpClient http;
    private final WebhookQueueResource queue;

    /**
     * Creates a new webhooks resource.
     *
     * @param http the HTTP client used for API communication
     */
    public WebhooksResource(HttpClient http) {
        this.http = http;
        this.queue = new WebhookQueueResource(http);
    }

    /**
     * Access the webhook pull queue for polling-based event consumption.
     *
     * @return the queue sub-resource
     */
    public WebhookQueueResource queue() {
        return queue;
    }

    /**
     * Register a new webhook endpoint. The response includes an HMAC-SHA256
     * signing secret that should be stored securely for verifying webhook payloads.
     *
     * @param url    the webhook endpoint URL (must be HTTPS)
     * @param events list of event types to subscribe to (e.g. {@code "document.received"}),
     *               or {@code null} to subscribe to all events
     * @return the created webhook detail including the signing secret
     * @throws sk.epostak.sdk.EPostakException if the URL is invalid or the request fails
     */
    public WebhookDetail create(String url, List<String> events) {
        return create(url, events, null);
    }

    /**
     * Register a new webhook endpoint with an explicit {@code Idempotency-Key}
     * header. Replaying the same key while the original request is still in
     * flight returns HTTP 409 ({@code idempotency_conflict}).
     *
     * @param url            the webhook endpoint URL (must be HTTPS)
     * @param events         list of event types to subscribe to, or {@code null}
     * @param idempotencyKey opaque idempotency key (1-255 chars), or {@code null} to skip
     * @return the created webhook detail including the signing secret
     * @throws sk.epostak.sdk.EPostakException if the URL is invalid or the request fails
     */
    public WebhookDetail create(String url, List<String> events, String idempotencyKey) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("url", url);
        if (events != null) body.put("events", events);
        if (idempotencyKey != null) {
            return http.postIdempotent("/webhooks", body, WebhookDetail.class, idempotencyKey);
        }
        return http.post("/webhooks", body, WebhookDetail.class);
    }

    /**
     * Register a new webhook endpoint subscribing to all event types.
     *
     * @param url the webhook endpoint URL (must be HTTPS)
     * @return the created webhook detail including the signing secret
     * @throws sk.epostak.sdk.EPostakException if the URL is invalid or the request fails
     */
    public WebhookDetail create(String url) {
        return create(url, null);
    }

    /**
     * List all registered webhook endpoints.
     *
     * @return list of webhooks, or an empty list if none
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public List<Webhook> list() {
        WebhookListWrapper wrapper = http.get("/webhooks", WebhookListWrapper.class);
        return wrapper != null ? wrapper.data() : List.of();
    }

    /**
     * Get webhook detail including delivery history for recent events.
     *
     * @param id the webhook UUID
     * @return the webhook detail with delivery attempts
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public WebhookDetail get(String id) {
        return http.get("/webhooks/" + HttpClient.encode(id), WebhookDetail.class);
    }

    /**
     * Update a webhook endpoint. Only provided (non-null) fields are changed.
     *
     * @param id       the webhook UUID
     * @param url      new endpoint URL, or {@code null} to keep current
     * @param events   new event type list, or {@code null} to keep current
     * @param isActive set to {@code false} to pause delivery, or {@code null} to keep current
     * @return the updated webhook
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public Webhook update(String id, String url, List<String> events, Boolean isActive) {
        Map<String, Object> body = new LinkedHashMap<>();
        if (url != null) body.put("url", url);
        if (events != null) body.put("events", events);
        if (isActive != null) body.put("isActive", isActive);
        return http.patch("/webhooks/" + HttpClient.encode(id), body, Webhook.class);
    }

    /**
     * Delete a webhook endpoint. This is irreversible. Returns HTTP 204 on success.
     *
     * @param id the webhook UUID
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public void delete(String id) {
        http.deleteVoid("/webhooks/" + HttpClient.encode(id));
    }

    /**
     * Send a test event to a webhook endpoint. Useful for verifying your
     * webhook URL is reachable and responding correctly.
     *
     * @param id    the webhook UUID to test
     * @param event the event type to simulate (e.g. {@code "document.created"}), or {@code null} for the server default
     * @return the test result with success status, HTTP status code, and response time
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public WebhookTestResponse test(String id, String event) {
        Map<String, Object> body = new LinkedHashMap<>();
        if (event != null) body.put("event", event);
        return http.post("/webhooks/" + HttpClient.encode(id) + "/test", body, WebhookTestResponse.class);
    }

    /**
     * Send a test event to a webhook endpoint using the server default event type.
     *
     * @param id the webhook UUID to test
     * @return the test result
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public WebhookTestResponse test(String id) {
        return test(id, null);
    }

    /**
     * Get paginated delivery history for a webhook.
     *
     * @param id     the webhook UUID
     * @param params optional query parameters: {@code limit} (1-100), {@code offset},
     *               {@code status} (UPPERCASE: {@code PENDING}, {@code SUCCESS},
     *               {@code FAILED}, {@code RETRYING}), {@code event}
     * @return paginated delivery records with total count
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public WebhookDeliveriesResponse deliveries(String id, Map<String, Object> params) {
        StringJoiner qj = new StringJoiner("&", "?", "");
        qj.setEmptyValue("");
        if (params != null) {
            for (var entry : params.entrySet()) {
                if (entry.getValue() != null) {
                    qj.add(HttpClient.encode(entry.getKey()) + "=" + HttpClient.encode(String.valueOf(entry.getValue())));
                }
            }
        }
        return http.get("/webhooks/" + HttpClient.encode(id) + "/deliveries" + qj, WebhookDeliveriesResponse.class);
    }

    /**
     * Get paginated delivery history for a webhook with default parameters.
     *
     * @param id the webhook UUID
     * @return paginated delivery records
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public WebhookDeliveriesResponse deliveries(String id) {
        return deliveries(id, null);
    }

    /**
     * Rotate a webhook's HMAC-SHA256 signing secret. Issues a fresh secret
     * and invalidates the previous one immediately. The returned {@code secret}
     * is shown ONCE — store it right away; there is no way to retrieve it
     * later. In-flight deliveries signed with the old secret will no longer
     * verify on the receiving side. Non-destructive alternative to
     * delete+recreate when a secret leaks.
     *
     * @param id webhook UUID whose secret to rotate
     * @return the new signing secret (only shown once) and a confirmation message
     * @throws sk.epostak.sdk.EPostakException if the webhook is not found or the request fails
     */
    public WebhookRotateSecretResponse rotateSecret(String id) {
        return http.post(
            "/webhooks/" + HttpClient.encode(id) + "/rotate-secret",
            null,
            WebhookRotateSecretResponse.class
        );
    }

    // -- internal wrappers ----------------------------------------------------

    private record WebhookListWrapper(List<Webhook> data) {}
}
