package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;
import java.util.Map;

/**
 * Result of parsing a UBL XML invoice into structured JSON via
 * {@code POST /documents/parse}.
 * <p>
 * The parsed shape matches the JSON side of {@code POST /documents/convert}
 * with {@code output_format=json}: {@link #document()} is a free-form map with
 * fields such as {@code invoice_number}, {@code issue_date}, {@code supplier},
 * {@code customer}, {@code items}, and {@code totals}. {@link #warnings()} lists
 * non-fatal issues surfaced during parsing.
 *
 * @param document parsed invoice fields as a generic JSON object
 * @param warnings non-fatal warnings emitted during parsing, or an empty list
 */
public record ParsedInvoice(
        @SerializedName("document") Map<String, Object> document,
        List<String> warnings
) {}
