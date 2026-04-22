package sk.epostak.sdk.models;

import java.util.List;

/**
 * Request body for sending multiple documents in a single API call via
 * {@code POST /documents/send/batch}. Each item wraps a {@link SendDocumentRequest}
 * and may include an optional idempotency key; the batch endpoint forwards the
 * key as the {@code Idempotency-Key} header on each per-item sub-request.
 *
 * <pre>{@code
 * BatchSendRequest req = new BatchSendRequest(List.of(
 *     new BatchSendRequest.BatchItem(
 *         SendDocumentRequest.builder("0245:12345678")
 *             .invoiceNumber("FV-2026-001").build(),
 *         "idem-001"),
 *     new BatchSendRequest.BatchItem(
 *         SendDocumentRequest.builder("0245:87654321")
 *             .invoiceNumber("FV-2026-002").build(),
 *         null)
 * ));
 * }</pre>
 *
 * @param items the documents to send, in order; max 50 items per batch
 */
public record BatchSendRequest(
        List<BatchItem> items
) {
    /**
     * A single item in a batch send request.
     *
     * @param document       the document to send (same shape as a single send)
     * @param idempotencyKey optional per-item idempotency key, or {@code null}.
     *                       If provided, resubmissions with the same key return the
     *                       original result instead of re-sending.
     */
    public record BatchItem(
            SendDocumentRequest document,
            String idempotencyKey
    ) {
        /**
         * Convenience constructor without an idempotency key.
         *
         * @param document the document to send
         */
        public BatchItem(SendDocumentRequest document) {
            this(document, null);
        }
    }
}
