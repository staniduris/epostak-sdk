package sk.epostak.sdk;

/**
 * Exception thrown when an ePosťak API request fails.
 * <p>
 * Contains the HTTP status code, a human-readable message, an optional
 * machine-readable error code, and optional structured error details.
 * For network errors (timeout, DNS failure, etc.), the status code is {@code 0}.
 *
 * <pre>{@code
 * try {
 *     client.documents().get("invalid-id");
 * } catch (EPostakException e) {
 *     System.err.println("HTTP " + e.getStatus() + ": " + e.getMessage());
 *     if (e.getCode() != null) {
 *         System.err.println("Error code: " + e.getCode());
 *     }
 * }
 * }</pre>
 */
public class EPostakException extends RuntimeException {

    /** HTTP status code from the API response, or {@code 0} for network errors. */
    private final int status;
    /** Machine-readable error code from the API, e.g. {@code "VALIDATION_FAILED"}. May be {@code null}. */
    private final String code;
    /** Additional error details such as validation messages. May be {@code null}. */
    private final Object details;

    /**
     * Creates a new exception with full error information.
     *
     * @param status  HTTP status code, or {@code 0} for network errors
     * @param message human-readable error message
     * @param code    machine-readable error code from the API, or {@code null}
     * @param details additional error details (validation messages, etc.), or {@code null}
     */
    public EPostakException(int status, String message, String code, Object details) {
        super(message);
        this.status = status;
        this.code = code;
        this.details = details;
    }

    /**
     * Creates a new exception with status and message only.
     *
     * @param status  HTTP status code, or {@code 0} for network errors
     * @param message human-readable error message
     */
    public EPostakException(int status, String message) {
        this(status, message, null, null);
    }

    /**
     * Returns the HTTP status code from the API response.
     * Returns {@code 0} for network-level errors (timeout, DNS, etc.).
     *
     * @return the HTTP status code
     */
    public int getStatus() {
        return status;
    }

    /**
     * Returns the machine-readable error code from the API, e.g. {@code "VALIDATION_FAILED"}.
     *
     * @return the error code, or {@code null} if not provided
     */
    public String getCode() {
        return code;
    }

    /**
     * Returns additional structured error details, such as per-field validation messages.
     *
     * @return the error details, or {@code null} if not provided
     */
    public Object getDetails() {
        return details;
    }

    @Override
    public String toString() {
        StringBuilder sb = new StringBuilder("EPostakException{status=");
        sb.append(status);
        if (code != null) {
            sb.append(", code='").append(code).append('\'');
        }
        sb.append(", message='").append(getMessage()).append('\'');
        if (details != null) {
            sb.append(", details=").append(details);
        }
        sb.append('}');
        return sb.toString();
    }
}
