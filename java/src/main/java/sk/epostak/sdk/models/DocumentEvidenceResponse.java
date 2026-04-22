package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Delivery evidence for a sent document, including AS4 receipts, Message Level
 * Responses, and optional invoice-response / TDD records.
 *
 * @param documentId      the document UUID
 * @param as4Receipt      AS4 receipt data, or {@code null} if not yet received
 * @param mlrDocument     Message Level Response document, or {@code null}
 * @param invoiceResponse invoice response (accept/reject/query) from the receiver, or {@code null}
 * @param tdd             Tax Data Document reporting status towards the Slovak Financial
 *                        Administration, or {@code null} if not reported
 * @param deliveredAt     ISO 8601 delivery timestamp, or {@code null}
 * @param sentAt          ISO 8601 send timestamp, or {@code null}
 */
public record DocumentEvidenceResponse(
        String documentId,
        Map<String, Object> as4Receipt,
        Map<String, Object> mlrDocument,
        InvoiceResponseEvidence invoiceResponse,
        TddStatus tdd,
        String deliveredAt,
        String sentAt
) {
    /**
     * Invoice response evidence from the receiver.
     *
     * @param status   response status: one of {@code AB}, {@code IP}, {@code UQ},
     *                 {@code CA}, {@code RE}, {@code AP}, {@code PD}
     * @param document the full invoice response document (UBL ApplicationResponse XML as string, or map)
     */
    public record InvoiceResponseEvidence(
            String status,
            Object document
    ) {}

    /**
     * Tax Data Document reporting status (SK FS SR).
     *
     * @param reportedAt ISO 8601 timestamp of the TDD submission, or {@code null}
     * @param reported   {@code true} when the TDD was accepted by FS SR
     */
    public record TddStatus(
            String reportedAt,
            boolean reported
    ) {}
}
