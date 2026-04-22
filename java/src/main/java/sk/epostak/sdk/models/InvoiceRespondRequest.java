package sk.epostak.sdk.models;

/**
 * Request body for responding to a received invoice.
 * <p>
 * The {@code status} must be one of the seven UBL response codes the server
 * accepts:
 * <ul>
 *   <li>{@code "AB"} — accepted for billing</li>
 *   <li>{@code "IP"} — in process</li>
 *   <li>{@code "UQ"} — under query</li>
 *   <li>{@code "CA"} — conditionally accepted</li>
 *   <li>{@code "RE"} — rejected</li>
 *   <li>{@code "AP"} — accepted</li>
 *   <li>{@code "PD"} — paid</li>
 * </ul>
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
 * @param status one of {@code "AB"}, {@code "IP"}, {@code "UQ"}, {@code "CA"},
 *               {@code "RE"}, {@code "AP"}, {@code "PD"}
 * @param note   optional note for the response (max 500 chars), or {@code null}
 */
public record InvoiceRespondRequest(
        String status,
        String note
) {
    /**
     * Create a response with status only (no note).
     *
     * @param status one of {@code "AB"}, {@code "IP"}, {@code "UQ"}, {@code "CA"},
     *               {@code "RE"}, {@code "AP"}, {@code "PD"}
     */
    public InvoiceRespondRequest(String status) {
        this(status, null);
    }
}
