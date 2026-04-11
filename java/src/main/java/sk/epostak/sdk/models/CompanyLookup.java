package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Slovak company lookup result by ICO.
 *
 * @param ico      the company registration number (ICO)
 * @param name     the company name
 * @param dic      the tax identification number (DIC)
 * @param icDph    the VAT registration number (IC DPH), or {@code null} if not VAT-registered
 * @param address  the company postal address
 * @param peppolId the Peppol participant ID, or {@code null} if not registered on Peppol
 */
public record CompanyLookup(
        String ico,
        String name,
        String dic,
        @SerializedName("ic_dph") String icDph,
        Document.PartyAddress address,
        @SerializedName("peppol_id") String peppolId
) {}
