package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Result of a JSON/UBL conversion.
 *
 * @param outputFormat the format of the returned document: {@code "ubl"} or {@code "json"}
 * @param document     a UBL XML string when {@code outputFormat == "ubl"}, or a parsed JSON
 *                     object when {@code outputFormat == "json"}
 * @param warnings     non-fatal warnings emitted during conversion, or an empty list
 */
public record ConvertResult(
        @SerializedName("output_format") String outputFormat,
        @SerializedName("document") Object document,
        @SerializedName("warnings") List<String> warnings
) {}
