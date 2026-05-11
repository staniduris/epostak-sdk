package sk.epostak.sdk;

/**
 * Thrown when the API returns {@code 422} with
 * {@code error.code = "UBL_VALIDATION_ERROR"}.
 * <p>
 * The {@link #getRule()} method returns the first failing Peppol BIS 3.0 or
 * EN 16931 rule identifier. The human-readable error is in
 * {@link EPostakException#getMessage()}.
 *
 * <pre>{@code
 * try {
 *     client.documents().send(request);
 * } catch (UblValidationException e) {
 *     System.err.println("UBL rule violation: " + e.getRule());
 *     System.err.println("Message: " + e.getMessage());
 *     if (e.getRequestId() != null) {
 *         System.err.println("Request-Id: " + e.getRequestId());
 *     }
 * } catch (EPostakException e) {
 *     // other API errors
 * }
 * }</pre>
 */
public class UblValidationException extends EPostakException {

    /**
     * The rule identifier from the first validation failure, or {@code null}
     * if the response did not include one.
     */
    private final String rule;

    /**
     * Creates a new exception with all fields.
     *
     * @param status        HTTP status code (usually 422)
     * @param message       human-readable error message
     * @param code          machine-readable error code ({@code "UBL_VALIDATION_ERROR"})
     * @param details       additional structured error details, or {@code null}
     * @param type          RFC 7807 type, or {@code null}
     * @param title         RFC 7807 title, or {@code null}
     * @param detail        RFC 7807 detail, or {@code null}
     * @param instance      RFC 7807 instance, or {@code null}
     * @param requestId     server-assigned request id, or {@code null}
     * @param requiredScope OAuth scope required, or {@code null}
     * @param rule          first failing rule identifier (e.g. {@code "BR_02"}), or {@code null}
     */
    public UblValidationException(
            int status,
            String message,
            String code,
            Object details,
            String type,
            String title,
            String detail,
            String instance,
            String requestId,
            String requiredScope,
            String rule
    ) {
        super(status, message, code, details, type, title, detail, instance, requestId, requiredScope);
        this.rule = rule;
    }

    /**
     * Creates a new exception with status, message, requestId, and rule.
     * Convenience constructor for the common case.
     *
     * @param status    HTTP status code
     * @param message   human-readable error message
     * @param requestId server-assigned request id, or {@code null}
     * @param rule      first failing rule identifier, or {@code null}
     */
    public UblValidationException(int status, String message, String requestId, String rule) {
        this(status, message, "UBL_VALIDATION_ERROR", null, null, null, null, null, requestId, null, rule);
    }

    /**
     * Returns the first failing Peppol BIS 3.0 / EN 16931 rule identifier,
     * e.g. {@code "BR_02"}, {@code "PEPPOL_R008"}. May be {@code null} if the
     * server response did not include a rule field.
     *
     * @return the rule identifier, or {@code null}
     * @see UblRule
     */
    public String getRule() {
        return rule;
    }

    @Override
    public String toString() {
        StringBuilder sb = new StringBuilder("UblValidationException{status=");
        sb.append(getStatus());
        if (rule != null) sb.append(", rule='").append(rule).append('\'');
        if (getRequestId() != null) sb.append(", requestId='").append(getRequestId()).append('\'');
        sb.append(", message='").append(getMessage()).append('\'');
        sb.append('}');
        return sb.toString();
    }
}
