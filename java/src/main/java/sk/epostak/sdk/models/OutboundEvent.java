package sk.epostak.sdk.models;

/**
 * A single event from the outbound event stream ({@code GET /outbound/events}).
 * <p>
 * The event stream is cursor-paginated. Each event records a state transition
 * or action on an outbound document. Use
 * {@link sk.epostak.sdk.resources.OutboundResource#events(OutboundEventsParams)}
 * to read the stream. Scope {@code documents:read} required.
 *
 * @see sk.epostak.sdk.resources.OutboundResource#events(OutboundEventsParams)
 */
public final class OutboundEvent {

    private final String id;
    private final String documentId;
    private final String type;
    private final String actor;
    private final String detail;
    private final Object meta;
    private final String occurredAt;

    public OutboundEvent(
            String id,
            String documentId,
            String type,
            String actor,
            String detail,
            Object meta,
            String occurredAt
    ) {
        this.id = id;
        this.documentId = documentId;
        this.type = type;
        this.actor = actor;
        this.detail = detail;
        this.meta = meta;
        this.occurredAt = occurredAt;
    }

    /** @return event UUID */
    public String getId() { return id; }

    /** @return UUID of the outbound document this event belongs to */
    public String getDocumentId() { return documentId; }

    /**
     * @return event type identifier, e.g. {@code "status_changed"},
     *         {@code "respond_sent"}, {@code "delivered"}
     */
    public String getType() { return type; }

    /**
     * @return actor that triggered the event, e.g. {@code "system"}, {@code "api_key"},
     *         {@code "user"}
     */
    public String getActor() { return actor; }

    /** @return human-readable detail, or {@code null} */
    public String getDetail() { return detail; }

    /** @return arbitrary metadata object attached to the event, or {@code null} */
    public Object getMeta() { return meta; }

    /** @return ISO 8601 timestamp when the event occurred */
    public String getOccurredAt() { return occurredAt; }
}
