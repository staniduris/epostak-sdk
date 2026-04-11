package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from acknowledging (marking as processed) an inbox document.
 *
 * @param documentId    the document UUID that was acknowledged
 * @param status        the new status, e.g. {@code "ACKNOWLEDGED"}
 * @param acknowledgedAt ISO 8601 timestamp of acknowledgement
 */
public record AcknowledgeResponse(
        @SerializedName("document_id") String documentId,
        String status,
        @SerializedName("acknowledged_at") String acknowledgedAt
) {}
