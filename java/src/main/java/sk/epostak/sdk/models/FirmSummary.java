package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Summary of a firm in the integrator's portfolio.
 *
 * @param id           the firm UUID
 * @param name         the company name
 * @param ico          the Slovak ICO (company registration number)
 * @param peppolId     the Peppol participant ID, or {@code null} if not registered
 * @param peppolStatus Peppol registration status, e.g. {@code "ACTIVE"}, {@code "PENDING"}
 */
public record FirmSummary(
        String id,
        String name,
        String ico,
        @SerializedName("peppol_id") String peppolId,
        @SerializedName("peppol_status") String peppolStatus
) {}
