package sk.epostak.sdk.models;

/**
 * Slovak company lookup result by ICO.
 *
 * @param ico               the company registration number (ICO)
 * @param name              the company name
 * @param address           the company postal address
 * @param dic               the tax identification number (DIC)
 * @param icDph             the VAT registration number (IC DPH), or empty string if not VAT-registered
 * @param legalForm         the legal form code, or empty string if unknown
 * @param peppolRegistered  {@code true} when the company is registered on the Peppol network
 * @param peppolId          the Peppol participant ID, or {@code null} if not registered on Peppol
 */
public record CompanyLookup(
        String ico,
        String name,
        Document.PartyAddress address,
        String dic,
        String icDph,
        String legalForm,
        boolean peppolRegistered,
        String peppolId
) {}
