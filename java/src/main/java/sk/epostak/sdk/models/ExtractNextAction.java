package sk.epostak.sdk.models;

/**
 * Recommended next action after OCR extraction.
 *
 * @param type     machine-readable action type
 * @param label    human-readable action label
 * @param message  longer review/send instruction
 * @param endpoint API endpoint to call next, when applicable
 * @param method   HTTP method for {@code endpoint}, when applicable
 */
public record ExtractNextAction(
        String type,
        String label,
        String message,
        String endpoint,
        String method
) {}
