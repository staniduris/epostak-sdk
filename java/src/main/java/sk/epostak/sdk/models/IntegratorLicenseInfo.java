package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response of {@code GET /api/v1/integrator/licenses/info}.
 * <p>
 * Tier rates are applied to the AGGREGATE document count across all the
 * integrator's {@code integrator-managed} firms. A 100-firm x 50-doc
 * integrator lands in tier 2-3, not tier 1 like a standalone firm would.
 * Volumes above {@link #contactThreshold()} ({@code 5000}) flip
 * {@link #exceedsAutoTier()} to {@code true}; auto-billing pauses there
 * and sales handles invoicing manually.
 *
 * @param integrator        integrator metadata (id, name, plan)
 * @param period            current billing period in {@code YYYY-MM} (SK timezone)
 * @param nextResetAt       ISO 8601 - 1st of next month, SK midnight in UTC
 * @param billable          aggregate over firms on the {@code integrator-managed} plan
 * @param nonManaged        linked firms paying their own plan
 * @param exceedsAutoTier   {@code true} when outbound or inbound exceeds {@link #contactThreshold()}
 * @param contactThreshold  threshold above which auto-billing stops (5000)
 * @param pricing           tier table for outbound + inbound API
 * @param firms             paginated per-firm breakdown (sorted by outboundCount desc)
 * @param pagination        pagination envelope
 */
public record IntegratorLicenseInfo(
        Integrator integrator,
        String period,
        String nextResetAt,
        Billable billable,
        NonManaged nonManaged,
        boolean exceedsAutoTier,
        int contactThreshold,
        Pricing pricing,
        List<FirmUsage> firms,
        Pagination pagination
) {

    /**
     * Integrator-level metadata.
     *
     * @param id                   integrator UUID
     * @param name                 integrator display name
     * @param plan                 plan code (e.g. {@code "integrator"})
     * @param monthlyDocumentLimit hard monthly cap, or {@code null} for unlimited
     */
    public record Integrator(
            String id,
            String name,
            String plan,
            Integer monthlyDocumentLimit
    ) {}

    /**
     * Aggregate over firms on the {@code integrator-managed} plan
     * (the integrator pays for these).
     *
     * @param managedFirms     count of firms on the {@code integrator-managed} plan
     * @param outboundCount    aggregate outbound document count for the period
     * @param inboundApiCount  aggregate inbound API document count for the period
     * @param outboundCharge   tier rates applied to the AGGREGATE outboundCount
     * @param inboundApiCharge tier rates applied to the AGGREGATE inboundApiCount
     * @param totalCharge      sum of outboundCharge + inboundApiCharge, cents-rounded
     * @param currency         always {@code "EUR"}
     */
    public record Billable(
            int managedFirms,
            int outboundCount,
            int inboundApiCount,
            double outboundCharge,
            double inboundApiCharge,
            double totalCharge,
            String currency
    ) {}

    /**
     * Linked firms that pay their own plan (not billed to the integrator).
     *
     * @param firms           number of non-managed firms
     * @param outboundCount   their aggregate outbound count
     * @param inboundApiCount their aggregate inbound API count
     */
    public record NonManaged(
            int firms,
            int outboundCount,
            int inboundApiCount
    ) {}

    /**
     * Pricing table - separate tiers for outbound and inbound API.
     *
     * @param model           always {@code "tiered"}
     * @param currency        always {@code "EUR"}
     * @param outboundTiers   outbound tier table
     * @param inboundApiTiers inbound API tier table
     */
    public record Pricing(
            String model,
            String currency,
            List<Tier> outboundTiers,
            List<Tier> inboundApiTiers
    ) {}

    /**
     * One tier in the pricing table. The last entry has {@code upTo} and
     * {@code rate} both {@code null} (open-ended top tier) and
     * {@code contactRequired = true}.
     *
     * @param upTo            inclusive upper bound, or {@code null} on the open tier
     * @param rate            per-document rate in EUR, or {@code null} on the open tier
     * @param label           label for the open tier (e.g. {@code "Individuálne"}); {@code null} otherwise
     * @param contactRequired {@code true} on the open tier
     */
    public record Tier(
            Integer upTo,
            Double rate,
            String label,
            Boolean contactRequired
    ) {}

    /**
     * Per-firm row in the {@code firms} page.
     *
     * @param firmId          firm UUID
     * @param name            firm display name, or {@code null}
     * @param ico             Slovak ICO, or {@code null}
     * @param managed         {@code true} -&gt; counts in {@link Billable}; {@code false} -&gt; counts in {@link NonManaged}
     * @param outboundCount   firm's outbound count for the period
     * @param inboundApiCount firm's inbound API count for the period
     */
    public record FirmUsage(
            String firmId,
            String name,
            String ico,
            boolean managed,
            int outboundCount,
            int inboundApiCount
    ) {}

    /**
     * Pagination envelope for the {@code firms} list.
     *
     * @param limit  page size used for this response
     * @param offset offset used for this response
     * @param total  total firm count for the integrator
     */
    public record Pagination(
            int limit,
            int offset,
            int total
    ) {}
}
