package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.Map;

/**
 * Result of AI-powered extraction from a single document (PDF or image).
 *
 * @param extraction       structured extraction data as a map (invoice fields, parties,
 *                         line items, etc.)
 * @param ublXml           generated UBL XML from the extracted data
 * @param confidence       overall extraction confidence bucket:
 *                         {@code "high"}, {@code "medium"}, or {@code "low"}
 * @param confidenceScores numeric confidence per logical block (e.g. {@code "header"},
 *                         {@code "line_items"}, {@code "totals"}); each value is
 *                         a float in {@code [0.0, 1.0]}
 * @param needsReview      {@code true} when the overall confidence is {@code "low"} or
 *                         {@code "medium"} — the integrator SHOULD surface the
 *                         extracted data to a human before auto-processing
 * @param fileName         the original file name that was processed
 */
public record ExtractResult(
        Map<String, Object> extraction,
        @SerializedName("ubl_xml") String ublXml,
        String confidence,
        @SerializedName("confidence_scores") Map<String, Double> confidenceScores,
        @SerializedName("needs_review") boolean needsReview,
        @SerializedName("file_name") String fileName
) {}
