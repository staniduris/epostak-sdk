package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.Map;

/**
 * Delivery evidence for a sent document, including AS4 receipts and invoice responses.
 *
 * @param documentId      the document UUID
 * @param as4Receipt       AS4 receipt data, or {@code null} if not yet received
 * @param mlrDocument      Message Level Response document, or {@code null}
 * @param invoiceResponse  invoice response (accept/reject/query) from the receiver, or {@code null}
 * @param deliveredAt      ISO 8601 delivery timestamp, or {@code null}
 * @param sentAt           ISO 8601 send timestamp
 */
public record DocumentEvidenceResponse(
        @SerializedName("document_id") String documentId,
        @SerializedName("as4_receipt") Map<String, Object> as4Receipt,
        @SerializedName("mlr_document") Map<String, Object> mlrDocument,
        @SerializedName("invoice_response") InvoiceResponseEvidence invoiceResponse,
        @SerializedName("delivered_at") String deliveredAt,
        @SerializedName("sent_at") String sentAt
) {
    /**
     * Invoice response evidence from the receiver.
     *
     * @param status   response status: {@code "AP"} (accepted), {@code "RE"} (rejected), {@code "UQ"} (under query)
     * @param document the full invoice response document as a map
     */
    public record InvoiceResponseEvidence(
            String status,
            Map<String, Object> document
    ) {}
}
