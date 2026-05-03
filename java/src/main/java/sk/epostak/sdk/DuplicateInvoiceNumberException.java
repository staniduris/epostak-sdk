package sk.epostak.sdk;

import java.util.List;

/**
 * Thrown when {@code POST /api/v1/documents/send} (or the dashboard
 * create endpoint) rejects an outbound invoice whose {@code invoice_number}
 * already exists for the firm.
 * <p>
 * The conflict key is {@code (firmId, invoiceNumber)} — the recipient is
 * intentionally NOT part of it; outbound numbering belongs to the sender.
 *
 * <pre>{@code
 * try {
 *     client.documents().send(req);
 * } catch (DuplicateInvoiceNumberException e) {
 *     ExistingDocument existing = e.getExistingDocument();
 *     if (existing != null) {
 *         System.err.println("Already sent at " + existing.sentAt() +
 *                            ", id=" + existing.id());
 *     }
 * }
 * }</pre>
 */
public class DuplicateInvoiceNumberException extends EPostakException {

    /**
     * Identification of the recipient on the existing duplicate invoice.
     * Each field can be {@code null} if the original invoice did not have
     * it stored — in particular {@code peppolId} is always present when
     * the parent {@code recipient} object is non-null, but {@code ico} and
     * {@code name} are optional on the API side.
     */
    public record Recipient(String peppolId, String ico, String name) {}

    /**
     * The pre-existing outbound invoice that triggered the conflict.
     * {@code sentAt} is an ISO 8601 string — {@code peppolSentAt} if the
     * original was already delivered, otherwise {@code createdAt}.
     * {@code recipient} is {@code null} when the original invoice had no
     * recipient Peppol ID stored.
     */
    public record ExistingDocument(
        String id,
        String invoiceNumber,
        String status,
        String sentAt,
        Recipient recipient
    ) {}

    private final List<String> conflictKey;
    private final ExistingDocument existingDocument;

    public DuplicateInvoiceNumberException(
            int status,
            String message,
            String code,
            Object details,
            String type,
            String title,
            String detail,
            String instance,
            String requestId,
            String requiredScope,
            List<String> conflictKey,
            ExistingDocument existingDocument
    ) {
        super(status, message, code, details, type, title, detail, instance, requestId, requiredScope);
        this.conflictKey = conflictKey == null
            ? List.of("firmId", "invoiceNumber")
            : List.copyOf(conflictKey);
        this.existingDocument = existingDocument;
    }

    /** Always {@code ["firmId", "invoiceNumber"]}. */
    public List<String> getConflictKey() {
        return conflictKey;
    }

    /**
     * The pre-existing outbound invoice that caused the conflict, or
     * {@code null} if it was deleted between the constraint hit and the
     * server-side lookup.
     */
    public ExistingDocument getExistingDocument() {
        return existingDocument;
    }
}
