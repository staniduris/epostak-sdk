package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Response from sending or idempotently replaying a document via Peppol.
 *
 * @param documentId    the unique document UUID assigned by the API
 * @param submissionId  Storecove-style alias for documentId
 * @param messageId     the Peppol AS4 message ID
 * @param status        latest persisted lifecycle status
 * @param duplicate     true only for an HTTP 200 idempotent replay
 * @param payloadSha256 hex-lowercase SHA-256 digest over the canonical UBL XML
 *                      wire payload — lets the receiver verify the bytes off
 *                      Peppol AS4 match what ePošťák logged at send time.
 *                      Always present on 201 responses; absent ({@code null}) only
 *                      on {@code 202 SENT_DB_PENDING} recoveries
 * @param warning       partial-failure explanation for SENT_DB_PENDING
 * @param links         convenience links for the submitted document
 */
public record SendDocumentResponse(
        String documentId,
        String submissionId,
        String messageId,
        String status,
        Boolean duplicate,
        String payloadSha256,
        String warning,
        Map<String, String> links
) {
    /** Source-compatible constructor for the previous four-field response shape. */
    public SendDocumentResponse(String documentId, String messageId, String status, String payloadSha256) {
        this(documentId, null, messageId, status, null, payloadSha256, null, null);
    }
}
