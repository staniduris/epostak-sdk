using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Send, receive, and manage e-invoicing documents via the Peppol network.
/// Provides operations for the full document lifecycle: sending, receiving,
/// status tracking, validation, and format conversion.
/// </summary>
public sealed class DocumentsResource
{
    private readonly HttpRequestor _http;

    /// <summary>Access received (inbound) documents from the Peppol network.</summary>
    public InboxResource Inbox { get; }

    internal DocumentsResource(HttpRequestor http)
    {
        _http = http;
        Inbox = new InboxResource(http);
    }

    /// <summary>
    /// Retrieve a document by its unique identifier.
    /// Returns the full document including supplier/customer details, line items, and totals.
    /// </summary>
    /// <param name="id">The document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete document with all metadata, parties, lines, and totals.</returns>
    /// <example>
    /// <code>
    /// var doc = await client.Documents.GetAsync("doc_abc123");
    /// Console.WriteLine($"{doc.Number}: {doc.Totals.WithVat} {doc.Currency}");
    /// </code>
    /// </example>
    public Task<Document> GetAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<Document>(HttpMethod.Get, $"/documents/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// Update a draft document before sending. Only documents in draft status can be updated.
    /// Any fields left null in the request will remain unchanged.
    /// </summary>
    /// <param name="id">The document UUID of the draft to update.</param>
    /// <param name="request">Fields to update on the draft document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated document with all current values.</returns>
    /// <example>
    /// <code>
    /// var updated = await client.Documents.UpdateAsync("doc_abc123", new UpdateDocumentRequest
    /// {
    ///     DueDate = "2026-05-01",
    ///     Note = "Updated payment terms"
    /// });
    /// </code>
    /// </example>
    public Task<Document> UpdateAsync(string id, UpdateDocumentRequest request, CancellationToken ct = default)
        => _http.RequestAsync<Document>(HttpMethod.Patch, $"/documents/{Uri.EscapeDataString(id)}", request, ct);

    /// <summary>
    /// Send an e-invoice via the Peppol network to a registered recipient.
    /// You can either provide structured JSON data (with line items) or raw UBL XML.
    /// The document is validated, converted to UBL if needed, and transmitted via AS4.
    /// </summary>
    /// <param name="request">The invoice data including receiver Peppol ID and line items or UBL XML.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The document ID, Peppol message ID, and initial delivery status.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Documents.SendAsync(new SendDocumentRequest
    /// {
    ///     ReceiverPeppolId = "0192:12345678",
    ///     InvoiceNumber = "FV-2026-001",
    ///     IssueDate = "2026-04-11",
    ///     DueDate = "2026-05-11",
    ///     Currency = "EUR",
    ///     Items = new List&lt;LineItem&gt;
    ///     {
    ///         new() { Description = "Consulting", Quantity = 10, UnitPrice = 100m, VatRate = 23m, Unit = "HUR" }
    ///     }
    /// });
    /// Console.WriteLine($"Sent: {result.DocumentId}, Status: {result.Status}");
    /// </code>
    /// </example>
    public Task<SendDocumentResponse> SendAsync(SendDocumentRequest request, CancellationToken ct = default)
        => _http.RequestAsync<SendDocumentResponse>(HttpMethod.Post, "/documents/send", request, ct);

    /// <summary>
    /// Get the current delivery status and full status history of a document.
    /// Use this to track whether a sent invoice has been delivered, acknowledged, or responded to.
    /// </summary>
    /// <param name="id">The document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current status, status history timeline, and optional validation/response details.</returns>
    /// <example>
    /// <code>
    /// var status = await client.Documents.StatusAsync("doc_abc123");
    /// Console.WriteLine($"Current: {status.Status}");
    /// foreach (var entry in status.StatusHistory)
    ///     Console.WriteLine($"  {entry.Timestamp}: {entry.Status}");
    /// </code>
    /// </example>
    public Task<DocumentStatusResponse> StatusAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<DocumentStatusResponse>(HttpMethod.Get, $"/documents/{Uri.EscapeDataString(id)}/status", ct);

    /// <summary>
    /// Get delivery evidence for a sent document. This includes the AS4 receipt from
    /// the receiver's access point, the Message Level Response (MLR), and any
    /// invoice response (accept/reject/query) from the recipient.
    /// </summary>
    /// <param name="id">The document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Available evidence: AS4 receipt, MLR document, and invoice response with timestamps.</returns>
    /// <example>
    /// <code>
    /// var evidence = await client.Documents.EvidenceAsync("doc_abc123");
    /// if (evidence.InvoiceResponse is not null)
    ///     Console.WriteLine($"Response: {evidence.InvoiceResponse.Status}");
    /// </code>
    /// </example>
    public Task<DocumentEvidenceResponse> EvidenceAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<DocumentEvidenceResponse>(HttpMethod.Get, $"/documents/{Uri.EscapeDataString(id)}/evidence", ct);

    /// <summary>
    /// Download the PDF visualization of a document. The PDF is generated from the
    /// UBL XML and includes a human-readable invoice layout.
    /// </summary>
    /// <param name="id">The document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PDF file contents as a byte array.</returns>
    /// <example>
    /// <code>
    /// byte[] pdf = await client.Documents.PdfAsync("doc_abc123");
    /// await File.WriteAllBytesAsync("invoice.pdf", pdf);
    /// </code>
    /// </example>
    public Task<byte[]> PdfAsync(string id, CancellationToken ct = default)
        => _http.RequestBytesAsync(HttpMethod.Get, $"/documents/{Uri.EscapeDataString(id)}/pdf", ct);

    /// <summary>
    /// Download the UBL XML source of a document. This is the canonical Peppol BIS 3.0
    /// representation that was sent or received over the network.
    /// </summary>
    /// <param name="id">The document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The UBL 2.1 XML document as a string.</returns>
    /// <example>
    /// <code>
    /// string ubl = await client.Documents.UblAsync("doc_abc123");
    /// File.WriteAllText("invoice.xml", ubl);
    /// </code>
    /// </example>
    public Task<string> UblAsync(string id, CancellationToken ct = default)
        => _http.RequestStringAsync(HttpMethod.Get, $"/documents/{Uri.EscapeDataString(id)}/ubl", ct);

    /// <summary>
    /// Respond to a received document with an Invoice Response (accept, reject, or query).
    /// Sends a Peppol BIS Invoice Response 3.0 message back to the original sender.
    /// </summary>
    /// <param name="id">The document UUID of the received invoice to respond to.</param>
    /// <param name="request">The response status (AP/RE/UQ) and optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Confirmation of the response including document ID and timestamp.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Documents.RespondAsync("doc_abc123", new InvoiceRespondRequest
    /// {
    ///     Status = InvoiceResponseCode.AP,
    ///     Note = "Invoice accepted for payment"
    /// });
    /// </code>
    /// </example>
    public Task<InvoiceRespondResponse> RespondAsync(string id, InvoiceRespondRequest request, CancellationToken ct = default)
        => _http.RequestAsync<InvoiceRespondResponse>(HttpMethod.Post, $"/documents/{Uri.EscapeDataString(id)}/respond", request, ct);

    /// <summary>
    /// Validate a document without sending it. Checks the invoice data against
    /// Peppol BIS 3.0 rules and Slovak e-invoicing requirements. For JSON mode,
    /// also returns the generated UBL XML.
    /// </summary>
    /// <param name="request">The invoice data to validate (same format as <see cref="SendAsync"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation result with valid/invalid flag, warnings, and generated UBL XML (for JSON mode).</returns>
    /// <example>
    /// <code>
    /// var result = await client.Documents.ValidateAsync(new SendDocumentRequest
    /// {
    ///     ReceiverPeppolId = "0192:12345678",
    ///     Items = new List&lt;LineItem&gt;
    ///     {
    ///         new() { Description = "Item 1", Quantity = 1, UnitPrice = 50m, VatRate = 23m }
    ///     }
    /// });
    /// if (!result.Valid)
    ///     Console.WriteLine($"Warnings: {string.Join(", ", result.Warnings)}");
    /// </code>
    /// </example>
    public Task<ValidationResult> ValidateAsync(SendDocumentRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ValidationResult>(HttpMethod.Post, "/documents/validate", request, ct);

    /// <summary>
    /// Check whether a receiver is registered on the Peppol network and can accept a given
    /// document type. Performs an SMP (Service Metadata Publisher) lookup without sending anything.
    /// Use this before sending to verify the recipient exists.
    /// </summary>
    /// <param name="request">The receiver Peppol ID and optional document type to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Registration status, document type support, and SMP URL for the receiver.</returns>
    /// <example>
    /// <code>
    /// var check = await client.Documents.PreflightAsync(new PreflightRequest
    /// {
    ///     ReceiverPeppolId = "0192:12345678"
    /// });
    /// if (check.Registered &amp;&amp; check.SupportsDocumentType)
    ///     Console.WriteLine("Receiver is ready to accept invoices");
    /// </code>
    /// </example>
    public Task<PreflightResult> PreflightAsync(PreflightRequest request, CancellationToken ct = default)
        => _http.RequestAsync<PreflightResult>(HttpMethod.Post, "/documents/preflight", request, ct);

    /// <summary>
    /// Convert between JSON and UBL XML document formats. Useful for previewing the UBL
    /// that would be generated from JSON data, or parsing received UBL into structured JSON.
    /// </summary>
    /// <param name="request">The input/output formats and the source document (JSON object or UBL XML string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The converted output in the target format, plus any non-fatal warnings.</returns>
    /// <example>
    /// <code>
    /// var result = await client.Documents.ConvertAsync(new ConvertRequest
    /// {
    ///     InputFormat = ConvertInputFormat.Ubl,
    ///     OutputFormat = ConvertOutputFormat.Json,
    ///     Document = ublXmlString
    /// });
    /// </code>
    /// </example>
    public Task<ConvertResult> ConvertAsync(ConvertRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConvertResult>(HttpMethod.Post, "/documents/convert", request, ct);

    /// <summary>
    /// Send multiple documents in a single call. Items are processed independently --
    /// a failure on one item does not abort the batch. The response contains a per-item
    /// status in the same order as the request.
    /// </summary>
    /// <param name="items">Items to send. Max 100 per call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch totals (total/succeeded/failed) and per-item results.</returns>
    /// <example>
    /// <code>
    /// var response = await client.Documents.SendBatchAsync(new List&lt;BatchSendItem&gt;
    /// {
    ///     new() { Document = new SendDocumentRequest { ReceiverPeppolId = "0192:12345678", ... } },
    ///     new() { Document = new SendDocumentRequest { ReceiverPeppolId = "0192:87654321", ... },
    ///             IdempotencyKey = "batch-002" }
    /// });
    /// Console.WriteLine($"{response.Succeeded}/{response.Total} sent");
    /// </code>
    /// </example>
    public Task<BatchSendResponse> SendBatchAsync(List<BatchSendItem> items, CancellationToken ct = default)
        => _http.RequestAsync<BatchSendResponse>(
            HttpMethod.Post,
            "/documents/send/batch",
            new BatchSendRequest { Items = items },
            ct);

    /// <summary>
    /// Parse a UBL XML invoice into structured JSON without persisting or sending it.
    /// The returned shape matches the JSON side of <see cref="ConvertAsync"/>
    /// with <see cref="ConvertOutputFormat.Json"/>.
    /// </summary>
    /// <param name="xml">UBL 2.1 XML document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed invoice and any non-fatal warnings.</returns>
    /// <example>
    /// <code>
    /// var parsed = await client.Documents.ParseAsync(ublXmlString);
    /// var json = (JsonElement)parsed.Document;
    /// Console.WriteLine(json.GetProperty("invoiceNumber").GetString());
    /// </code>
    /// </example>
    public Task<ParsedInvoice> ParseAsync(string xml, CancellationToken ct = default)
        => _http.RequestRawAsync<ParsedInvoice>(HttpMethod.Post, "/documents/parse", xml, "application/xml", ct);

    /// <summary>
    /// Mark the processing state of an inbound document. <paramref name="state"/> must be
    /// one of <c>delivered</c>, <c>processed</c>, <c>failed</c>, or <c>read</c>. The optional
    /// <paramref name="note"/> is recorded alongside the transition (e.g. failure reason).
    /// </summary>
    /// <param name="id">Document UUID to mark.</param>
    /// <param name="state">Target state: <c>delivered</c>, <c>processed</c>, <c>failed</c>, or <c>read</c>.</param>
    /// <param name="note">Optional note (max 500 chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mark response with updated timestamps and overall document status.</returns>
    /// <example>
    /// <code>
    /// var marked = await client.Documents.MarkAsync("doc_abc123", "processed", "Booked to ledger");
    /// Console.WriteLine($"Status now: {marked.Status}");
    /// </code>
    /// </example>
    public Task<MarkResponse> MarkAsync(string id, string state, string? note = null, CancellationToken ct = default)
        => _http.RequestAsync<MarkResponse>(
            HttpMethod.Post,
            $"/documents/{Uri.EscapeDataString(id)}/mark",
            new MarkRequest { State = state, Note = note },
            ct);
}
