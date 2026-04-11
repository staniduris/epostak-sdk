package sk.epostak.sdk.models;

/**
 * Result of a JSON/UBL conversion.
 *
 * @param direction the conversion direction: {@code "json_to_ubl"} or {@code "ubl_to_json"}
 * @param result    a UBL XML string for {@code json_to_ubl}, or a parsed JSON object for {@code ubl_to_json}
 */
public record ConvertResult(
        String direction,
        Object result
) {}
