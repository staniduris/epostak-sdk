package sk.epostak.sdk.models;

/**
 * Field the integrator must review or complete before sending.
 *
 * @param field    machine-readable field key, for example {@code receiverPeppolId}
 * @param label    human-readable label for review UIs
 * @param message  explanation of why the value is needed
 * @param blocking {@code true} when the field blocks {@code /documents/send}
 * @param value    current value, when a partial value exists
 */
public record ExtractMissingField(
        String field,
        String label,
        String message,
        Boolean blocking,
        Object value
) {}
