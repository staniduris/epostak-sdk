package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * Full document status with delivery timeline history.
 *
 * @param id                    document UUID
 * @param status                current status, e.g. {@code "DELIVERED"}, {@code "FAILED"}
 * @param documentType          UBL document type identifier
 * @param senderPeppolId        sender's Peppol participant ID
 * @param receiverPeppolId      receiver's Peppol participant ID
 * @param statusHistory         ordered list of status transitions
 * @param validationResult      validation details (warnings, errors), or {@code null}
 * @param deliveredAt           ISO 8601 delivery timestamp, or {@code null}
 * @param acknowledgedAt        ISO 8601 acknowledgement timestamp, or {@code null}
 * @param invoiceResponseStatus invoice response status (AP/RE/UQ), or {@code null}
 * @param as4MessageId          AS4 message ID
 * @param createdAt             ISO 8601 creation timestamp
 * @param updatedAt             ISO 8601 last-update timestamp
 */
public record DocumentStatusResponse(
        String id,
        String status,
        @SerializedName("document_type") String documentType,
        @SerializedName("sender_peppol_id") String senderPeppolId,
        @SerializedName("receiver_peppol_id") String receiverPeppolId,
        @SerializedName("status_history") List<StatusHistoryEntry> statusHistory,
        @SerializedName("validation_result") Map<String, Object> validationResult,
        @SerializedName("delivered_at") String deliveredAt,
        @SerializedName("acknowledged_at") String acknowledgedAt,
        @SerializedName("invoice_response_status") String invoiceResponseStatus,
        @SerializedName("as4_message_id") String as4MessageId,
        @SerializedName("created_at") String createdAt,
        @SerializedName("updated_at") String updatedAt
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
