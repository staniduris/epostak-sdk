package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.IntegratorLicenseInfo;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Integrator-aggregate endpoints. Reachable only with an {@code sk_int_*}
 * key; per-firm endpoints stay under their existing resources.
 * <p>
 * Aggregate semantics: tier rates apply to the AGGREGATE document count
 * across the integrator's {@code integrator-managed} firms, not per-firm.
 * A 100-firm x 50-doc integrator lands in tier 2-3, not tier 1 like a
 * standalone firm would.
 * <p>
 * Access via {@code client.integrator()}.
 *
 * <pre>{@code
 * EPostak client = EPostak.builder().clientId("sk_int_xxxxx").clientSecret("sk_int_xxxxx").build();
 *
 * IntegratorLicenseInfo info = client.integrator().licenses().info();
 * System.out.println("Managed firms: " + info.billable().managedFirms());
 * System.out.println("Outbound charge: " + info.billable().outboundCharge() + " EUR");
 * if (info.exceedsAutoTier()) {
 *     System.out.println("Manual review - sales handles invoicing");
 * }
 * }</pre>
 */
public final class IntegratorResource {

    private final IntegratorLicensesResource licenses;

    /**
     * Creates a new integrator resource.
     *
     * @param http the HTTP client used for API communication
     */
    public IntegratorResource(HttpClient http) {
        this.licenses = new IntegratorLicensesResource(http);
    }

    /**
     * Sub-resource for {@code /integrator/licenses/*} endpoints.
     *
     * @return the integrator licenses resource
     */
    public IntegratorLicensesResource licenses() {
        return licenses;
    }

    /**
     * Sub-resource for {@code GET /api/v1/integrator/licenses/info}.
     * <p>
     * Returns aggregate plan + current-period usage across every firm an
     * integrator manages, with a paginated per-firm breakdown.
     */
    public static final class IntegratorLicensesResource {

        private final HttpClient http;

        IntegratorLicensesResource(HttpClient http) {
            this.http = http;
        }

        /**
         * Get aggregate plan + current-period usage across every firm the
         * integrator manages, plus a per-firm breakdown for the first page
         * of firms (sorted by outbound count, descending).
         * <p>
         * Tier rates are applied to the AGGREGATE document count, not
         * per-firm. Above {@code 5000} outbound or inbound documents per
         * month, {@code exceedsAutoTier} flips to {@code true}, auto-billing
         * pauses, and sales handles invoicing manually.
         * <p>
         * Requires an {@code sk_int_*} integrator key with the
         * {@code account:read} scope. The endpoint is integrator-scoped, so
         * no {@code X-Firm-Id} header is sent.
         *
         * @return the integrator license info response
         * @throws sk.epostak.sdk.EPostakException if the request fails
         */
        public IntegratorLicenseInfo info() {
            return info(null, null);
        }

        /**
         * Get aggregate plan + current-period usage with pagination of the
         * per-firm breakdown.
         *
         * @param offset zero-based offset into the firms page (default {@code 0})
         * @param limit  page size for the firms breakdown (default {@code 50}, max {@code 100})
         * @return the integrator license info response
         * @throws sk.epostak.sdk.EPostakException if the request fails
         */
        public IntegratorLicenseInfo info(Integer offset, Integer limit) {
            Map<String, Object> params = new LinkedHashMap<>();
            if (offset != null) params.put("offset", offset);
            if (limit != null) params.put("limit", limit);
            return http.get("/integrator/licenses/info" + HttpClient.buildQuery(params), IntegratorLicenseInfo.class);
        }
    }
}
