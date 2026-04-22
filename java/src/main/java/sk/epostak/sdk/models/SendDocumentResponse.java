package sk.epostak.sdk.models;

/**
 * Response from sending a document via Peppol. Returned as HTTP 201 on success.
 *
 * @param documentId the unique document UUID assigned by the API
 * @param messageId  the Peppol AS4 message ID
 * @param status     delivery status, {@code "SENT"} on successful transmission
 */
public record SendDocumentResponse(
        String documentId,
        String messageId,
        String status
) {}
