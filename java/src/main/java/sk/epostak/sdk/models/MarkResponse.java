package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from marking a document's processing state.
 *
 * @param id             the document UUID that was marked
 * @param state          the state that was recorded
 * @param status         the document's overall status after applying the mark
 * @param deliveredAt    ISO 8601 timestamp of the {@code delivered} mark, or {@code null}
 * @param acknowledgedAt ISO 8601 timestamp of the {@code processed} mark, or {@code null}
 * @param readAt         ISO 8601 timestamp of the {@code read} mark, or {@code null}
 */
public record MarkResponse(
        String id,
        String state,
        String status,
        @SerializedName("delivered_at") String deliveredAt,
        @SerializedName("acknowledged_at") String acknowledgedAt,
        @SerializedName("read_at") String readAt
) {}
