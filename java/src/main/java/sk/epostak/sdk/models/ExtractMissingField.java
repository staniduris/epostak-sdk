package sk.epostak.sdk.models;

/**
 * Field the integrator must review or complete before sending.
 *
 * @param field    machine-readable field key, for example {@code receiverPeppolId}
 * @param label    human-readable label for review UIs
 * @param bt       optional EN 16931 business-term identifier
 * @param required whether the API requires the value before the next action
 * @param severity {@code blocking} or {@code review}
 * @param reason machine-readable reason why the value needs attention
 * @param howToFix human-readable correction guidance
 * @param acceptedFormat expected value format, when available
 * @param message  legacy explanation of why the value is needed
 * @param blocking {@code true} when the field blocks {@code /documents/send}
 * @param value    current value, when a partial value exists
 */
public record ExtractMissingField(
        String field,
        String label,
        String bt,
        Boolean required,
        String severity,
        String reason,
        @com.google.gson.annotations.SerializedName("how_to_fix") String howToFix,
        @com.google.gson.annotations.SerializedName("accepted_format") String acceptedFormat,
        String message,
        Boolean blocking,
        Object value
) {
    public ExtractMissingField(
            String field,
            String label,
            String message,
            Boolean blocking,
            Object value
    ) {
        this(field, label, null, null, null, null, null, null, message, blocking, value);
    }
}
