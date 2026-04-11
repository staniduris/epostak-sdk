package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.Statistics;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Document statistics and reports.
 * <p>
 * Provides aggregated counts of inbound and outbound documents for a given
 * time period.
 * <p>
 * Access via {@code client.reporting()}.
 *
 * <pre>{@code
 * Statistics stats = client.reporting().statistics("2026-01-01", "2026-03-31");
 * System.out.println("Sent: " + stats.outbound().total());
 * System.out.println("Received: " + stats.inbound().total());
 * }</pre>
 */
public final class ReportingResource {

    private final HttpClient http;

    /**
     * Creates a new reporting resource.
     *
     * @param http the HTTP client used for API communication
     */
    public ReportingResource(HttpClient http) {
        this.http = http;
    }

    /**
     * Get aggregated document statistics for a date range.
     *
     * @param from start date in ISO 8601 format (e.g. {@code "2026-01-01"}), or {@code null} for all time
     * @param to   end date in ISO 8601 format (e.g. {@code "2026-03-31"}), or {@code null} for all time
     * @return the statistics with outbound and inbound document counts
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Statistics statistics(String from, String to) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("from", from);
        params.put("to", to);
        return http.get("/reporting/statistics" + HttpClient.buildQuery(params), Statistics.class);
    }

    /**
     * Get aggregated document statistics for all time.
     *
     * @return the statistics with outbound and inbound document counts
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Statistics statistics() {
        return statistics(null, null);
    }
}
