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
     * @param extraction structured extraction data, or {@code null} on error
     * @param ublXml     generated UBL XML, or {@code null} on error
     * @param confidence extraction confidence score, or {@code null} on error
     * @param error      error message if extraction failed, or {@code null} on success
     */
    public record BatchExtractItem(
            @SerializedName("file_name") String fileName,
            Map<String, Object> extraction,
            @SerializedName("ubl_xml") String ublXml,
            String confidence,
            String error
    ) {}
}
