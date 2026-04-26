package sk.epostak.sdk.models;

import java.util.List;

/**
 * Paginated list of outbound documents from {@code GET /documents/outbox}.
 *
 * @param documents the outbound documents in the current page
 * @param total     total number of documents matching the query
 * @param offset    the offset that was applied
 * @param limit     the limit that was applied
 */
public record OutboxListResponse(
        List<Document> documents,
        int total,
        int offset,
        int limit
) {}
