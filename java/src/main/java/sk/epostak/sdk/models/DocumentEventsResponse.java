package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * Response from {@code GET /documents/{id}/events}.
 *
 * @param processId  canonical bare Peppol process URN
 * @param documentId the document UUID the events belong to
 * @param events     ordered array of audit events
 * @param pagination cursor pagination metadata
 */
public record DocumentEventsResponse(
        @SerializedName("process_id") String processId,
        String documentId,
        List<DocumentEvent> events,
        Pagination pagination
) {
    /** Source-compatible constructor for the old top-level cursor shape. */
    public DocumentEventsResponse(String documentId, List<DocumentEvent> events, String nextCursor) {
        this(null, documentId, events, new Pagination(0, nextCursor, nextCursor != null));
    }

    /** Convenience accessor matching the previous SDK API. */
    public String nextCursor() {
        return pagination == null ? null : pagination.nextCursor();
    }

    public record Pagination(int limit, String nextCursor, boolean hasMore) {}

    /**
     * A single event in a document's audit trail.
     *
     * @param processId canonical bare Peppol process URN
     * @param id        event UUID
     * @param eventType event type identifier (e.g. "status_changed", "respond_sent")
     * @param actor     actor that triggered the event: "system", "user", or "api"
     * @param detail    human-readable detail, or {@code null}
     * @param meta      arbitrary structured metadata
     * @param occurredAt ISO 8601 timestamp when the event occurred
     */
    public record DocumentEvent(
            @SerializedName("process_id") String processId,
            String id,
            String eventType,
            String actor,
            String detail,
            Map<String, Object> meta,
            String occurredAt
    ) {
        public DocumentEvent(String id, String eventType, String actor, String detail,
                             Map<String, Object> meta, String occurredAt) {
            this(null, id, eventType, actor, detail, meta, occurredAt);
        }
    }
}
