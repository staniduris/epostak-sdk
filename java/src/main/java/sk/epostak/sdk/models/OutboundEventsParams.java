package sk.epostak.sdk.models;

/**
 * Query parameters for {@code GET /outbound/events}. All fields are optional.
 *
 * @param documentId filter to events for a specific outbound document UUID
 * @param cursor     opaque cursor from a previous page's {@code nextCursor}
 * @param limit      page size (1-500, default 100)
 */
public record OutboundEventsParams(
        String documentId,
        String cursor,
        Integer limit
) {

    /**
     * Empty params — returns the latest page of all outbound events.
     *
     * @return params with no filters
     */
    public static OutboundEventsParams empty() {
        return new OutboundEventsParams(null, null, null);
    }

    /**
     * Filter events to a single document.
     *
     * @param documentId the outbound document UUID
     * @return params scoped to the given document
     */
    public static OutboundEventsParams forDocument(String documentId) {
        return new OutboundEventsParams(documentId, null, null);
    }
}
