package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Response from {@code GET /documents/{id}/responses}.
 *
 * @param documentId the document UUID the responses belong to
 * @param responses  array of Invoice Response records
 */
public record InvoiceResponsesListResponse(
        String documentId,
        List<InvoiceResponseItem> responses
) {
    /**
     * A single Invoice Response record.
     *
     * @param id             response UUID
     * @param responseCode   response status code (AB, IP, UQ, CA, RE, AP, PD)
     * @param note           optional note, or {@code null}
     * @param senderPeppolId Peppol participant ID of the response sender
     * @param createdAt      ISO 8601 timestamp when the response was created
     */
    public record InvoiceResponseItem(
            String id,
            String responseCode,
            String note,
            String senderPeppolId,
            String createdAt
    ) {}
}
