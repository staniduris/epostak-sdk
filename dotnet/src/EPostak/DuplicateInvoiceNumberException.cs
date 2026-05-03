namespace EPostak;

/// <summary>
/// Recipient identification on the existing duplicate invoice. Any field
/// can be <c>null</c> if the original invoice did not have it stored.
/// </summary>
public sealed record DuplicateInvoiceRecipient(string? PeppolId, string? Ico, string? Name);

/// <summary>
/// The pre-existing outbound invoice that triggered the conflict.
/// <see cref="SentAt"/> is an ISO 8601 string — <c>peppolSentAt</c> if the
/// original was already delivered, otherwise <c>createdAt</c>.
/// </summary>
public sealed record DuplicateInvoiceExistingDocument(
    string Id,
    string InvoiceNumber,
    string Status,
    string SentAt,
    DuplicateInvoiceRecipient? Recipient
);

/// <summary>
/// Thrown when <c>POST /api/v1/documents/send</c> (or the dashboard create
/// endpoint) rejects an outbound invoice whose <c>invoice_number</c> is
/// already in use for the firm.
/// </summary>
/// <remarks>
/// The conflict key is <c>(firmId, invoiceNumber)</c> — recipient is NOT
/// part of it; outbound numbering belongs to the sender.
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await client.Documents.SendAsync(req, ct);
/// }
/// catch (DuplicateInvoiceNumberException ex)
/// {
///     var existing = ex.ExistingDocument;
///     if (existing != null)
///         Console.WriteLine($"Already sent on {existing.SentAt}, id={existing.Id}");
/// }
/// </code>
/// </example>
public class DuplicateInvoiceNumberException : EPostakException
{
    /// <summary>Always <c>["firmId", "invoiceNumber"]</c>.</summary>
    public IReadOnlyList<string> ConflictKey { get; }

    /// <summary>
    /// The pre-existing outbound invoice that caused the conflict, or
    /// <c>null</c> if it was deleted between the constraint hit and the
    /// server-side lookup.
    /// </summary>
    public DuplicateInvoiceExistingDocument? ExistingDocument { get; }

    public DuplicateInvoiceNumberException(
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
        IReadOnlyList<string>? conflictKey,
        DuplicateInvoiceExistingDocument? existingDocument
    ) : base(status, message, code, details, type, title, detail, instance, requestId, requiredScope)
    {
        ConflictKey = conflictKey ?? Array.AsReadOnly(new[] { "firmId", "invoiceNumber" });
        ExistingDocument = existingDocument;
    }
}
