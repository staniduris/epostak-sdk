package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * Result of AI-powered extraction from a single document (PDF or image).
 *
 * @param extraction       structured extraction data as a map (invoice fields, parties,
 *                         line items, etc.)
 * @param direction        {@code "inbound"} returns generated UBL; {@code "outbound"}
 *                         returns a reviewable send payload when supported
 * @param documentType     resolved document type, e.g. {@code "invoice"} or
 *                         {@code "self_billing"}
 * @param sendPayload      draft JSON body for {@code POST /documents/send}
 * @param sendPayloadMissingFields fields the caller must fill before posting
 *                         {@code sendPayload}
 * @param sendReady        {@code true} when {@code sendPayload} has the blocking
 *                         fields needed by {@code /documents/send}
 * @param ublXml           generated UBL XML from the extracted data
 * @param confidence       overall extraction confidence bucket:
 *                         {@code "high"}, {@code "medium"}, or {@code "low"}
 * @param confidenceScores numeric confidence per logical block (e.g. {@code "header"},
 *                         {@code "line_items"}, {@code "totals"}); each value is
 *                         a float in {@code [0.0, 1.0]}
 * @param needsReview      {@code true} when the overall confidence is {@code "low"} or
 *                         {@code "medium"} — the integrator SHOULD surface the
 *                         extracted data to a human before auto-processing
 * @param missingFields    review checklist for missing or low-confidence values
 * @param fieldSources     provenance map for extracted/enriched fields
 * @param nextAction       recommended next API action
 * @param fileName         the original file name that was processed
 */
public record ExtractResult(
        Map<String, Object> extraction,
        String direction,
        @SerializedName("document_type") String documentType,
        @SerializedName("send_payload") Map<String, Object> sendPayload,
        @SerializedName("send_payload_missing_fields") List<String> sendPayloadMissingFields,
        @SerializedName("send_ready") Boolean sendReady,
        @SerializedName("ubl_xml") String ublXml,
        String confidence,
        @SerializedName("confidence_scores") Map<String, Double> confidenceScores,
        @SerializedName("needs_review") boolean needsReview,
        @SerializedName("missing_fields") List<ExtractMissingField> missingFields,
        @SerializedName("field_sources") Map<String, ExtractFieldSource> fieldSources,
        @SerializedName("next_action") ExtractNextAction nextAction,
        @SerializedName("file_name") String fileName
) {
    public ExtractResult(
            Map<String, Object> extraction,
            String ublXml,
            String confidence,
            Map<String, Double> confidenceScores,
            boolean needsReview,
            String fileName
    ) {
        this(
                extraction,
                null,
                null,
                null,
                null,
                null,
                ublXml,
                confidence,
                confidenceScores,
                needsReview,
                null,
                null,
                null,
                fileName
        );
    }
}
