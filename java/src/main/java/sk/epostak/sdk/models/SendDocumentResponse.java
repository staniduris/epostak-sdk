package sk.epostak.sdk.models;

/**
 * Response from sending a document via Peppol. Returned as HTTP 201 on success.
 *
 * @param documentId    the unique document UUID assigned by the API
 * @param messageId     the Peppol AS4 message ID
 * @param status        delivery status, {@code "SENT"} on successful transmission
 * @param payloadSha256 hex-lowercase SHA-256 digest over the canonical UBL XML
 *                      wire payload — lets the receiver verify the bytes off
 *                      Peppol AS4 match what ePošťák logged at send time.
 *                      Always present on 201 responses; absent ({@code null}) only
 *                      on {@code 202 SENT_DB_PENDING} recoveries.
 */
public record SendDocumentResponse(
        String documentId,
        String messageId,
        String status,
        String payloadSha256
) {}
