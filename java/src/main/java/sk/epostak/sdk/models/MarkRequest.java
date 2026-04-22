package sk.epostak.sdk.models;

/**
 * Request body for marking an inbound document's processing state via
 * {@code POST /documents/{id}/mark}.
 *
 * @param state target state: one of {@code "delivered"}, {@code "processed"},
 *              {@code "failed"}, {@code "read"}
 * @param note  optional free-text note, e.g. failure reason; max 500 chars, or {@code null}
 */
public record MarkRequest(
        String state,
        String note
) {
    /**
     * Convenience constructor without a note.
     *
     * @param state target state
     */
    public MarkRequest(String state) {
        this(state, null);
    }
}
