using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Shared primitives
// ---------------------------------------------------------------------------

/// <summary>
/// Peppol BIS Invoice Response 3.0 status codes.
/// Used when responding to a received invoice.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvoiceResponseCode>))]
public enum InvoiceResponseCode
{
    /// <summary>Accepted -- the invoice is approved for payment.</summary>
    AP,
    /// <summary>Rejected -- the invoice is rejected (e.g. incorrect data, disputed amount).</summary>
    RE,
    /// <summary>Under Query -- the invoice requires clarification before acceptance or rejection.</summary>
    UQ
}

/// <summary>
/// Direction of a document relative to the firm: inbound (received) or outbound (sent).
/// </summary>
[JsonConverter(typeof(DocumentDirectionConverter))]
public enum DocumentDirection
{
    /// <summary>Received from another Peppol participant.</summary>
    Inbound,
    /// <summary>Sent to another Peppol participant.</summary>
    Outbound
}

internal sealed class DocumentDirectionConverter : JsonConverter<DocumentDirection>
{
    public override DocumentDirection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "inbound" => DocumentDirection.Inbound,
            "outbound" => DocumentDirection.Outbound,
            var s => throw new JsonException($"Unknown DocumentDirection: {s}")
        };

    public override void Write(Utf8JsonWriter writer, DocumentDirection value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            DocumentDirection.Inbound => "inbound",
            DocumentDirection.Outbound => "outbound",
            _ => throw new JsonException($"Unknown DocumentDirection: {value}")
        });
}

/// <summary>
/// Processing status of an inbox (received) document.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InboxStatus>))]
public enum InboxStatus
{
    /// <summary>Document received but not yet acknowledged by the consuming system.</summary>
    RECEIVED,
    /// <summary>Document has been acknowledged as processed.</summary>
    ACKNOWLEDGED
}

/// <summary>
/// Input format for the document format converter.
/// </summary>
[JsonConverter(typeof(ConvertInputFormatConverter))]
public enum ConvertInputFormat
{
    /// <summary>Input is structured JSON data.</summary>
    Json,
    /// <summary>Input is UBL 2.1 XML.</summary>
    Ubl
}

internal sealed class ConvertInputFormatConverter : JsonConverter<ConvertInputFormat>
{
    public override ConvertInputFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "json" => ConvertInputFormat.Json,
            "ubl" => ConvertInputFormat.Ubl,
            var s => throw new JsonException($"Unknown ConvertInputFormat: {s}")
        };

    public override void Write(Utf8JsonWriter writer, ConvertInputFormat value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            ConvertInputFormat.Json => "json",
            ConvertInputFormat.Ubl => "ubl",
            _ => throw new JsonException($"Unknown ConvertInputFormat: {value}")
        });
}

/// <summary>
/// Output format for the document format converter.
/// </summary>
[JsonConverter(typeof(ConvertOutputFormatConverter))]
public enum ConvertOutputFormat
{
    /// <summary>Output is UBL 2.1 XML.</summary>
    Ubl,
    /// <summary>Output is structured JSON data.</summary>
    Json
}

internal sealed class ConvertOutputFormatConverter : JsonConverter<ConvertOutputFormat>
{
    public override ConvertOutputFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "ubl" => ConvertOutputFormat.Ubl,
            "json" => ConvertOutputFormat.Json,
            var s => throw new JsonException($"Unknown ConvertOutputFormat: {s}")
        };

    public override void Write(Utf8JsonWriter writer, ConvertOutputFormat value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            ConvertOutputFormat.Ubl => "ubl",
            ConvertOutputFormat.Json => "json",
            _ => throw new JsonException($"Unknown ConvertOutputFormat: {value}")
        });
}

// ---------------------------------------------------------------------------
// Line items
// ---------------------------------------------------------------------------

/// <summary>
/// A line item on an outbound invoice. Represents a single product or service
/// with quantity, price, VAT rate, and optional discount.
/// </summary>
public sealed class LineItem
{
    /// <summary>Description of the product or service.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>Quantity of the item (e.g. 10 hours, 5 pieces).</summary>
    [JsonPropertyName("quantity")]
    public required decimal Quantity { get; set; }

    /// <summary>UN/CEFACT unit code, e.g. HUR = hours, C62 = pieces, KGM = kilograms.</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>Price per unit excluding VAT.</summary>
    [JsonPropertyName("unitPrice")]
    public required decimal UnitPrice { get; set; }

    /// <summary>VAT rate in percent (e.g. 23 for the standard Slovak rate).</summary>
    [JsonPropertyName("vatRate")]
    public required decimal VatRate { get; set; }

    /// <summary>Optional discount as a percentage (e.g. 10 for 10% off).</summary>
    [JsonPropertyName("discount")]
    public decimal? Discount { get; set; }
}

/// <summary>
/// A line item as returned by the API on document responses. Includes
/// server-calculated fields like VAT category and line total.
/// </summary>
public sealed class LineItemResponse
{
    /// <summary>Description of the product or service.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>Quantity of the item.</summary>
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    /// <summary>UN/CEFACT unit code (e.g. HUR, C62, KGM). Null if not specified.</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>Price per unit excluding VAT.</summary>
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    /// <summary>VAT rate in percent.</summary>
    [JsonPropertyName("vatRate")]
    public decimal VatRate { get; set; }

    /// <summary>UBL VAT category code (e.g. "S" for standard rate, "Z" for zero-rated, "E" for exempt).</summary>
    [JsonPropertyName("vatCategory")]
    public string? VatCategory { get; set; }

    /// <summary>Calculated line total excluding VAT (quantity * unitPrice - discount).</summary>
    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }
}

// ---------------------------------------------------------------------------
// Party
// ---------------------------------------------------------------------------

/// <summary>
/// Postal address of a business party (supplier or customer).
/// </summary>
public sealed class PartyAddress
{
    /// <summary>Street name and building number.</summary>
    [JsonPropertyName("street")]
    public string? Street { get; set; }

    /// <summary>City or town name.</summary>
    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>Postal/ZIP code.</summary>
    [JsonPropertyName("zip")]
    public string? Zip { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. "SK", "CZ", "DE").</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

/// <summary>
/// A business party (supplier or customer) on an invoice. Contains legal identifiers
/// used in Slovak and EU e-invoicing.
/// </summary>
public sealed class Party
{
    /// <summary>Legal business name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Slovak business registration number (ICO) -- 8-digit identifier assigned by the Slovak Statistical Office.</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Tax identification number (DIC) -- used for income tax purposes in Slovakia.</summary>
    [JsonPropertyName("dic")]
    public string? Dic { get; set; }

    /// <summary>VAT identification number (IC DPH) -- Slovak VAT registration number in the format "SK" + 10 digits.</summary>
    [JsonPropertyName("icDph")]
    public string? IcDph { get; set; }

    /// <summary>Postal address of the party.</summary>
    [JsonPropertyName("address")]
    public PartyAddress? Address { get; set; }

    /// <summary>Peppol participant identifier (e.g. "0192:12345678").</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }
}

// ---------------------------------------------------------------------------
// Document totals
// ---------------------------------------------------------------------------

/// <summary>
/// Aggregate monetary totals for a document, calculated from line items.
/// </summary>
public sealed class DocumentTotals
{
    /// <summary>Total amount excluding VAT.</summary>
    [JsonPropertyName("withoutVat")]
    public decimal WithoutVat { get; set; }

    /// <summary>Total VAT amount.</summary>
    [JsonPropertyName("vat")]
    public decimal Vat { get; set; }

    /// <summary>Total amount including VAT (the payable amount).</summary>
    [JsonPropertyName("withVat")]
    public decimal WithVat { get; set; }
}

// ---------------------------------------------------------------------------
// Document (shared response shape)
// ---------------------------------------------------------------------------

/// <summary>
/// A Peppol e-invoicing document with all metadata, parties, line items, and totals.
/// This is the shared response shape returned by document retrieval and listing endpoints.
/// </summary>
public sealed class Document
{
    /// <summary>Unique document UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Invoice number (e.g. "FV-2026-001").</summary>
    [JsonPropertyName("number")]
    public string Number { get; set; } = "";

    /// <summary>Current delivery status (e.g. "draft", "sent", "delivered", "failed", "received", "acknowledged").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Whether this document was sent (outbound) or received (inbound).</summary>
    [JsonPropertyName("direction")]
    public DocumentDirection Direction { get; set; }

    /// <summary>Document type identifier (e.g. "invoice", "credit_note").</summary>
    [JsonPropertyName("docType")]
    public string DocType { get; set; } = "";

    /// <summary>Invoice issue date in ISO 8601 format (YYYY-MM-DD).</summary>
    [JsonPropertyName("issueDate")]
    public string IssueDate { get; set; } = "";

    /// <summary>Payment due date in ISO 8601 format (YYYY-MM-DD). Null if not specified.</summary>
    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    /// <summary>ISO 4217 currency code (e.g. "EUR", "CZK", "USD").</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    /// <summary>The invoice supplier (seller) party with name, ICO, tax IDs, and address.</summary>
    [JsonPropertyName("supplier")]
    public Party Supplier { get; set; } = new();

    /// <summary>The invoice customer (buyer) party with name, ICO, tax IDs, and address.</summary>
    [JsonPropertyName("customer")]
    public Party Customer { get; set; } = new();

    /// <summary>Invoice line items with descriptions, quantities, prices, and calculated totals.</summary>
    [JsonPropertyName("lines")]
    public List<LineItemResponse> Lines { get; set; } = [];

    /// <summary>Aggregate totals: amount without VAT, VAT amount, and total with VAT.</summary>
    [JsonPropertyName("totals")]
    public DocumentTotals Totals { get; set; } = new();

    /// <summary>Peppol AS4 message identifier. Null for draft documents not yet sent.</summary>
    [JsonPropertyName("peppolMessageId")]
    public string? PeppolMessageId { get; set; }

    /// <summary>Timestamp when the document was created (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>Timestamp when the document was last updated (ISO 8601).</summary>
    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Send document
// ---------------------------------------------------------------------------

/// <summary>
/// Request to send an e-invoice via the Peppol network. Provide either structured
/// data (<see cref="Items"/>) or raw UBL XML (<see cref="Xml"/>), but not both.
/// </summary>
public sealed class SendDocumentRequest
{
    /// <summary>Peppol identifier of the receiver (e.g. "0192:12345678"). Required.</summary>
    [JsonPropertyName("receiverPeppolId")]
    public required string ReceiverPeppolId { get; set; }

    /// <summary>Invoice number (e.g. "FV-2026-001"). Auto-generated if not provided.</summary>
    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    /// <summary>Invoice issue date in YYYY-MM-DD format. Defaults to today.</summary>
    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    /// <summary>Payment due date in YYYY-MM-DD format.</summary>
    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    /// <summary>ISO 4217 currency code (e.g. "EUR"). Defaults to "EUR".</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Free-text note included in the invoice (e.g. payment instructions, references).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>IBAN bank account number for payment.</summary>
    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    /// <summary>Payment method code (e.g. "credit_transfer", "direct_debit").</summary>
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>Variable symbol (variabilny symbol) -- Slovak payment reference number used to match payments to invoices.</summary>
    [JsonPropertyName("variableSymbol")]
    public string? VariableSymbol { get; set; }

    /// <summary>Buyer reference or purchase order number required by the receiver.</summary>
    [JsonPropertyName("buyerReference")]
    public string? BuyerReference { get; set; }

    /// <summary>Legal name of the receiver company. Auto-resolved from Peppol ID if not provided.</summary>
    [JsonPropertyName("receiverName")]
    public string? ReceiverName { get; set; }

    /// <summary>Receiver's Slovak business registration number (ICO).</summary>
    [JsonPropertyName("receiverIco")]
    public string? ReceiverIco { get; set; }

    /// <summary>Receiver's tax identification number (DIC).</summary>
    [JsonPropertyName("receiverDic")]
    public string? ReceiverDic { get; set; }

    /// <summary>Receiver's VAT identification number (IC DPH).</summary>
    [JsonPropertyName("receiverIcDph")]
    public string? ReceiverIcDph { get; set; }

    /// <summary>Receiver's postal address as a single string.</summary>
    [JsonPropertyName("receiverAddress")]
    public string? ReceiverAddress { get; set; }

    /// <summary>Receiver's ISO 3166-1 alpha-2 country code (e.g. "SK").</summary>
    [JsonPropertyName("receiverCountry")]
    public string? ReceiverCountry { get; set; }

    /// <summary>Invoice line items. Mutually exclusive with <see cref="Xml"/>.</summary>
    [JsonPropertyName("items")]
    public List<LineItem>? Items { get; set; }

    /// <summary>
    /// Invoice attachments (BG-24). JSON mode only; embedded into the generated UBL XML as base64
    /// via <c>AdditionalDocumentReference</c> / <c>EmbeddedDocumentBinaryObject</c>, so the receiver
    /// sees them inline with the invoice. Limits: max 20 files, 10 MB each, 15 MB total.
    /// </summary>
    [JsonPropertyName("attachments")]
    public List<DocumentAttachment>? Attachments { get; set; }

    /// <summary>Raw UBL 2.1 XML to send instead of structured JSON. Mutually exclusive with <see cref="Items"/>.</summary>
    [JsonPropertyName("xml")]
    public string? Xml { get; set; }
}

/// <summary>
/// An invoice attachment (BG-24) embedded as base64 into the UBL XML. MIME type is verified
/// by magic-byte sniffing server-side; the declared <see cref="MimeType"/> must match the
/// actual file content or the request is rejected with <c>VALIDATION_ERROR</c>.
/// </summary>
public sealed class DocumentAttachment
{
    /// <summary>Original file name (max 255 chars). Required.</summary>
    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }

    /// <summary>
    /// MIME type. Allowed values (BR-CL-22): <c>application/pdf</c>, <c>image/png</c>,
    /// <c>image/jpeg</c>, <c>text/csv</c>,
    /// <c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c> (.xlsx),
    /// <c>application/vnd.oasis.opendocument.spreadsheet</c> (.ods).
    /// </summary>
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; set; }

    /// <summary>Base64-encoded file content (no <c>data:</c> prefix). Max 10 MB after decoding. Required.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; set; }

    /// <summary>Optional short description (max 100 chars).</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Response returned after successfully submitting a document for Peppol delivery.
/// </summary>
public sealed class SendDocumentResponse
{
    /// <summary>Unique document UUID assigned by the API.</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>Peppol AS4 message identifier for tracking the delivery.</summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = "";

    /// <summary>Initial delivery status (typically "sent" or "queued").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Update document (draft only)
// ---------------------------------------------------------------------------

/// <summary>
/// Request to update a draft document. Only documents in "draft" status can be updated.
/// All fields are optional -- only provided (non-null) fields are changed.
/// </summary>
public sealed class UpdateDocumentRequest
{
    /// <summary>Invoice number (e.g. "FV-2026-001").</summary>
    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    /// <summary>Invoice issue date in YYYY-MM-DD format.</summary>
    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    /// <summary>Payment due date in YYYY-MM-DD format.</summary>
    [JsonPropertyName("dueDate")]
    public string? DueDate { get; set; }

    /// <summary>ISO 4217 currency code (e.g. "EUR").</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>Free-text note on the invoice.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>IBAN bank account number for payment.</summary>
    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    /// <summary>Variable symbol (variabilny symbol) -- Slovak payment reference number.</summary>
    [JsonPropertyName("variableSymbol")]
    public string? VariableSymbol { get; set; }

    /// <summary>Buyer reference or purchase order number.</summary>
    [JsonPropertyName("buyerReference")]
    public string? BuyerReference { get; set; }

    /// <summary>Legal name of the receiver company.</summary>
    [JsonPropertyName("receiverName")]
    public string? ReceiverName { get; set; }

    /// <summary>Receiver's Slovak business registration number (ICO).</summary>
    [JsonPropertyName("receiverIco")]
    public string? ReceiverIco { get; set; }

    /// <summary>Receiver's tax identification number (DIC).</summary>
    [JsonPropertyName("receiverDic")]
    public string? ReceiverDic { get; set; }

    /// <summary>Receiver's VAT identification number (IC DPH).</summary>
    [JsonPropertyName("receiverIcDph")]
    public string? ReceiverIcDph { get; set; }

    /// <summary>Receiver's postal address as a single string.</summary>
    [JsonPropertyName("receiverAddress")]
    public string? ReceiverAddress { get; set; }

    /// <summary>Receiver's ISO 3166-1 alpha-2 country code (e.g. "SK").</summary>
    [JsonPropertyName("receiverCountry")]
    public string? ReceiverCountry { get; set; }

    /// <summary>Peppol identifier of the receiver (e.g. "0192:12345678").</summary>
    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    /// <summary>Replacement line items. Replaces the entire line items list when provided.</summary>
    [JsonPropertyName("items")]
    public List<LineItem>? Items { get; set; }
}

// ---------------------------------------------------------------------------
// Inbox
// ---------------------------------------------------------------------------

/// <summary>
/// Parameters for listing inbox (received) documents with optional filtering.
/// </summary>
public sealed class InboxListParams
{
    /// <summary>Number of documents to skip for pagination (default 0).</summary>
    public int? Offset { get; set; }

    /// <summary>Maximum number of documents to return (default 20, max 100).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter by processing status: RECEIVED (unprocessed) or ACKNOWLEDGED.</summary>
    public InboxStatus? Status { get; set; }

    /// <summary>ISO 8601 timestamp -- only return documents created after this date (e.g. "2026-01-01T00:00:00Z").</summary>
    public string? Since { get; set; }
}

/// <summary>
/// Paginated response containing inbox documents.
/// </summary>
public sealed class InboxListResponse
{
    /// <summary>List of documents in the current page.</summary>
    [JsonPropertyName("documents")]
    public List<Document> Documents { get; set; } = [];

    /// <summary>Total number of documents matching the filter criteria.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>The limit value used for this response.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>The offset value used for this response.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

/// <summary>
/// Detailed inbox document including the raw UBL XML payload.
/// </summary>
public sealed class InboxDocumentDetailResponse
{
    /// <summary>The full document with metadata, parties, lines, and totals.</summary>
    [JsonPropertyName("document")]
    public Document Document { get; set; } = new();

    /// <summary>Raw UBL 2.1 XML content as received from the Peppol network. Null if not yet available.</summary>
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}

/// <summary>
/// Confirmation returned after acknowledging an inbox document.
/// </summary>
public sealed class AcknowledgeResponse
{
    /// <summary>The acknowledged document's UUID.</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>New status after acknowledgement (typically "acknowledged").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Timestamp when the document was acknowledged (ISO 8601).</summary>
    [JsonPropertyName("acknowledgedAt")]
    public string AcknowledgedAt { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Inbox all (integrator -- cross-firm inbox)
// ---------------------------------------------------------------------------

/// <summary>
/// Parameters for listing inbox documents across all firms (integrator keys only).
/// </summary>
public sealed class InboxAllParams
{
    /// <summary>Number of documents to skip for pagination (default 0).</summary>
    public int? Offset { get; set; }

    /// <summary>Maximum number of documents to return (default 20, max 100).</summary>
    public int? Limit { get; set; }

    /// <summary>Filter by processing status: RECEIVED (unprocessed) or ACKNOWLEDGED.</summary>
    public InboxStatus? Status { get; set; }

    /// <summary>ISO 8601 timestamp -- only return documents created after this date.</summary>
    public string? Since { get; set; }

    /// <summary>Filter to a specific firm UUID. When set, only returns documents for that firm.</summary>
    public string? FirmId { get; set; }
}

/// <summary>
/// Simplified party representation used in cross-firm inbox responses.
/// </summary>
public sealed class InboxAllParty
{
    /// <summary>Legal business name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Slovak business registration number (ICO).</summary>
    [JsonPropertyName("ico")]
    public string? Ico { get; set; }

    /// <summary>Peppol participant identifier.</summary>
    [JsonPropertyName("peppol_id")]
    public string? PeppolId { get; set; }
}

/// <summary>
/// Document totals in cross-firm inbox responses. Values may be null if not yet calculated.
/// </summary>
public sealed class InboxAllTotals
{
    /// <summary>Total amount excluding VAT.</summary>
    [JsonPropertyName("without_vat")]
    public decimal? WithoutVat { get; set; }

    /// <summary>Total VAT amount.</summary>
    [JsonPropertyName("vat")]
    public decimal? Vat { get; set; }

    /// <summary>Total amount including VAT.</summary>
    [JsonPropertyName("with_vat")]
    public decimal? WithVat { get; set; }
}

/// <summary>
/// A document in the cross-firm inbox response. Includes the firm ID and name
/// so integrators can route documents to the correct client.
/// </summary>
public sealed class InboxAllDocument
{
    /// <summary>UUID of the firm that received this document.</summary>
    [JsonPropertyName("firm_id")]
    public string FirmId { get; set; } = "";

    /// <summary>Name of the firm that received this document.</summary>
    [JsonPropertyName("firm_name")]
    public string? FirmName { get; set; }

    /// <summary>Unique document UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Invoice number.</summary>
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    /// <summary>Current processing status (e.g. "received", "acknowledged").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Document direction (always "inbound" in inbox responses).</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    /// <summary>Document type identifier (e.g. "invoice", "credit_note").</summary>
    [JsonPropertyName("doc_type")]
    public string DocType { get; set; } = "";

    /// <summary>Invoice issue date (YYYY-MM-DD).</summary>
    [JsonPropertyName("issue_date")]
    public string? IssueDate { get; set; }

    /// <summary>Payment due date (YYYY-MM-DD).</summary>
    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    /// <summary>ISO 4217 currency code (e.g. "EUR").</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    /// <summary>The invoice supplier (seller) party.</summary>
    [JsonPropertyName("supplier")]
    public InboxAllParty Supplier { get; set; } = new();

    /// <summary>The invoice customer (buyer) party.</summary>
    [JsonPropertyName("customer")]
    public InboxAllParty Customer { get; set; } = new();

    /// <summary>Aggregate monetary totals for the document.</summary>
    [JsonPropertyName("totals")]
    public InboxAllTotals Totals { get; set; } = new();

    /// <summary>Peppol AS4 message identifier.</summary>
    [JsonPropertyName("peppol_message_id")]
    public string? PeppolMessageId { get; set; }

    /// <summary>Timestamp when the document was created (ISO 8601).</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// Paginated response containing inbox documents from all firms (integrator keys only).
/// </summary>
public sealed class InboxAllResponse
{
    /// <summary>List of cross-firm inbox documents in the current page.</summary>
    [JsonPropertyName("documents")]
    public List<InboxAllDocument> Documents { get; set; } = [];

    /// <summary>Total number of documents matching the filter criteria across all firms.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>The limit value used for this response.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>The offset value used for this response.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

// ---------------------------------------------------------------------------
// Document lifecycle -- status
// ---------------------------------------------------------------------------

/// <summary>
/// A single entry in a document's status history timeline, recording when
/// and why the document transitioned to a given status.
/// </summary>
public sealed class StatusHistoryEntry
{
    /// <summary>The status the document transitioned to (e.g. "sent", "delivered", "failed").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>ISO 8601 timestamp when this status transition occurred.</summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    /// <summary>Optional human-readable detail about the transition (e.g. error message for failures).</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

/// <summary>
/// Full delivery status of a document including the status history timeline,
/// validation results, and delivery evidence timestamps.
/// </summary>
public sealed class DocumentStatusResponse
{
    /// <summary>Unique document UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Current delivery status (e.g. "draft", "sent", "delivered", "failed", "received").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Peppol document type identifier.</summary>
    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    /// <summary>Peppol ID of the sender.</summary>
    [JsonPropertyName("senderPeppolId")]
    public string? SenderPeppolId { get; set; }

    /// <summary>Peppol ID of the receiver.</summary>
    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    /// <summary>Chronological list of status transitions from creation to current state.</summary>
    [JsonPropertyName("statusHistory")]
    public List<StatusHistoryEntry> StatusHistory { get; set; } = [];

    /// <summary>Peppol BIS 3.0 validation results if validation was performed. Null for received documents.</summary>
    [JsonPropertyName("validationResult")]
    public Dictionary<string, object>? ValidationResult { get; set; }

    /// <summary>Timestamp when the document was successfully delivered to the receiver's access point (ISO 8601).</summary>
    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    /// <summary>Timestamp when the receiver acknowledged the document (ISO 8601).</summary>
    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }

    /// <summary>Invoice response status from the receiver: AP (accepted), RE (rejected), or UQ (under query). Null if no response yet.</summary>
    [JsonPropertyName("invoiceResponseStatus")]
    public InvoiceResponseCode? InvoiceResponseStatus { get; set; }

    /// <summary>AS4 message identifier used for Peppol transport layer tracking.</summary>
    [JsonPropertyName("as4MessageId")]
    public string? As4MessageId { get; set; }

    /// <summary>Timestamp when the document was created (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>Timestamp when the document was last updated (ISO 8601).</summary>
    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Document lifecycle -- evidence
// ---------------------------------------------------------------------------

/// <summary>
/// Invoice response evidence from the receiver, containing the response status
/// and the full Invoice Response document.
/// </summary>
public sealed class InvoiceResponseEvidence
{
    /// <summary>Response status: AP (accepted), RE (rejected), or UQ (under query).</summary>
    [JsonPropertyName("status")]
    public InvoiceResponseCode? Status { get; set; }

    /// <summary>The full Peppol BIS Invoice Response 3.0 document as a parsed JSON object.</summary>
    [JsonPropertyName("document")]
    public Dictionary<string, object> Document { get; set; } = [];
}

/// <summary>
/// Delivery evidence for a sent document. Contains AS4 receipt from the receiver's
/// access point, the Message Level Response (MLR), and any Invoice Response from the recipient.
/// </summary>
public sealed class DocumentEvidenceResponse
{
    /// <summary>The document UUID this evidence belongs to.</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>AS4 receipt from the receiver's Peppol access point, confirming successful transport. Null if not yet received.</summary>
    [JsonPropertyName("as4Receipt")]
    public Dictionary<string, object>? As4Receipt { get; set; }

    /// <summary>Message Level Response (MLR) document confirming successful processing by the receiver's system. Null if not yet received.</summary>
    [JsonPropertyName("mlrDocument")]
    public Dictionary<string, object>? MlrDocument { get; set; }

    /// <summary>Invoice Response from the recipient (accept/reject/query). Null if the recipient has not responded yet.</summary>
    [JsonPropertyName("invoiceResponse")]
    public InvoiceResponseEvidence? InvoiceResponse { get; set; }

    /// <summary>Timestamp when the document was delivered to the receiver's access point (ISO 8601).</summary>
    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    /// <summary>Timestamp when the document was sent from the sender's access point (ISO 8601).</summary>
    [JsonPropertyName("sentAt")]
    public string? SentAt { get; set; }
}

// ---------------------------------------------------------------------------
// Invoice response (respond to received document)
// ---------------------------------------------------------------------------

/// <summary>
/// Request to respond to a received invoice via Peppol BIS Invoice Response 3.0.
/// </summary>
public sealed class InvoiceRespondRequest
{
    /// <summary>Response status: AP (accepted for payment), RE (rejected), or UQ (under query / needs clarification).</summary>
    [JsonPropertyName("status")]
    public required InvoiceResponseCode Status { get; set; }

    /// <summary>Optional note explaining the response (e.g. reason for rejection or clarification needed).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Confirmation returned after successfully sending an Invoice Response.
/// </summary>
public sealed class InvoiceRespondResponse
{
    /// <summary>The UUID of the document that was responded to.</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>The response status that was sent (AP, RE, or UQ).</summary>
    [JsonPropertyName("responseStatus")]
    public InvoiceResponseCode ResponseStatus { get; set; }

    /// <summary>Timestamp when the response was sent (ISO 8601).</summary>
    [JsonPropertyName("respondedAt")]
    public string RespondedAt { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Validate / preflight / convert
// ---------------------------------------------------------------------------

/// <summary>
/// Result of validating a document against Peppol BIS 3.0 and Slovak e-invoicing rules.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Whether the document passes all validation rules.</summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>List of validation warnings (non-fatal issues that may cause problems).</summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    /// <summary>Generated UBL 2.1 XML. Only present when validating JSON mode requests (not raw XML).</summary>
    [JsonPropertyName("ubl")]
    public string? Ubl { get; set; }
}

/// <summary>
/// Request to check whether a receiver can accept documents via Peppol (SMP lookup).
/// </summary>
public sealed class PreflightRequest
{
    /// <summary>Peppol identifier of the receiver to check (e.g. "0192:12345678"). Required.</summary>
    [JsonPropertyName("receiverPeppolId")]
    public required string ReceiverPeppolId { get; set; }

    /// <summary>Optional Peppol document type ID to check support for (e.g. invoice, credit note). If omitted, checks general registration.</summary>
    [JsonPropertyName("documentTypeId")]
    public string? DocumentTypeId { get; set; }
}

/// <summary>
/// Result of a preflight SMP lookup checking receiver registration and capability.
/// </summary>
public sealed class PreflightResult
{
    /// <summary>The Peppol ID that was checked.</summary>
    [JsonPropertyName("receiverPeppolId")]
    public string ReceiverPeppolId { get; set; } = "";

    /// <summary>Whether the receiver is registered on the Peppol network.</summary>
    [JsonPropertyName("registered")]
    public bool Registered { get; set; }

    /// <summary>Whether the receiver's SMP entry supports the requested document type.</summary>
    [JsonPropertyName("supportsDocumentType")]
    public bool SupportsDocumentType { get; set; }

    /// <summary>URL of the receiver's SMP (Service Metadata Publisher) entry. Null if not registered.</summary>
    [JsonPropertyName("smpUrl")]
    public string? SmpUrl { get; set; }
}

/// <summary>
/// Request to convert between JSON and UBL XML document formats.
/// </summary>
public sealed class ConvertRequest
{
    /// <summary>Input format: <see cref="ConvertInputFormat.Json"/> or <see cref="ConvertInputFormat.Ubl"/>.</summary>
    [JsonPropertyName("input_format")]
    public required ConvertInputFormat InputFormat { get; set; }

    /// <summary>Output format: <see cref="ConvertOutputFormat.Ubl"/> or <see cref="ConvertOutputFormat.Json"/>.</summary>
    [JsonPropertyName("output_format")]
    public required ConvertOutputFormat OutputFormat { get; set; }

    /// <summary>
    /// The source document. A <see cref="Dictionary{TKey, TValue}"/> (or any object serializable to JSON)
    /// when <see cref="InputFormat"/> is <see cref="ConvertInputFormat.Json"/>, or a <see cref="string"/>
    /// containing UBL 2.1 XML when <see cref="InputFormat"/> is <see cref="ConvertInputFormat.Ubl"/>.
    /// </summary>
    [JsonPropertyName("document")]
    public required object Document { get; set; }
}

/// <summary>
/// Result of a document format conversion.
/// </summary>
public sealed class ConvertResult
{
    /// <summary>The output format that was produced.</summary>
    [JsonPropertyName("output_format")]
    public ConvertOutputFormat OutputFormat { get; set; }

    /// <summary>
    /// The converted document: a UBL XML <see cref="string"/> when <see cref="OutputFormat"/> is
    /// <see cref="ConvertOutputFormat.Ubl"/>, or a parsed JSON object (<see cref="JsonElement"/>)
    /// when <see cref="OutputFormat"/> is <see cref="ConvertOutputFormat.Json"/>.
    /// </summary>
    [JsonPropertyName("document")]
    public object Document { get; set; } = "";

    /// <summary>Non-fatal warnings produced by the converter (e.g. lossy fields, schema hints).</summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
