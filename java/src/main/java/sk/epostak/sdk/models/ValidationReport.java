package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Full 3-layer Peppol BIS 3.0 validation report returned by {@code POST /api/validate}.
 * <p>
 * Reports findings from each validation layer: UBL XSD schema, EN 16931 core
 * business rules, and Peppol BIS 3.0 country-specific rules. Each layer can
 * independently pass, warn, or fail.
 *
 * @param valid        {@code true} if every layer passes (no {@code errors})
 * @param schematronOk {@code true} if all Schematron (EN + Peppol) layers pass
 * @param summary      aggregate counts of errors and warnings across all layers
 * @param layers       per-layer findings
 * @param errors       flat list of all error-level findings
 * @param warnings     flat list of all warning-level findings
 */
public record ValidationReport(
        boolean valid,
        @SerializedName("schematron_ok") boolean schematronOk,
        Summary summary,
        Layers layers,
        List<Finding> errors,
        List<Finding> warnings
) {
    /**
     * Aggregate error and warning counts for the full report.
     *
     * @param errors   total number of error-level findings
     * @param warnings total number of warning-level findings
     */
    public record Summary(
            int errors,
            int warnings
    ) {}

    /**
     * Per-layer validation results.
     *
     * @param xsd    UBL 2.1 XSD schema validation layer
     * @param en     EN 16931 core business rules layer
     * @param peppol Peppol BIS 3.0 country-specific rules layer
     */
    public record Layers(
            LayerResult xsd,
            LayerResult en,
            LayerResult peppol
    ) {}

    /**
     * Result of a single validation layer.
     *
     * @param ok       {@code true} if this layer produced no errors
     * @param errors   layer-specific error findings
     * @param warnings layer-specific warning findings
     */
    public record LayerResult(
            boolean ok,
            List<Finding> errors,
            List<Finding> warnings
    ) {}

    /**
     * A single validation finding (error or warning).
     *
     * @param rule     the rule identifier (e.g. {@code "BR-01"}, {@code "SK-R-002"})
     * @param message  human-readable description of the finding
     * @param location XPath or schema location where the finding was raised, or {@code null}
     * @param severity severity level: {@code "error"} or {@code "warning"}
     */
    public record Finding(
            String rule,
            String message,
            String location,
            String severity
    ) {}
}
