package sk.epostak.sdk.models;

/**
 * Parameters for {@code POST /webhooks/{id}/test}.
 * <p>
 * Use the fluent setter to set an optional event type to simulate.
 * The server default is {@code document.created} when no event is provided.
 *
 * <pre>{@code
 * WebhookTestResponse result = client.webhooks().test(
 *     webhookId,
 *     new WebhookTestParams().event("document.delivered"));
 * }</pre>
 */
public final class WebhookTestParams {

    private String event;

    /**
     * Creates params with no event override (server will use its default).
     */
    public WebhookTestParams() {}

    /**
     * Set the event type to simulate.
     *
     * @param event the webhook event type, e.g. {@code "document.delivered"},
     *              {@code "document.received"}. Pass {@code null} to use the
     *              server default ({@code "document.created"}).
     * @return {@code this} for chaining
     */
    public WebhookTestParams event(String event) {
        this.event = event;
        return this;
    }

    /**
     * Returns the configured event type, or {@code null} when not set.
     *
     * @return the event type string, or {@code null}
     */
    public String getEvent() {
        return event;
    }
}
