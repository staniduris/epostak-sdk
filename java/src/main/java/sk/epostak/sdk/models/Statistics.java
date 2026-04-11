package sk.epostak.sdk.models;

/**
 * Aggregated document statistics for a time period.
 *
 * @param period   the date range these statistics cover
 * @param outbound outbound (sent) document statistics
 * @param inbound  inbound (received) document statistics
 */
public record Statistics(
        Period period,
        OutboundStats outbound,
        InboundStats inbound
) {
    /**
     * The date range for the statistics.
     *
     * @param from start date in ISO 8601 format (YYYY-MM-DD), or {@code null} for all time
     * @param to   end date in ISO 8601 format (YYYY-MM-DD), or {@code null} for all time
     */
    public record Period(String from, String to) {}

    /**
     * Outbound (sent) document statistics.
     *
     * @param total     total number of outbound documents
     * @param delivered number of successfully delivered documents
     * @param failed    number of failed delivery attempts
     */
    public record OutboundStats(int total, int delivered, int failed) {}

    /**
     * Inbound (received) document statistics.
     *
     * @param total        total number of inbound documents
     * @param acknowledged number of acknowledged (processed) documents
     * @param pending      number of documents pending acknowledgement
     */
    public record InboundStats(int total, int acknowledged, int pending) {}
}
