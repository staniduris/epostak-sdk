package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Account information including firm details, subscription plan, document usage,
 * and plan-specific quota limits.
 *
 * @param firm   the firm associated with this API key
 * @param plan   the current subscription plan
 * @param usage  document usage counters for the current billing period
 * @param limits plan-specific monthly quotas; {@code -1} means "unlimited"
 */
public record Account(
        Firm firm,
        Plan plan,
        Usage usage,
        Limits limits
) {
    /**
     * Firm details associated with the API key.
     *
     * @param name         the company name
     * @param ico          the Slovak ICO (company registration number), or {@code null}
     * @param peppolId     the Peppol participant ID, or {@code null}
     * @param peppolStatus Peppol registration status, e.g. {@code "ACTIVE"}
     */
    public record Firm(
            String name,
            String ico,
            String peppolId,
            String peppolStatus
    ) {}

    /**
     * Subscription plan information.
     *
     * @param name   the plan identifier, e.g. {@code "free"}, {@code "api-enterprise"}
     * @param status {@code "active"} while the plan is valid, {@code "expired"} after expiry
     */
    public record Plan(
            String name,
            String status
    ) {}

    /**
     * Document usage counters for the current billing period.
     *
     * @param outbound       number of outbound documents sent
     * @param inbound        number of inbound documents received
     * @param ocrExtractions number of OCR extractions performed this month
     */
    public record Usage(
            int outbound,
            int inbound,
            @SerializedName("ocr_extractions") int ocrExtractions
    ) {}

    /**
     * Plan-specific monthly quota limits. A value of {@code -1} means no cap.
     *
     * @param documentsPerMonth max number of documents per month, or {@code -1} for unlimited
     * @param ocrPerMonth       max number of OCR extractions per month, or {@code -1} for unlimited
     */
    public record Limits(
            @SerializedName("documents_per_month") int documentsPerMonth,
            @SerializedName("ocr_per_month") int ocrPerMonth
    ) {}
}
