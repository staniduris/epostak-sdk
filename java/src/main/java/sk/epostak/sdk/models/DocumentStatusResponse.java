package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Full document status with delivery timeline history.
 *
 * @param id                    document UUID
 * @param status                current status, e.g. {@code "DELIVERED"}, {@code "FAILED"}
 * @param documentType          UBL document type identifier
 * @param senderPeppolId        sender's Peppol participant ID, or {@code null}
 * @param receiverPeppolId      receiver's Peppol participant ID, or {@code null}
 * @param statusHistory         ordered list of status transitions (may be empty)
 * @param validationResult      validation details (warnings, errors), or {@code null}
 * @param deliveredAt           ISO 8601 delivery timestamp, or {@code null}
 * @param acknowledgedAt        ISO 8601 acknowledgement timestamp, or {@code null}
 * @param invoiceResponseStatus invoice response status (one of {@code AB}, {@code IP},
 *                              {@code UQ}, {@code CA}, {@code RE}, {@code AP}, {@code PD}),
 *                              or {@code null}
 * @param as4MessageId          AS4 message ID, or {@code null}
 * @param createdAt             ISO 8601 creation timestamp
 * @param updatedAt             ISO 8601 last-update timestamp
 */
public record DocumentStatusResponse(
        String id,
        String status,
        String documentType,
        String senderPeppolId,
        String receiverPeppolId,
        List<StatusHistoryEntry> statusHistory,
        Map<String, Object> validationResult,
        String deliveredAt,
        String acknowledgedAt,
        String invoiceResponseStatus,
        String as4MessageId,
        String createdAt,
        String updatedAt
) {
    /**
     * A single entry in the document status history timeline.
     *
     * @param status    the status at this point in time
     * @param timestamp ISO 8601 timestamp of the transition
     * @param detail    human-readable detail message, or {@code null}
     */
    public record StatusHistoryEntry(
            String status,
            String timestamp,
            String detail
    ) {}
}
