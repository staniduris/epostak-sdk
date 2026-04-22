package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from a batch send operation. Reports overall counts and a per-item
 * result in the same order as the request.
 *
 * @param total     total number of items in the batch
 * @param succeeded number of items that returned a 2xx status
 * @param failed    number of items that returned a non-2xx status
 * @param results   per-item results in request order
 */
public record BatchSendResponse(
        int total,
        int succeeded,
        int failed,
        List<BatchResult> results
) {
    /**
     * Result of a single item in a batch send operation.
     *
     * @param index  zero-based index of the item in the request
     * @param status HTTP status code for this item (e.g. {@code 202}, {@code 400}, {@code 422})
     * @param result the per-item result payload. On success, shaped like
     *               {@link SendDocumentResponse}. On failure, shaped like an error body
     *               with {@code error.message} and {@code error.code}. May be {@code null}.
     */
    public record BatchResult(
            int index,
            int status,
            Object result
    ) {}
}
