package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from assigning a firm to an integrator by ICO. Returned as HTTP 201.
 *
 * @param firm   the assigned firm details
 * @param status assignment status, typically {@code "active"}
 */
public record AssignFirmResponse(
        AssignedFirm firm,
        String status
) {
    /**
     * Summary of the assigned firm.
     *
     * @param id           the firm UUID
     * @param name         the company name
     * @param ico          the Slovak ICO
     * @param peppolId     the Peppol participant ID, or {@code null}
     * @param peppolStatus Peppol registration status
     */
    public record AssignedFirm(
            String id,
            String name,
            String ico,
            @SerializedName("peppol_id") String peppolId,
            @SerializedName("peppol_status") String peppolStatus
    ) {}
}
