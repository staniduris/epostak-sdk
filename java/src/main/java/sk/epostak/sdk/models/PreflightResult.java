package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Result of a preflight check on a Peppol receiver, indicating whether
 * the receiver is registered and supports the requested document type.
 *
 * @param receiverPeppolId     the receiver's Peppol participant ID that was checked
 * @param registered           {@code true} if the receiver is registered on the Peppol network
 * @param supportsDocumentType {@code true} if the receiver supports the requested document type
 * @param smpUrl               the SMP endpoint URL for this participant, or {@code null}
 */
public record PreflightResult(
        @SerializedName("receiver_peppol_id") String receiverPeppolId,
        boolean registered,
        @SerializedName("supports_document_type") boolean supportsDocumentType,
        @SerializedName("smp_url") String smpUrl
) {}
