package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;
import java.util.Map;

/**
 * Result of batch document extraction (multiple files in one request).
 *
 * @param batchId    unique batch identifier
 * @param total      total number of files submitted
 * @param successful number of successfully extracted files
 * @param failed     number of files that failed extraction
 * @param results    per-file extraction results
 */
public record BatchExtractResult(
        @SerializedName("batch_id") String batchId,
        int total,
        int successful,
        int failed,
        List<BatchExtractItem> results
) {
    /**
     * Extraction result for a single file in a batch.
     *
     * @param fileName   the original file name
     * @param direction  {@code "inbound"} or {@code "outbound"}
     * @param documentType resolved document type, e.g. {@code "invoice"} or
     *                   {@code "self_billing"}
     * @param sendPayload draft JSON body for {@code POST /documents/send}
     * @param sendPayloadMissingFields fields the caller must fill before posting
     *                   {@code sendPayload}
     * @param sendReady  {@code true} when {@code sendPayload} has the blocking
     *                   fields needed by {@code /documents/send}
     * @param extraction structured extraction data, or {@code null} on error
     * @param ublXml     generated UBL XML, or {@code null} on error
     * @param confidence extraction confidence score, or {@code null} on error
     * @param confidenceScores numeric confidence per logical block
     * @param needsReview {@code true} when a human should review this extraction
     * @param missingFields review checklist for missing or low-confidence values
     * @param fieldSources provenance map for extracted/enriched fields
     * @param nextAction  recommended next API action
     * @param error      error message if extraction failed, or {@code null} on success
     */
    public record BatchExtractItem(
            @SerializedName("file_name") String fileName,
            String direction,
            @SerializedName("document_type") String documentType,
            @SerializedName("send_payload") Map<String, Object> sendPayload,
            @SerializedName("send_payload_missing_fields") List<String> sendPayloadMissingFields,
            @SerializedName("send_ready") Boolean sendReady,
            Map<String, Object> extraction,
            @SerializedName("ubl_xml") String ublXml,
            String confidence,
            @SerializedName("confidence_scores") Map<String, Double> confidenceScores,
            @SerializedName("needs_review") Boolean needsReview,
            @SerializedName("missing_fields") List<ExtractMissingField> missingFields,
            @SerializedName("field_sources") Map<String, ExtractFieldSource> fieldSources,
            @SerializedName("next_action") ExtractNextAction nextAction,
            String error
    ) {
        public BatchExtractItem(
                String fileName,
                Map<String, Object> extraction,
                String ublXml,
                String confidence,
                String error
        ) {
            this(
                    fileName,
                    null,
                    null,
                    null,
                    null,
                    null,
                    extraction,
                    ublXml,
                    confidence,
                    null,
                    null,
                    null,
                    null,
                    null,
                    error
            );
        }
    }
}
