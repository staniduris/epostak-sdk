package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Request body for {@code POST /peppol/capabilities}, asking whether a participant
 * can receive a given document type.
 *
 * @param scheme       Peppol identifier scheme, e.g. {@code "0245"} for Slovak DIC
 * @param identifier   identifier value, e.g. {@code "12345678"}
 * @param documentType the UBL document type identifier to probe for, or {@code null}
 *                     to return the full set of supported document types without a
 *                     specific match
 */
public record CapabilitiesRequest(
        String scheme,
        String identifier,
        @SerializedName("document_type") String documentType
) {}
