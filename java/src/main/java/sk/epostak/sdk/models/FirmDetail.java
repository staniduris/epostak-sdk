package sk.epostak.sdk.models;

import java.util.List;

/**
 * Detailed firm information including tax identifiers and plan.
 *
 * @param id                the firm UUID
 * @param name              the company name
 * @param ico               the Slovak ICO (company registration number), or {@code null}
 * @param dic               the tax identification number (DIC), or {@code null}
 * @param icDph             the VAT registration number (IC DPH), or {@code null}
 * @param address           the company postal address
 * @param peppolId          the primary Peppol participant ID, or {@code null}
 * @param peppolStatus      Peppol registration status, e.g. {@code "ACTIVE"}, {@code "PENDING"}
 * @param plan              the firm's current plan identifier
 * @param peppolIdentifiers list of all registered Peppol identifiers, or {@code null}
 * @param createdAt         ISO 8601 timestamp of firm creation
 */
public record FirmDetail(
        String id,
        String name,
        String ico,
        String dic,
        String icDph,
        Document.PartyAddress address,
        String peppolId,
        String peppolStatus,
        String plan,
        List<PeppolIdentifier> peppolIdentifiers,
        String createdAt
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
