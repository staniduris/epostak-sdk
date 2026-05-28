package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;
import java.util.Map;

/**
 * Paginated FS SR report submission history.
 *
 * @param items  report submission rows
 * @param total  total matching rows
 * @param limit  page size
 * @param offset pagination offset
 */
public record ReportingSubmissionsResponse(
        List<Item> items,
        int total,
        int limit,
        int offset
) {
    /**
     * One EUSR/TSR report submitted to FS SR by ePošťák as AP operator.
     */
    public record Item(
            String id,
            @SerializedName("report_type") String reportType,
            Map<String, String> period,
            String status,
            @SerializedName("message_id") String messageId,
            @SerializedName("submitted_at") String submittedAt,
            @SerializedName("has_error") boolean hasError
    ) {}
}
