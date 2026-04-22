package sk.epostak.sdk.models;

/**
 * Response from acknowledging (marking as processed) an inbox document.
 *
 * @param documentId     the document UUID that was acknowledged
 * @param status         the new status, {@code "ACKNOWLEDGED"}
 * @param acknowledgedAt ISO 8601 timestamp of acknowledgement
 */
public record AcknowledgeResponse(
        String documentId,
        String status,
        String acknowledgedAt
) {}
