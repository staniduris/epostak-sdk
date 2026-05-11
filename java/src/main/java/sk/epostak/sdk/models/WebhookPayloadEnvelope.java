package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Common envelope for every v1 webhook payload — received via push (POST body)
 * or pull ({@link WebhookQueueResponse.WebhookQueueItem#payload()}).
 *
 * <p>Example push-handler usage:
 * <pre>{@code
 * WebhookPayloadEnvelope env = gson.fromJson(body, WebhookPayloadEnvelope.class);
 * if (WebhookEvent.DOCUMENT_DELIVERED.equals(env.event)) {
 *     System.out.println("Delivered: " + env.data.documentId);
 * }
 * }</pre>
 */
public final class WebhookPayloadEnvelope {

    /**
     * Event type — one of the constants in {@link WebhookEvent},
     * e.g. {@code "document.sent"}.
     */
    public String event;

    /**
     * Payload schema version. Always {@code "1"} for the v1 contract.
     */
    @SerializedName("event_version")
    public String eventVersion;

    /**
     * Per-delivery UUID prefixed with {@code whk_}. Echoed in the
     * {@code X-Webhook-Id} header on push deliveries.
     * {@code null} for items returned from the pull queue.
     */
    @SerializedName("webhook_id")
    public String webhookId;

    /**
     * Pull-queue row UUID. Pass directly to
     * {@code client.webhooks().queue().ack(webhookEventId)} to acknowledge
     * the event without an extra GET round-trip.
     * {@code null} when no pull subscription exists for this event on the firm.
     */
    @SerializedName("webhook_event_id")
    public String webhookEventId;

    /** ISO 8601 timestamp when the dispatcher emitted this event. */
    public String timestamp;

    /** Business payload — see {@link WebhookPayloadData} for field docs. */
    public WebhookPayloadData data;
}
