package sk.epostak.sdk.models;

/**
 * Canonical webhook event type constants for the ePošťák v1 webhook contract.
 *
 * <p>Use these constants when subscribing to events or matching on event type in
 * a {@link WebhookPayloadEnvelope}:
 * <pre>{@code
 * WebhookDetail hook = client.webhooks().create(
 *     "https://example.com/hook",
 *     List.of(WebhookEvent.DOCUMENT_RECEIVED, WebhookEvent.DOCUMENT_DELIVERED));
 * }</pre>
 */
public final class WebhookEvent {

    private WebhookEvent() {}

    /** A new document has been created / ingested in the system. */
    public static final String DOCUMENT_CREATED = "document.created";

    /** An outbound document was successfully submitted to the Peppol network (AS4 send succeeded). */
    public static final String DOCUMENT_SENT = "document.sent";

    /** An inbound document arrived from the Peppol network. */
    public static final String DOCUMENT_RECEIVED = "document.received";

    /** A document passed schematron / XSD validation. */
    public static final String DOCUMENT_VALIDATED = "document.validated";

    /** An outbound document was confirmed as delivered by the receiving AP. */
    public static final String DOCUMENT_DELIVERED = "document.delivered";

    /** An outbound document could not be delivered after all retry attempts. */
    public static final String DOCUMENT_DELIVERY_FAILED = "document.delivery_failed";

    /** A document was rejected (MLR from receiving AP, InvoiceResponse from buyer, or loopback). */
    public static final String DOCUMENT_REJECTED = "document.rejected";

    /** A buyer InvoiceResponse (PEPPOL BIS IMR) was received for a previously sent invoice. */
    public static final String DOCUMENT_RESPONSE_RECEIVED = "document.response_received";
}
