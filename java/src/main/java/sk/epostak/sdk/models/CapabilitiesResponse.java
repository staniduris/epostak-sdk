package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Response from {@code POST /peppol/capabilities}.
 *
 * @param found                   {@code true} if the participant was found in the SMP
 * @param accepts                 list of business process IDs the participant advertises,
 *                                or {@code null} when {@code found == false}
 * @param supportedDocumentTypes  list of UBL document type identifiers the participant
 *                                can receive, or {@code null} when {@code found == false}
 * @param matchedDocumentType     the document type ID that matched the requested
 *                                {@code documentType}, or {@code null} if no specific type
 *                                was requested or no match was found
 */
public record CapabilitiesResponse(
        boolean found,
        List<String> accepts,
        @SerializedName("supported_document_types") List<String> supportedDocumentTypes,
        @SerializedName("matched_document_type") String matchedDocumentType
) {}
