package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from {@code POST /documents/validate}, returned with HTTP 200 when
 * {@code valid == true} and HTTP 422 when {@code valid == false}.
 *
 * @param valid        {@code true} when the document passed every Peppol BIS validation layer
 * @param errorCount   total number of error-level findings (zero when {@code valid == true})
 * @param warningCount total number of warning-level findings
 * @param errors       flat list of error-level findings
 * @param warnings     flat list of warning-level findings
 */
public record ValidationResult(
        boolean valid,
        int errorCount,
        int warningCount,
        List<ValidationReport.Finding> errors,
        List<ValidationReport.Finding> warnings
) {}
