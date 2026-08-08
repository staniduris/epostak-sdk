package sk.epostak.sdk.models;

import java.util.List;

/**
 * Recommended next action after OCR extraction.
 *
 * @param type     machine-readable action type
 * @param label    human-readable action label
 * @param message  longer review/send instruction
 * @param endpoint API endpoint to call next, when applicable
 * @param method   HTTP method for {@code endpoint}, when applicable
 * @param fields   values to complete or review before taking the action
 */
public record ExtractNextAction(
        String type,
        String label,
        String message,
        String endpoint,
        String method,
        List<String> fields
) {
    public ExtractNextAction(
            String type,
            String label,
            String message,
            String endpoint,
            String method
    ) {
        this(type, label, message, endpoint, method, null);
    }
}
