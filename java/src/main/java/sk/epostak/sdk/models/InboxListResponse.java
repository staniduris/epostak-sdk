package sk.epostak.sdk.models;

import java.util.List;

/**
 * Paginated list of inbox documents.
 *
 * @param documents the list of documents on this page
 * @param total     total number of matching documents
 * @param limit     max results per page
 * @param offset    current pagination offset
 */
public record InboxListResponse(
        List<Document> documents,
        int total,
        int limit,
        int offset
) {}
