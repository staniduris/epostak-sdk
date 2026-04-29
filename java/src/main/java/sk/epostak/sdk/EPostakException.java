package sk.epostak.sdk;

/**
 * Exception thrown when an ePosťak API request fails.
 * <p>
 * Contains the HTTP status code, a human-readable message, an optional
 * machine-readable error code, and optional structured error details. The SDK
 * also normalises the RFC 7807 ({@code application/problem+json}) envelope —
 * {@link #getType()}, {@link #getTitle()}, {@link #getDetail()},
 * {@link #getInstance()} are populated when the server returns one.
 * <p>
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
 *     if (e.getRequestId() != null) {
 *         System.err.println("Request ID: " + e.getRequestId());
 *     }
 *     if (e.getRequiredScope() != null) {
 *         System.err.println("Missing scope: " + e.getRequiredScope());
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

    // RFC 7807 (application/problem+json) fields — populated when the server
    // returns a problem+json envelope. Otherwise null.
    private final String type;
    private final String title;
    private final String detail;
    private final String instance;

    /** Server-assigned request id (from response body or {@code X-Request-Id}). */
    private final String requestId;
    /**
     * Required OAuth scope when the server rejects with {@code 403 insufficient_scope}.
     * Parsed from the {@code WWW-Authenticate: Bearer error="insufficient_scope"
     * scope="..."} header, or from the body's {@code required_scope} field. {@code null}
     * when the header is absent or the rejection was for a different reason.
     */
    private final String requiredScope;

    /**
     * Creates a new exception with full v2 error information.
     *
     * @param status        HTTP status code, or {@code 0} for network errors
     * @param message       human-readable error message
     * @param code          machine-readable error code from the API, or {@code null}
     * @param details       additional error details (validation messages, etc.), or {@code null}
     * @param type          RFC 7807 {@code type}, or {@code null}
     * @param title         RFC 7807 {@code title}, or {@code null}
     * @param detail        RFC 7807 {@code detail}, or {@code null}
     * @param instance      RFC 7807 {@code instance}, or {@code null}
     * @param requestId     server-assigned request id, or {@code null}
     * @param requiredScope OAuth scope required for the call, or {@code null}
     */
    public EPostakException(
            int status,
            String message,
            String code,
            Object details,
            String type,
            String title,
            String detail,
            String instance,
            String requestId,
            String requiredScope
    ) {
        super(message);
        this.status = status;
        this.code = code;
        this.details = details;
        this.type = type;
        this.title = title;
        this.detail = detail;
        this.instance = instance;
        this.requestId = requestId;
        this.requiredScope = requiredScope;
    }

    /**
     * Creates a new exception with status, message, code, and details only.
     *
     * @param status  HTTP status code, or {@code 0} for network errors
     * @param message human-readable error message
     * @param code    machine-readable error code from the API, or {@code null}
     * @param details additional error details (validation messages, etc.), or {@code null}
     */
    public EPostakException(int status, String message, String code, Object details) {
        this(status, message, code, details, null, null, null, null, null, null);
    }

    /**
     * Creates a new exception with status and message only.
     *
     * @param status  HTTP status code, or {@code 0} for network errors
     * @param message human-readable error message
     */
    public EPostakException(int status, String message) {
        this(status, message, null, null, null, null, null, null, null, null);
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

    /**
     * RFC 7807 {@code type} — URI reference identifying the problem type, or {@code null}.
     *
     * @return the problem type URI, or {@code null}
     */
    public String getType() {
        return type;
    }

    /**
     * RFC 7807 {@code title} — short, human-readable summary, or {@code null}.
     *
     * @return the problem title, or {@code null}
     */
    public String getTitle() {
        return title;
    }

    /**
     * RFC 7807 {@code detail} — explanation of this specific occurrence, or {@code null}.
     *
     * @return the problem detail, or {@code null}
     */
    public String getDetail() {
        return detail;
    }

    /**
     * RFC 7807 {@code instance} — URI reference identifying this specific occurrence, or {@code null}.
     *
     * @return the problem instance URI, or {@code null}
     */
    public String getInstance() {
        return instance;
    }

    /**
     * Server-assigned request id, captured from the body or the
     * {@code X-Request-Id} response header.
     *
     * @return the request id, or {@code null} when neither was set
     */
    public String getRequestId() {
        return requestId;
    }

    /**
     * Required OAuth scope when the server rejects with {@code 403 insufficient_scope}.
     *
     * @return the required scope, or {@code null}
     */
    public String getRequiredScope() {
        return requiredScope;
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
        if (requestId != null) {
            sb.append(", requestId='").append(requestId).append('\'');
        }
        if (requiredScope != null) {
            sb.append(", requiredScope='").append(requiredScope).append('\'');
        }
        sb.append('}');
        return sb.toString();
    }
}
