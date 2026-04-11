package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from sending a document via Peppol.
 *
 * @param documentId the unique document UUID assigned by the API
 * @param messageId  the Peppol AS4 message ID
 * @param status     initial delivery status, e.g. {@code "SENDING"}
 */
public record SendDocumentResponse(
        @SerializedName("document_id") String documentId,
        @SerializedName("message_id") String messageId,
        String status
) {}
