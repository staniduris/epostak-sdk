package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;
import java.util.Map;

/**
 * Aggregated document statistics for a time period.
 *
 * @param period        the date range these statistics cover
 * @param sent          sent document counts (total + per-type breakdown)
 * @param received      received document counts (total + per-type breakdown)
 * @param deliveryRate  fraction of sent documents that were confirmed delivered, in {@code [0.0, 1.0]}
 * @param topRecipients top-N recipients the firm sends to (most recent window)
 * @param topSenders    top-N senders the firm receives from
 */
public record Statistics(
        Period period,
        @SerializedName("sent") DirectionStats sent,
        @SerializedName("received") DirectionStats received,
        @SerializedName("delivery_rate") double deliveryRate,
        @SerializedName("top_recipients") List<PartyCount> topRecipients,
        @SerializedName("top_senders") List<PartyCount> topSenders
) {
    /**
     * The date range for the statistics.
     *
     * @param from start date in ISO 8601 format (YYYY-MM-DD), or {@code null} for all time
     * @param to   end date in ISO 8601 format (YYYY-MM-DD), or {@code null} for all time
     */
    public record Period(String from, String to) {}

    /**
     * Aggregate document counts in one direction.
     *
     * @param total  total number of documents in this direction
     * @param byType per-doctype breakdown (e.g. {@code invoice -> 42})
     */
    public record DirectionStats(
            int total,
            @SerializedName("by_type") Map<String, Integer> byType
    ) {}

    /**
     * One row of a top-N counterparty ranking.
     *
     * @param name     counterparty legal name, or {@code null}
     * @param peppolId counterparty Peppol participant ID, or {@code null}
     * @param count    number of documents exchanged with this counterparty
     */
    public record PartyCount(
            String name,
            @SerializedName("peppol_id") String peppolId,
            int count
    ) {}
}
