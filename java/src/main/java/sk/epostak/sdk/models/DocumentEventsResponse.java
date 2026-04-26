package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Response from {@code GET /documents/{id}/events}.
 *
 * @param documentId the document UUID the events belong to
 * @param events     ordered array of audit events
 * @param nextCursor cursor for the next page, or {@code null} when no more pages
 */
public record DocumentEventsResponse(
        String documentId,
        List<DocumentEvent> events,
        String nextCursor
) {
    /**
     * A single event in a document's audit trail.
     *
     * @param id        event UUID
     * @param eventType event type identifier (e.g. "status_changed", "respond_sent")
     * @param actor     actor that triggered the event (e.g. "system", "api_key", "user")
     * @param detail    human-readable detail, or {@code null}
     * @param meta      arbitrary structured metadata
     * @param occurredAt ISO 8601 timestamp when the event occurred
     */
    public record DocumentEvent(
            String id,
            String eventType,
            String actor,
            String detail,
            Map<String, Object> meta,
            String occurredAt
    ) {}
}
