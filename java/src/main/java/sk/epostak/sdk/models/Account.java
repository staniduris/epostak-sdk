package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Account information including firm details, subscription plan, and document usage.
 *
 * @param firm  the firm associated with this API key
 * @param plan  the current subscription plan
 * @param usage document usage counters for the current billing period
 */
public record Account(
        Firm firm,
        Plan plan,
        Usage usage
) {
    /**
     * Firm details associated with the API key.
     *
     * @param name         the company name
     * @param ico          the Slovak ICO (company registration number)
     * @param peppolId     the Peppol participant ID, or {@code null}
     * @param peppolStatus Peppol registration status, e.g. {@code "ACTIVE"}
     */
    public record Firm(
            String name,
            String ico,
            @SerializedName("peppol_id") String peppolId,
            @SerializedName("peppol_status") String peppolStatus
    ) {}

    /**
     * Subscription plan information.
     *
     * @param name   the plan name, e.g. {@code "Starter"}, {@code "Business"}, {@code "Enterprise"}
     * @param status the plan status, e.g. {@code "active"}, {@code "trial"}
     */
    public record Plan(
            String name,
            String status
    ) {}

    /**
     * Document usage counters for the current billing period.
     *
     * @param outbound number of outbound documents sent
     * @param inbound  number of inbound documents received
     */
    public record Usage(
            int outbound,
            int inbound
    ) {}
}
