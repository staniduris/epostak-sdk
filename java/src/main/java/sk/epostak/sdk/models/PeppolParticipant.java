package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Peppol SMP participant with supported document capabilities.
 *
 * @param peppolId     the Peppol participant ID, e.g. {@code "0245:12345678"}
 * @param name         the registered company name
 * @param country      ISO 3166-1 alpha-2 country code
 * @param capabilities list of supported document type capabilities
 */
public record PeppolParticipant(
        @SerializedName("peppol_id") String peppolId,
        String name,
        String country,
        List<Capability> capabilities
) {
    /**
     * A document capability supported by this participant.
     *
     * @param documentTypeId   the UBL document type identifier
     * @param processId        the business process identifier
     * @param transportProfile the AS4 transport profile
     */
    public record Capability(
            @SerializedName("document_type_id") String documentTypeId,
            @SerializedName("process_id") String processId,
            @SerializedName("transport_profile") String transportProfile
    ) {}
}
