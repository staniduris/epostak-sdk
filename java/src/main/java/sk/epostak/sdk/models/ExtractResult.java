package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.Map;

/**
 * Result of AI-powered extraction from a single document (PDF or image).
 *
 * @param extraction structured extraction data as a map (invoice fields, parties, line items, etc.)
 * @param ublXml     generated UBL XML from the extracted data
 * @param confidence extraction confidence score (0.0 to 1.0)
 * @param fileName   the original file name that was processed
 */
public record ExtractResult(
        Map<String, Object> extraction,
        @SerializedName("ubl_xml") String ublXml,
        double confidence,
        @SerializedName("file_name") String fileName
) {}
