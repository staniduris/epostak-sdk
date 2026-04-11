package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from sending an invoice response (accept/reject/query).
 *
 * @param documentId     the document UUID that was responded to
 * @param responseStatus the response status that was sent: {@code "AP"}, {@code "RE"}, or {@code "UQ"}
 * @param respondedAt    ISO 8601 timestamp of when the response was sent
 */
public record InvoiceRespondResponse(
        @SerializedName("document_id") String documentId,
        @SerializedName("response_status") String responseStatus,
        @SerializedName("responded_at") String respondedAt
) {}
