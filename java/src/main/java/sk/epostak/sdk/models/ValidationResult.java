package sk.epostak.sdk.models;

import java.util.List;

/**
 * Result of document validation (dry-run without sending).
 *
 * @param valid    {@code true} if the document passes all validation rules
 * @param warnings list of validation warning messages (may be empty)
 * @param ubl      generated UBL XML, only present for JSON mode requests; {@code null} for XML mode
 */
public record ValidationResult(
        boolean valid,
        List<String> warnings,
        String ubl
) {}
