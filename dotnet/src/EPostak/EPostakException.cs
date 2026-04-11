namespace EPostak;

/// <summary>
/// Exception thrown when an ePosťak API request fails. Contains the HTTP status code,
/// machine-readable error code, and any additional details from the API response body.
/// For network-level failures (DNS, timeout, connection refused), <see cref="Status"/> is 0
/// and the original exception is available via <see cref="Exception.InnerException"/>.
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

    /// <summary>Machine-readable error code from the API response (e.g. "VALIDATION_ERROR", "NOT_FOUND", "RATE_LIMITED"). Null for network errors.</summary>
    public string? Code { get; }

    /// <summary>Additional structured error details from the API (e.g. per-field validation messages). Null when not provided by the API.</summary>
    public object? Details { get; }

    /// <summary>
    /// Create an exception from an API error response.
    /// </summary>
    /// <param name="status">The HTTP status code from the API response.</param>
    /// <param name="message">Human-readable error message from the API.</param>
    /// <param name="code">Machine-readable error code (e.g. "VALIDATION_ERROR").</param>
    /// <param name="details">Additional structured error details.</param>
    public EPostakException(int status, string message, string? code = null, object? details = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Details = details;
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
