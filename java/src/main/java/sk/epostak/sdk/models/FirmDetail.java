package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Detailed firm information including tax identifiers and Peppol registrations.
 *
 * @param id                the firm UUID
 * @param name              the company name
 * @param ico               the Slovak ICO (company registration number)
 * @param peppolId          the primary Peppol participant ID, or {@code null}
 * @param peppolStatus      Peppol registration status, e.g. {@code "ACTIVE"}, {@code "PENDING"}
 * @param dic               the tax identification number (DIC)
 * @param icDph             the VAT registration number (IC DPH), or {@code null}
 * @param address           the company postal address
 * @param peppolIdentifiers list of all registered Peppol identifiers
 * @param createdAt         ISO 8601 timestamp of firm creation
 */
public record FirmDetail(
        String id,
        String name,
        String ico,
        @SerializedName("peppol_id") String peppolId,
        @SerializedName("peppol_status") String peppolStatus,
        String dic,
        @SerializedName("ic_dph") String icDph,
        Document.PartyAddress address,
        @SerializedName("peppol_identifiers") List<PeppolIdentifier> peppolIdentifiers,
        @SerializedName("created_at") String createdAt
) {
    /**
     * A Peppol identifier registered for this firm.
     *
     * @param scheme     the identifier scheme, e.g. {@code "0245"}
     * @param identifier the identifier value, e.g. {@code "12345678"}
     */
    public record PeppolIdentifier(
            String scheme,
            String identifier
    ) {}
}
