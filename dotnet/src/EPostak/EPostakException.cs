namespace EPostak;

/// <summary>
/// Exception thrown when an ePošťák API request fails. Contains the HTTP status code,
/// machine-readable error code, RFC 7807 problem-details fields, and any additional
/// details from the API response body. For network-level failures (DNS, timeout,
/// connection refused), <see cref="Status"/> is 0 and the original exception is
/// available via <see cref="Exception.InnerException"/>.
/// </summary>
/// <example>
/// <code>
/// try
/// {
///     await client.Documents.SendAsync(request);
/// }
/// catch (EPostakException ex) when (ex.Status == 422)
/// {
///     Console.WriteLine($"Validation error: {ex.Message}");
///     Console.WriteLine($"Code: {ex.Code}, Details: {ex.Details}");
/// }
/// catch (EPostakException ex) when (ex.Status == 403 &amp;&amp; ex.RequiredScope is not null)
/// {
///     Console.WriteLine($"Missing scope: {ex.RequiredScope}");
/// }
/// catch (EPostakException ex) when (ex.Status == 0)
/// {
///     Console.WriteLine($"Network error: {ex.InnerException?.Message}");
/// }
/// </code>
/// </example>
public class EPostakException : Exception
{
    /// <summary>HTTP status code returned by the API (e.g. 400, 401, 404, 422, 500), or 0 for network-level errors.</summary>
    public int Status { get; }

    /// <summary>Machine-readable error code from the API response (e.g. "VALIDATION_ERROR", "idempotency_conflict", "insufficient_scope"). Null for network errors.</summary>
    public string? Code { get; }

    /// <summary>Additional structured error details from the API (e.g. per-field validation messages, schematron rule IDs). Null when not provided by the API.</summary>
    public object? Details { get; }

    /// <summary>RFC 7807 <c>type</c> — URI reference identifying the problem type.</summary>
    public string? Type { get; }

    /// <summary>RFC 7807 <c>title</c> — short, human-readable summary.</summary>
    public string? Title { get; }

    /// <summary>RFC 7807 <c>detail</c> — human-readable explanation of this specific occurrence.</summary>
    public string? Detail { get; }

    /// <summary>RFC 7807 <c>instance</c> — URI reference identifying this specific occurrence.</summary>
    public string? Instance { get; }

    /// <summary>Server-assigned request identifier — set whenever the server returns <c>X-Request-Id</c> or includes <c>requestId</c> in the body.</summary>
    public string? RequestId { get; }

    /// <summary>Request field that should be corrected, when supplied by the API.</summary>
    public string? Field { get; }

    /// <summary>Remediation hint supplied by the API.</summary>
    public string? NextAction { get; }

    /// <summary>Whether the same business operation can be retried unchanged.</summary>
    public bool? Retryable { get; }

    /// <summary>Delta seconds from the <c>Retry-After</c> response header.</summary>
    public int? RetryAfter { get; }

    /// <summary>
    /// Required OAuth scope when the server rejects with <c>403 insufficient_scope</c>.
    /// Parsed from the <c>WWW-Authenticate: Bearer error="insufficient_scope" scope="..."</c>
    /// header. <c>null</c> when the header is absent or the rejection was for a different reason.
    /// </summary>
    public string? RequiredScope { get; }

    /// <summary>
    /// Create an exception from an API error response.
    /// </summary>
    /// <param name="status">The HTTP status code from the API response.</param>
    /// <param name="message">Human-readable error message from the API.</param>
    /// <param name="code">Machine-readable error code (e.g. "VALIDATION_ERROR").</param>
    /// <param name="details">Additional structured error details.</param>
    /// <param name="type">RFC 7807 <c>type</c> URI.</param>
    /// <param name="title">RFC 7807 <c>title</c>.</param>
    /// <param name="detail">RFC 7807 <c>detail</c>.</param>
    /// <param name="instance">RFC 7807 <c>instance</c> URI.</param>
    /// <param name="requestId">Server-assigned request ID (X-Request-Id).</param>
    /// <param name="requiredScope">Required OAuth scope when the server rejects with 403 insufficient_scope.</param>
    public EPostakException(
        int status,
        string message,
        string? code = null,
        object? details = null,
        string? type = null,
        string? title = null,
        string? detail = null,
        string? instance = null,
        string? requestId = null,
        string? requiredScope = null)
        : this(status, message, code, details, type, title, detail, instance, requestId, requiredScope, null, null, null, null)
    {
    }

    /// <summary>Create an exception including Connector remediation metadata.</summary>
    public EPostakException(
        int status,
        string message,
        string? code,
        object? details,
        string? type,
        string? title,
        string? detail,
        string? instance,
        string? requestId,
        string? requiredScope,
        string? field,
        string? nextAction,
        bool? retryable,
        int? retryAfter)
        : base(message)
    {
        Status = status;
        Code = code;
        Details = details;
        Type = type;
        Title = title;
        Detail = detail;
        Instance = instance;
        RequestId = requestId;
        RequiredScope = requiredScope;
        Field = field;
        NextAction = nextAction;
        Retryable = retryable;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Create an exception from a network-level failure (DNS, timeout, connection refused).
    /// <see cref="Status"/> will be 0.
    /// </summary>
    /// <param name="message">Descriptive error message.</param>
    /// <param name="innerException">The original network exception.</param>
    public EPostakException(string message, Exception innerException)
        : base(message, innerException)
    {
        Status = 0;
    }
}
