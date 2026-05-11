namespace EPostak;

/// <summary>
/// Well-known UBL pre-flight rule codes returned in <c>error.rule</c> when the
/// API rejects a document with <c>error.code == "UBL_VALIDATION_ERROR"</c>
/// (HTTP 422). Source of truth: <c>lib/ubl/generate.ts</c> in the epostak repo.
/// New rules may be added in future API versions; treat as hint, not closed enum.
/// </summary>
public static class UblRule
{
    /// <summary>BT-2 — Invoice issue date is mandatory.</summary>
    public const string BR_02 = "BR-02";

    /// <summary>BT-27 — Seller name is mandatory.</summary>
    public const string BR_05 = "BR-05";

    /// <summary>BT-44 — Buyer name is mandatory. Pass <c>receiverName</c> in the request body.</summary>
    public const string BR_06 = "BR-06";

    /// <summary>BT-31 / BT-32 — Seller VAT identifier required for VAT-rated invoices.</summary>
    public const string BR_11 = "BR-11";

    /// <summary>Invoice must have at least one line.</summary>
    public const string BR_16 = "BR-16";

    /// <summary>BT-1 — Invoice number must not be empty.</summary>
    public const string BT_1 = "BT-1";

    /// <summary>EndpointID empty — firm must have DIČ, IČO, or a registered Peppol ID.</summary>
    public const string PEPPOL_R008 = "PEPPOL-R008";
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
    /// The Peppol BIS / EN 16931 rule code that caused the rejection. One of
    /// the <see cref="UblRule"/> constants (e.g. <c>UblRule.BR_06</c>). May be
    /// an empty string if the server omitted the <c>rule</c> field.
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
        // Code stays "UBL_VALIDATION_ERROR" (always — that's the wire code);
        // the violated rule is exposed separately via Rule.
        : base(status, message, "UBL_VALIDATION_ERROR", details, type, title, detail, instance, requestId, requiredScope)
    {
        Rule = rule;
    }
}
