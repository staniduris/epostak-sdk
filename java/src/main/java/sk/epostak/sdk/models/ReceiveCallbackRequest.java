package sk.epostak.sdk.models;

import java.util.List;

/**
 * Request body for {@code POST /document/receive-callback} — registers a
 * webhook endpoint that will receive push notifications when documents arrive
 * for the authenticated firm.
 *
 * @param url    the webhook endpoint URL (must be HTTPS)
 * @param events event types to subscribe to, or {@code null} to default to
 *               {@code ["document.received"]}
 */
public record ReceiveCallbackRequest(
        String url,
        List<String> events
) {
    /**
     * Create a request subscribing only to the default {@code document.received} event.
     *
     * @param url the webhook endpoint URL
     */
    public ReceiveCallbackRequest(String url) {
        this(url, null);
    }
}
