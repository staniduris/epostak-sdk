package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Response from {@code POST /api/v1/documents/status/batch}.
 *
 * @param total    number of requested IDs
 * @param found    number of documents visible to the caller
 * @param notFound number of missing or cross-tenant documents
 * @param results  per-ID results in the same order as the request
 */
public record DocumentStatusBatchResponse(
        int total,
        int found,
        int notFound,
        List<Item> results
) {
    /**
     * One requested document status. Missing or cross-tenant IDs return only
     * {@code id} and {@code error="not_found"}.
     */
    public record Item(
            String id,
            String error,
            String status,
            String documentType,
            String direction,
            String senderPeppolId,
            String receiverPeppolId,
            List<DocumentStatusResponse.StatusHistoryEntry> statusHistory,
            Map<String, Object> validationResult,
            String deliveredAt,
            String acknowledgedAt,
            String invoiceResponseStatus,
            String peppolMessageId,
            String as4MessageId,
            String createdAt,
            String updatedAt
    ) {}
}
