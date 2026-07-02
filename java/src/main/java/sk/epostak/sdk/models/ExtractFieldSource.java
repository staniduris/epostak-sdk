package sk.epostak.sdk.models;

/**
 * Provenance for a value returned by OCR or enrichment.
 *
 * @param source     source identifier, for example {@code ocr}, {@code firm_profile}, or {@code peppol_directory}
 * @param value      resolved value from this source
 * @param confidence optional source confidence score in {@code [0.0, 1.0]}
 */
public record ExtractFieldSource(
        String source,
        Object value,
        Double confidence
) {}
