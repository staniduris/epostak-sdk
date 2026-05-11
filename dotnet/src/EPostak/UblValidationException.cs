namespace EPostak;

/// <summary>
/// Well-known UBL validation rule codes returned by the API when a document
/// fails Peppol BIS 3.0 / EN 16931 schematron rules.
/// These correspond to the <c>code</c> field in the 422 error response body.
/// </summary>
public static class UblRule
{
    /// <summary>Generic UBL validation failure — the error body contains rule-level details.</summary>
    public const string UblValidationError = "UBL_VALIDATION_ERROR";

    /// <summary>The UBL XML is not well-formed or violates the UBL 2.1 XSD schema.</summary>
    public const string SchemaInvalid = "UBL_SCHEMA_INVALID";

    /// <summary>The document type declared in the UBL header is not supported for Peppol transmission.</summary>
    public const string UnsupportedDocumentType = "UBL_UNSUPPORTED_DOCUMENT_TYPE";

    /// <summary>The supplier Peppol ID in the UBL XML does not match the authenticated firm.</summary>
    public const string SupplierMismatch = "UBL_SUPPLIER_MISMATCH";

    /// <summary>A mandatory EN 16931 business rule (BR-*) was violated.</summary>
    public const string En16931Violation = "UBL_EN16931_VIOLATION";

    /// <summary>A mandatory Peppol BIS 3.0 rule (PEPPOL-EN16931-R*) was violated.</summary>
    public const string PeppolBisViolation = "UBL_PEPPOL_BIS_VIOLATION";

    /// <summary>A Slovak national schematron rule (SK-R-*) was violated.</summary>
    public const string SkNationalViolation = "UBL_SK_NATIONAL_VIOLATION";
}

/// <summary>
/// Thrown when the API returns HTTP 422 with <c>code == "UBL_VALIDATION_ERROR"</c>
/// (or another <see cref="UblRule"/> code). Contains the rule identifier that
/// triggered the rejection and the optional server-assigned request ID.
/// </summary>
/// <remarks>
/// Catch this exception to handle schematron validation failures:
/// <list type="bullet">
/// <item><description>The offending rule identifier is exposed as <see cref="Rule"/>.</description></item>
/// <item><description>Human-readable details about each violation are in <see cref="EPostakException.Details"/>.</description></item>
/// <item><description>The server request ID (for support tickets) is in <see cref="EPostakException.RequestId"/>.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await client.Documents.SendAsync(request);
/// }
/// catch (UblValidationException ex)
/// {
///     Console.WriteLine($"UBL rule violated: {ex.Rule}");
///     Console.WriteLine($"Details: {ex.Details}");
///     if (ex.RequestId is not null)
///         Console.WriteLine($"Support reference: {ex.RequestId}");
/// }
/// </code>
/// </example>
public sealed class UblValidationException : EPostakException
{
    /// <summary>
    /// The machine-readable rule code that caused the rejection. One of the
    /// <see cref="UblRule"/> constants (e.g. <c>UblRule.UblValidationError</c>).
    /// </summary>
    public string Rule { get; }

    internal UblValidationException(
        int status,
        string message,
        string rule,
        object? details,
        string? type,
        string? title,
        string? detail,
        string? instance,
        string? requestId,
        string? requiredScope)
        : base(status, message, rule, details, type, title, detail, instance, requestId, requiredScope)
    {
        Rule = rule;
    }
}
