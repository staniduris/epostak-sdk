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
 * Statistics stats = client.reporting().statistics("month");
 * System.out.println("Sent: " + stats.sent().total());
 * System.out.println("Received: " + stats.received().total());
 * System.out.println("Delivery rate: " + stats.deliveryRate());
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
     * Get aggregated document statistics using a convenience period selector.
     *
     * @param period one of {@code "month"} (default), {@code "quarter"}, or {@code "year"}; {@code null} for the server default
     * @return the statistics for the selected period
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Statistics statistics(String period) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("period", period);
        return http.get("/reporting/statistics" + HttpClient.buildQuery(params), Statistics.class);
    }

    /**
     * Get aggregated document statistics for an explicit date range. When
     * both {@code from} and {@code to} are present they take precedence over
     * any {@code period} the server might infer.
     *
     * @param from start date in ISO 8601 format (e.g. {@code "2026-01-01"}), or {@code null}
     * @param to   end date in ISO 8601 format (e.g. {@code "2026-03-31"}), or {@code null}
     * @return the statistics for the selected window
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Statistics statisticsRange(String from, String to) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("from", from);
        params.put("to", to);
        return http.get("/reporting/statistics" + HttpClient.buildQuery(params), Statistics.class);
    }

    /**
     * Get aggregated document statistics combining a period with optional
     * date overrides. Useful when callers want to keep a {@code period}
     * literal but override one boundary.
     *
     * @param period one of {@code "month"}, {@code "quarter"}, {@code "year"}, or {@code null}
     * @param from   ISO 8601 start date override, or {@code null}
     * @param to     ISO 8601 end date override, or {@code null}
     * @return the statistics for the resolved window
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Statistics statistics(String period, String from, String to) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("period", period);
        params.put("from", from);
        params.put("to", to);
        return http.get("/reporting/statistics" + HttpClient.buildQuery(params), Statistics.class);
    }

    /**
     * Get aggregated document statistics with default parameters
     * (server defaults to the current calendar month).
     *
     * @return the statistics for the default period
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Statistics statistics() {
        return statistics(null, null, null);
    }
}
