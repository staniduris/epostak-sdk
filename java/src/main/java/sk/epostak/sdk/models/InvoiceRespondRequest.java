package sk.epostak.sdk.models;

/**
 * Request body for responding to a received invoice.
 *
 * <pre>{@code
 * // Accept an invoice
 * new InvoiceRespondRequest("AP", "Accepted, payment scheduled");
 *
 * // Reject an invoice
 * new InvoiceRespondRequest("RE", "Incorrect amounts");
 *
 * // Query (request clarification)
 * new InvoiceRespondRequest("UQ");
 * }</pre>
 *
 * @param status response status code: {@code "AP"} (accepted), {@code "RE"} (rejected), or {@code "UQ"} (under query)
 * @param note   optional note for the response, or {@code null}
 */
public record InvoiceRespondRequest(
        String status,
        String note
) {
    /**
     * Create a response with status only (no note).
     *
     * @param status response status code: {@code "AP"}, {@code "RE"}, or {@code "UQ"}
     */
    public InvoiceRespondRequest(String status) {
        this(status, null);
    }
}
