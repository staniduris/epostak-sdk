using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPostak.Models;

// ---------------------------------------------------------------------------
// Shared primitives
// ---------------------------------------------------------------------------

/// <summary>
/// Peppol BIS Invoice Response 3.0 status codes (UNCL4343 subset). Used when
/// responding to a received invoice.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvoiceResponseCode>))]
public enum InvoiceResponseCode
{
    /// <summary>Accepted Billing — the invoice has been accepted for billing/processing.</summary>
    AB,
    /// <summary>In Process — the invoice is being processed but not yet accepted.</summary>
    IP,
    /// <summary>Under Query — the invoice requires clarification before acceptance or rejection.</summary>
    UQ,
    /// <summary>Conditionally Accepted — accepted subject to conditions described in the note.</summary>
    CA,
    /// <summary>Rejected — the invoice is rejected (e.g. incorrect data, disputed amount).</summary>
    RE,
    /// <summary>Accepted — the invoice is approved for payment.</summary>
    AP,
    /// <summary>Paid — the invoice has been paid.</summary>
    PD
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

    /// <summary>UBL VAT category code, for example S, Z, or AE.</summary>
    [JsonPropertyName("vatCategoryCode")]
    public string? VatCategoryCode { get; set; }

    /// <summary>Alias for <see cref="VatCategoryCode"/>.</summary>
    [JsonPropertyName("vatCategory")]
    public string? VatCategory { get; set; }

    /// <summary>Higher-level tax treatment mapped by the API to VatCategoryCode.</summary>
    [JsonPropertyName("taxTreatment")]
    public string? TaxTreatment { get; set; }

    /// <summary>Line delivery date in YYYY-MM-DD format.</summary>
    [JsonPropertyName("deliveryDate")]
    public string? DeliveryDate { get; set; }

    /// <summary>Line type, for example standard or advance_deduction.</summary>
    [JsonPropertyName("lineType")]
    public string? LineType { get; set; }

    /// <summary>Advance invoice reference required for advance deduction lines.</summary>
    [JsonPropertyName("advanceInvoiceReference")]
    public string? AdvanceInvoiceReference { get; set; }

    /// <summary>Customs tariff / combined nomenclature code.</summary>
    [JsonPropertyName("customsTariffCode")]
    public string? CustomsTariffCode { get; set; }

    /// <summary>Generic item classification code when CustomsTariffCode is not used.</summary>
    [JsonPropertyName("commodityClassificationCode")]
    public string? CommodityClassificationCode { get; set; }

    /// <summary>Classification list identifier, for example HS.</summary>
    [JsonPropertyName("commodityClassificationListId")]
    public string? CommodityClassificationListId { get; set; }

    /// <summary>Domestic reverse-charge paragraph letter evidence.</summary>
    [JsonPropertyName("reverseChargeParagraphLetter")]
    public string? ReverseChargeParagraphLetter { get; set; }

    /// <summary>Slovak control-statement type, for example IO or MT.</summary>
    [JsonPropertyName("controlStatementType")]
    public string? ControlStatementType { get; set; }

    /// <summary>Slovak control-statement quantity.</summary>
    [JsonPropertyName("controlStatementQuantity")]
    public decimal? ControlStatementQuantity { get; set; }

    /// <summary>Slovak control-statement unit, for example kg, t, m, or ks.</summary>
    [JsonPropertyName("controlStatementUnit")]
    public string? ControlStatementUnit { get; set; }
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

    /// <summary>Peppol participant identifier (e.g. "0245:12345678").</summary>
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
    /// <summary>Canonical bare Peppol process URN. Null means the legacy profile 01.</summary>
    [JsonPropertyName("process_id")]
    public string? ProcessId { get; set; }

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
    /// <summary>Peppol process URN. Profile 01 is used when omitted.</summary>
    [JsonPropertyName("processId")]
    public string? ProcessId { get; set; }

    /// <summary>Business document type: invoice, credit_note, self_billing, or self_billing_credit_note.</summary>
    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    /// <summary>Peppol identifier of the receiver/counterparty (e.g. "0245:12345678").</summary>
    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    /// <summary>Self-billing alias for the supplier/Peppol recipient identifier.</summary>
    [JsonPropertyName("supplierPeppolId")]
    public string? SupplierPeppolId { get; set; }

    /// <summary>Invoice number (e.g. "FV-2026-001"). Auto-generated if not provided.</summary>
    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    /// <summary>Original invoice number corrected by a credit note.</summary>
    [JsonPropertyName("precedingInvoiceRef")]
    public string? PrecedingInvoiceRef { get; set; }

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

    /// <summary>Legal name of the receiver company. Required when sending structured JSON with Items.</summary>
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

    /// <summary>Receiver street and number.</summary>
    [JsonPropertyName("receiverStreet")]
    public string? ReceiverStreet { get; set; }

    /// <summary>Receiver city.</summary>
    [JsonPropertyName("receiverCity")]
    public string? ReceiverCity { get; set; }

    /// <summary>Receiver postal code.</summary>
    [JsonPropertyName("receiverPostalCode")]
    public string? ReceiverPostalCode { get; set; }

    /// <summary>Receiver's ISO 3166-1 alpha-2 country code (e.g. "SK").</summary>
    [JsonPropertyName("receiverCountry")]
    public string? ReceiverCountry { get; set; }

    /// <summary>Self-billing alias for the supplier/counterparty legal name.</summary>
    [JsonPropertyName("supplierName")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("supplierIco")]
    public string? SupplierIco { get; set; }

    [JsonPropertyName("supplierDic")]
    public string? SupplierDic { get; set; }

    [JsonPropertyName("supplierIcDph")]
    public string? SupplierIcDph { get; set; }

    [JsonPropertyName("supplierStreet")]
    public string? SupplierStreet { get; set; }

    [JsonPropertyName("supplierCity")]
    public string? SupplierCity { get; set; }

    [JsonPropertyName("supplierPostalCode")]
    public string? SupplierPostalCode { get; set; }

    [JsonPropertyName("supplierAddress")]
    public string? SupplierAddress { get; set; }

    [JsonPropertyName("supplierCountry")]
    public string? SupplierCountry { get; set; }

    /// <summary>Amount paid in advance. Do not combine with advance deduction lines.</summary>
    [JsonPropertyName("prepaidAmount")]
    public decimal? PrepaidAmount { get; set; }

    /// <summary>
    /// Structured settled prepayments on the final invoice. The API sums AmountWithVat
    /// into PrepaidAmount, reduces PayableAmount, and preserves references in the UBL note.
    /// </summary>
    [JsonPropertyName("prepayments")]
    public List<Prepayment>? Prepayments { get; set; }

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

/// <summary>Structured settled prepayment for final-invoice JSON mode.</summary>
public sealed class Prepayment
{
    /// <summary>Advance/prepayment invoice reference from the ERP.</summary>
    [JsonPropertyName("advanceInvoiceRef")]
    public string? AdvanceInvoiceRef { get; set; }

    /// <summary>Tax document number for the received advance payment.</summary>
    [JsonPropertyName("taxDocumentRef")]
    public string? TaxDocumentRef { get; set; }

    /// <summary>Settlement date in YYYY-MM-DD format.</summary>
    [JsonPropertyName("settlementDate")]
    public string? SettlementDate { get; set; }

    /// <summary>Settled amount without VAT.</summary>
    [JsonPropertyName("amountWithoutVat")]
    public decimal? AmountWithoutVat { get; set; }

    /// <summary>VAT amount from the settled prepayment.</summary>
    [JsonPropertyName("vatAmount")]
    public decimal? VatAmount { get; set; }

    /// <summary>Settled amount including VAT. Required by the API.</summary>
    [JsonPropertyName("amountWithVat")]
    public required decimal AmountWithVat { get; set; }

    /// <summary>VAT rate of the prepayment, when known.</summary>
    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; }

    /// <summary>Optional VAT category of the prepayment, for example S or AE.</summary>
    [JsonPropertyName("vatCategoryCode")]
    public string? VatCategoryCode { get; set; }
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

    /// <summary>Storecove-style alias for <see cref="DocumentId"/>.</summary>
    [JsonPropertyName("submissionId")]
    public string? SubmissionId { get; set; }

    /// <summary>Peppol AS4 message identifier for tracking the delivery.</summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = "";

    /// <summary>Initial delivery status (typically "sent" or "queued").</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>True only for HTTP 200 idempotent replay responses.</summary>
    [JsonPropertyName("duplicate")]
    public bool? Duplicate { get; set; }

    /// <summary>
    /// Hex-lowercase SHA-256 digest over the canonical UBL XML wire payload —
    /// lets the receiver verify the bytes off Peppol AS4 match what ePošťák
    /// logged at send time. Always present on 201 responses; absent only on
    /// <c>202 SENT_DB_PENDING</c> recoveries.
    /// </summary>
    [JsonPropertyName("payloadSha256")]
    public string? PayloadSha256 { get; set; }

    /// <summary>Partial-failure explanation when status is SENT_DB_PENDING.</summary>
    [JsonPropertyName("warning")]
    public string? Warning { get; set; }

    /// <summary>Convenience links for the submitted document.</summary>
    [JsonPropertyName("links")]
    public SendDocumentLinks? Links { get; set; }
}

public sealed class SendDocumentLinks
{
    [JsonPropertyName("document")] public string? Document { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("events")] public string? Events { get; set; }
    [JsonPropertyName("ubl")] public string? Ubl { get; set; }
    [JsonPropertyName("evidence")] public string? Evidence { get; set; }
    [JsonPropertyName("evidenceBundle")] public string? EvidenceBundle { get; set; }
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

    /// <summary>Peppol identifier of the receiver (e.g. "0245:12345678").</summary>
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

    /// <summary>Invoice response status from the receiver: one of AB, IP, UQ, CA, RE, AP, PD. Null if no response yet.</summary>
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

/// <summary>
/// Single item returned by <c>POST /api/v1/documents/status/batch</c>.
/// Missing or cross-tenant IDs are represented with <see cref="Error"/>
/// set to <c>not_found</c>.
/// </summary>
public sealed class DocumentStatusBatchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("senderPeppolId")]
    public string? SenderPeppolId { get; set; }

    [JsonPropertyName("receiverPeppolId")]
    public string? ReceiverPeppolId { get; set; }

    [JsonPropertyName("statusHistory")]
    public List<StatusHistoryEntry> StatusHistory { get; set; } = [];

    [JsonPropertyName("validationResult")]
    public Dictionary<string, object>? ValidationResult { get; set; }

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }

    [JsonPropertyName("invoiceResponseStatus")]
    public InvoiceResponseCode? InvoiceResponseStatus { get; set; }

    [JsonPropertyName("peppolMessageId")]
    public string? PeppolMessageId { get; set; }

    [JsonPropertyName("as4MessageId")]
    public string? As4MessageId { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }
}

/// <summary>Response from <c>POST /api/v1/documents/status/batch</c>.</summary>
public sealed class DocumentStatusBatchResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("found")]
    public int Found { get; set; }

    [JsonPropertyName("notFound")]
    public int NotFound { get; set; }

    [JsonPropertyName("results")]
    public List<DocumentStatusBatchResult> Results { get; set; } = [];
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
    /// <summary>Response status: one of AB, IP, UQ, CA, RE, AP, PD.</summary>
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
    /// <summary>
    /// Response status: one of <c>AB</c> (accepted billing), <c>IP</c> (in process),
    /// <c>UQ</c> (under query), <c>CA</c> (conditionally accepted), <c>RE</c> (rejected),
    /// <c>AP</c> (accepted), <c>PD</c> (paid).
    /// </summary>
    [JsonPropertyName("status")]
    public required InvoiceResponseCode Status { get; set; }

    /// <summary>Optional note explaining the response (max 500 chars).</summary>
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

    /// <summary>The response status that was sent (one of AB, IP, UQ, CA, RE, AP, PD).</summary>
    [JsonPropertyName("responseStatus")]
    public InvoiceResponseCode ResponseStatus { get; set; }

    /// <summary>Timestamp when the response was sent (ISO 8601).</summary>
    [JsonPropertyName("respondedAt")]
    public string RespondedAt { get; set; } = "";

    /// <summary>Peppol AS4 message identifier for tracking the dispatch. Null if not dispatched.</summary>
    [JsonPropertyName("peppolMessageId")]
    public string? PeppolMessageId { get; set; }

    /// <summary>Dispatch status: <c>sent</c> if transmitted, <c>failed_queued</c> if dispatch failed and will retry.</summary>
    [JsonPropertyName("dispatchStatus")]
    public string? DispatchStatus { get; set; }

    /// <summary>Dispatch error message when <see cref="DispatchStatus"/> is <c>failed_queued</c>.</summary>
    [JsonPropertyName("dispatchError")]
    public string? DispatchError { get; set; }
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
/// Request for a dry-run validation and Peppol routing check before sending.
/// </summary>
public sealed class PreflightRequest
{
    /// <summary>Peppol identifier of the receiver to check (e.g. "0245:12345678"). Required.</summary>
    [JsonPropertyName("receiverPeppolId")]
    public required string ReceiverPeppolId { get; set; }

    /// <summary>Optional Peppol document type ID. If omitted, BIS Billing 3.0 Invoice is used.</summary>
    [JsonPropertyName("documentTypeId")]
    public string? DocumentTypeId { get; set; }

    /// <summary>Optional Peppol process ID. If omitted, BIS Billing 3.0 is used.</summary>
    [JsonPropertyName("processId")]
    public string? ProcessId { get; set; }

    /// <summary>Optional raw UBL XML document to validate as part of the preflight check.</summary>
    [JsonPropertyName("ublDocument")]
    public string? UblDocument { get; set; }
}

/// <summary>
/// Result of a dry-run send check covering UBL validation, Peppol participant discovery,
/// and exact document/process routing capability.
/// </summary>
public sealed class PreflightResult
{
    /// <summary>Final preflight decision: <c>sendable</c>, <c>sendable_with_warnings</c>, <c>blocked</c>, or <c>retry_later</c>.</summary>
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "";

    /// <summary>Whether the receiver exists and advertises the requested document/process route.</summary>
    [JsonPropertyName("networkReady")]
    public bool NetworkReady { get; set; }

    /// <summary>UBL validation result, or null when no document was supplied or validation was inconclusive.</summary>
    [JsonPropertyName("valid")]
    public bool? Valid { get; set; }

    /// <summary>Whether the document can be sent according to all completed checks.</summary>
    [JsonPropertyName("canSend")]
    public bool CanSend { get; set; }

    /// <summary>Whether the participant lookup found the receiver.</summary>
    [JsonPropertyName("participantExists")]
    public bool ParticipantExists { get; set; }

    /// <summary>Blocking preflight findings.</summary>
    [JsonPropertyName("errors")]
    public List<PreflightIssue> Errors { get; set; } = [];

    /// <summary>Non-blocking or retryable preflight findings.</summary>
    [JsonPropertyName("warnings")]
    public List<PreflightIssue> Warnings { get; set; } = [];

    /// <summary>Individual validation, participant, and routing checks.</summary>
    [JsonPropertyName("checks")]
    public List<PreflightCheck> Checks { get; set; } = [];

    /// <summary>Resolved Peppol receiver metadata.</summary>
    [JsonPropertyName("recipient")]
    public PreflightRecipient? Recipient { get; set; }

    /// <summary>Document type, process, ruleset, and input mode used by the check.</summary>
    [JsonPropertyName("documentProfile")]
    public PreflightDocumentProfile? DocumentProfile { get; set; }

    /// <summary>Request trace and timing metadata.</summary>
    [JsonPropertyName("trace")]
    public PreflightTrace? Trace { get; set; }

    /// <summary>Compatibility participant-found flag returned by the API.</summary>
    [JsonPropertyName("recipientFound")]
    public bool RecipientFound { get; set; }

    /// <summary>Whether the receiver advertises the requested document type.</summary>
    [JsonPropertyName("recipientAcceptsDocumentType")]
    public bool RecipientAcceptsDocumentType { get; set; }

    /// <summary>Compatibility validation flag, or null when validation was skipped or inconclusive.</summary>
    [JsonPropertyName("validationPassed")]
    public bool? ValidationPassed { get; set; }

    /// <summary>Compatibility list containing validation error messages only.</summary>
    [JsonPropertyName("validationErrors")]
    public List<string> ValidationErrors { get; set; } = [];

    /// <summary>The resolved receiver Peppol ID. Use <see cref="Recipient"/> for the full result.</summary>
    [JsonIgnore]
    public string ReceiverPeppolId
    {
        get => Recipient?.PeppolId ?? "";
        set
        {
            Recipient ??= new PreflightRecipient();
            Recipient.PeppolId = value;
        }
    }

    /// <summary>Compatibility alias for <see cref="ParticipantExists"/>.</summary>
    [JsonIgnore]
    public bool Registered
    {
        get => ParticipantExists;
        set
        {
            ParticipantExists = value;
            RecipientFound = value;
        }
    }

    /// <summary>Compatibility alias for <see cref="RecipientAcceptsDocumentType"/>.</summary>
    [JsonIgnore]
    public bool SupportsDocumentType
    {
        get => RecipientAcceptsDocumentType;
        set => RecipientAcceptsDocumentType = value;
    }

    /// <summary>The resolved AS4 endpoint URL. Use <see cref="Recipient"/> for full access-point metadata.</summary>
    [JsonIgnore]
    public string? SmpUrl
    {
        get => Recipient?.AccessPoint?.Url;
        set
        {
            Recipient ??= new PreflightRecipient();
            Recipient.AccessPoint ??= new PeppolAccessPoint();
            Recipient.AccessPoint.Url = value;
        }
    }
}

/// <summary>A single finding produced by preflight validation or routing checks.</summary>
public sealed class PreflightIssue
{
    /// <summary>Finding category: validation, participant, routing, or system.</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    /// <summary>Stable machine-readable finding code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    /// <summary>Finding severity: error or warning.</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";

    /// <summary>Human-readable finding message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>Request field or document location associated with the finding, when available.</summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

/// <summary>Timing and status of one preflight layer.</summary>
public sealed class PreflightCheck
{
    /// <summary>Check name: validation, participant, or routing.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Check status: passed, failed, warning, or skipped.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>Elapsed check time in milliseconds.</summary>
    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    /// <summary>Optional status detail, for example why validation was skipped.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>Peppol receiver metadata resolved during preflight.</summary>
public sealed class PreflightRecipient
{
    /// <summary>Normalized Peppol participant ID.</summary>
    [JsonPropertyName("peppolId")]
    public string? PeppolId { get; set; }

    /// <summary>Peppol identifier scheme.</summary>
    [JsonPropertyName("scheme")]
    public string? Scheme { get; set; }

    /// <summary>Identifier value within the scheme.</summary>
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>Lookup source, such as <c>sml</c> or <c>internal</c>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>Resolved AS4 endpoint metadata.</summary>
    [JsonPropertyName("accessPoint")]
    public PeppolAccessPoint? AccessPoint { get; set; }

    /// <summary>Resolved endpoint certificate metadata.</summary>
    [JsonPropertyName("certificate")]
    public PeppolCertificateInfo? Certificate { get; set; }

    /// <summary>Document types advertised by the receiver.</summary>
    [JsonPropertyName("supportedDocumentTypes")]
    public List<string>? SupportedDocumentTypes { get; set; }
}

/// <summary>Document and process profile used by the preflight checks.</summary>
public sealed class PreflightDocumentProfile
{
    /// <summary>Peppol document type identifier.</summary>
    [JsonPropertyName("documentTypeId")]
    public string DocumentTypeId { get; set; } = "";

    /// <summary>Peppol process identifier.</summary>
    [JsonPropertyName("processId")]
    public string ProcessId { get; set; } = "";

    /// <summary>Validation ruleset name.</summary>
    [JsonPropertyName("ruleset")]
    public string Ruleset { get; set; } = "";

    /// <summary>Validation ruleset version.</summary>
    [JsonPropertyName("rulesetVersion")]
    public string RulesetVersion { get; set; } = "";

    /// <summary>Input mode: ubl, json, or none.</summary>
    [JsonPropertyName("inputMode")]
    public string InputMode { get; set; } = "";
}

/// <summary>Trace and timing metadata for a preflight request.</summary>
public sealed class PreflightTrace
{
    /// <summary>Request trace identifier.</summary>
    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = "";

    /// <summary>ISO 8601 timestamp when the check ran.</summary>
    [JsonPropertyName("checkedAt")]
    public string CheckedAt { get; set; } = "";

    /// <summary>Total preflight duration in milliseconds.</summary>
    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; }

    /// <summary>UBL validation duration in milliseconds.</summary>
    [JsonPropertyName("validationMs")]
    public int ValidationMs { get; set; }

    /// <summary>Participant lookup duration in milliseconds.</summary>
    [JsonPropertyName("participantLookupMs")]
    public int ParticipantLookupMs { get; set; }
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

// ---------------------------------------------------------------------------
// Batch send
// ---------------------------------------------------------------------------

/// <summary>
/// A single item in a batch send request. Wraps a <see cref="SendDocumentRequest"/>
/// and may include an optional idempotency key.
/// </summary>
public sealed class BatchSendItem
{
    /// <summary>The document to send (same format as single send). Required.</summary>
    [JsonPropertyName("document")]
    public required SendDocumentRequest Document { get; set; }

    /// <summary>
    /// Optional per-item idempotency key. If set, resubmissions with the same key
    /// return the original result instead of re-sending.
    /// </summary>
    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Request body for sending multiple documents in a single call via
/// <c>POST /documents/send/batch</c>.
/// </summary>
public sealed class BatchSendRequest
{
    /// <summary>Items to send, in order. Max 100 items per batch. Required.</summary>
    [JsonPropertyName("items")]
    public required List<BatchSendItem> Items { get; set; }
}

/// <summary>
/// Result of a single item in a batch send operation.
/// </summary>
public sealed class BatchSendItemResult
{
    /// <summary>Zero-based index of the item in the request.</summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>HTTP status code for this item (e.g. 202, 400, 422).</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Per-item result payload. On success, matches <see cref="SendDocumentResponse"/>.
    /// On failure, is an error body shape with <c>error.message</c> and <c>error.code</c>.
    /// Null if the batch item produced no body.
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; set; }
}

/// <summary>
/// Response from a batch send operation. Reports overall counts and a per-item
/// result in the same order as the request.
/// </summary>
public sealed class BatchSendResponse
{
    /// <summary>Total number of items in the batch.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Number of items that returned a 2xx status.</summary>
    [JsonPropertyName("succeeded")]
    public int Succeeded { get; set; }

    /// <summary>Number of items that returned a non-2xx status.</summary>
    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    /// <summary>Per-item results in request order.</summary>
    [JsonPropertyName("results")]
    public List<BatchSendItemResult> Results { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Parse (UBL XML -> structured JSON)
// ---------------------------------------------------------------------------

/// <summary>
/// Result of parsing a UBL XML invoice into structured JSON via
/// <c>POST /payloads/parse</c>. The shape matches the JSON side of
/// <see cref="ConvertResult"/> with <see cref="ConvertOutputFormat.Json"/>:
/// <see cref="Document"/> is a free-form object with fields such as
/// <c>invoiceNumber</c>, <c>issueDate</c>, <c>supplier</c>, <c>customer</c>,
/// <c>items</c>, and <c>totals</c>.
/// </summary>
public sealed class ParsedInvoice
{
    /// <summary>Parsed invoice fields as a generic JSON object (<see cref="JsonElement"/>).</summary>
    [JsonPropertyName("document")]
    public object Document { get; set; } = new();

    /// <summary>Non-fatal warnings emitted during parsing.</summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Mark (inbound document state)
// ---------------------------------------------------------------------------

/// <summary>
/// Request body for marking an inbound document's processing state via
/// <c>POST /documents/{id}/mark</c>.
/// </summary>
public sealed class MarkRequest
{
    /// <summary>
    /// Target state: one of <c>delivered</c>, <c>processed</c>, <c>failed</c>, <c>read</c>. Required.
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; set; }

    /// <summary>Optional free-text note (max 500 chars), e.g. failure reason.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Response from marking a document's processing state.
/// </summary>
public sealed class MarkResponse
{
    /// <summary>The document UUID that was marked.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>The state that was recorded.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    /// <summary>The document's overall status after applying the mark.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>ISO 8601 timestamp of the <c>delivered</c> mark. Null if not yet delivered.</summary>
    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    /// <summary>ISO 8601 timestamp of the <c>processed</c> mark. Null if not yet processed.</summary>
    [JsonPropertyName("acknowledgedAt")]
    public string? AcknowledgedAt { get; set; }

    /// <summary>ISO 8601 timestamp of the <c>read</c> mark. Null if not yet read.</summary>
    [JsonPropertyName("readAt")]
    public string? ReadAt { get; set; }
}

// ---------------------------------------------------------------------------
// Validation report (public /api/validate)
// ---------------------------------------------------------------------------

/// <summary>
/// A single validation finding (error or warning) within a <see cref="ValidationReport"/>.
/// </summary>
public sealed class ValidationFinding
{
    /// <summary>The rule identifier (e.g. <c>BR-01</c>, <c>SK-R-002</c>).</summary>
    [JsonPropertyName("rule")]
    public string Rule { get; set; } = "";

    /// <summary>Human-readable description of the finding.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>XPath or schema location where the finding was raised. Null if not provided.</summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    /// <summary>Severity: <c>error</c> or <c>warning</c>.</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";
}

/// <summary>
/// Result of a single validation layer (XSD / EN / Peppol).
/// </summary>
public sealed class ValidationLayerResult
{
    /// <summary>True if this layer produced no errors.</summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    /// <summary>Layer-specific error findings.</summary>
    [JsonPropertyName("errors")]
    public List<ValidationFinding> Errors { get; set; } = [];

    /// <summary>Layer-specific warning findings.</summary>
    [JsonPropertyName("warnings")]
    public List<ValidationFinding> Warnings { get; set; } = [];
}

/// <summary>
/// Per-layer validation results returned by <c>POST /api/validate</c>.
/// </summary>
public sealed class ValidationLayers
{
    /// <summary>UBL 2.1 XSD schema validation layer.</summary>
    [JsonPropertyName("xsd")]
    public ValidationLayerResult Xsd { get; set; } = new();

    /// <summary>EN 16931 core business rules layer.</summary>
    [JsonPropertyName("en")]
    public ValidationLayerResult En { get; set; } = new();

    /// <summary>Peppol BIS 3.0 country-specific rules layer.</summary>
    [JsonPropertyName("peppol")]
    public ValidationLayerResult Peppol { get; set; } = new();
}

/// <summary>
/// Aggregate error and warning counts for a <see cref="ValidationReport"/>.
/// </summary>
public sealed class ValidationSummary
{
    /// <summary>Total number of error-level findings.</summary>
    [JsonPropertyName("errors")]
    public int Errors { get; set; }

    /// <summary>Total number of warning-level findings.</summary>
    [JsonPropertyName("warnings")]
    public int Warnings { get; set; }
}

/// <summary>
/// Full 3-layer Peppol BIS 3.0 validation report returned by the public
/// <c>POST /api/validate</c> endpoint. Reports findings from UBL XSD, EN 16931,
/// and Peppol BIS 3.0 country-specific rules.
/// </summary>
public sealed class ValidationReport
{
    /// <summary>True if every layer passes (no errors).</summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>True if all Schematron layers (EN + Peppol) pass.</summary>
    [JsonPropertyName("schematronOk")]
    public bool SchematronOk { get; set; }

    /// <summary>Aggregate error and warning counts across all layers.</summary>
    [JsonPropertyName("summary")]
    public ValidationSummary Summary { get; set; } = new();

    /// <summary>Per-layer findings.</summary>
    [JsonPropertyName("layers")]
    public ValidationLayers Layers { get; set; } = new();

    /// <summary>Flat list of all error-level findings across layers.</summary>
    [JsonPropertyName("errors")]
    public List<ValidationFinding> Errors { get; set; } = [];

    /// <summary>Flat list of all warning-level findings across layers.</summary>
    [JsonPropertyName("warnings")]
    public List<ValidationFinding> Warnings { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Outbox
// ---------------------------------------------------------------------------

/// <summary>
/// Query parameters for listing outbound documents.
/// </summary>
public sealed class OutboxParams
{
    /// <summary>Number of documents to skip (default 0).</summary>
    public int? Offset { get; set; }
    /// <summary>Maximum number of documents to return (1-100, default 20).</summary>
    public int? Limit { get; set; }
    /// <summary>Filter by processing status.</summary>
    public string? Status { get; set; }
    /// <summary>Filter by Peppol AS4 message ID.</summary>
    public string? PeppolMessageId { get; set; }
    /// <summary>ISO 8601 timestamp -- only return documents created after this date.</summary>
    public string? Since { get; set; }
}

/// <summary>
/// Paginated list of outbound documents.
/// </summary>
public sealed class OutboxListResponse
{
    /// <summary>Array of outbound documents in the current page.</summary>
    [JsonPropertyName("documents")]
    public List<Document> Documents { get; set; } = [];

    /// <summary>Total number of documents matching the query.</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>The offset that was applied.</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>The limit that was applied.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }
}

// ---------------------------------------------------------------------------
// Invoice responses list
// ---------------------------------------------------------------------------

/// <summary>
/// A single Invoice Response record associated with a document.
/// </summary>
public sealed class InvoiceResponseItem
{
    /// <summary>Response UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Response status code (one of AB, IP, UQ, CA, RE, AP, PD).</summary>
    [JsonPropertyName("responseCode")]
    public InvoiceResponseCode ResponseCode { get; set; }

    /// <summary>Optional note accompanying the response.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>Peppol participant ID of the sender of this response.</summary>
    [JsonPropertyName("senderPeppolId")]
    public string SenderPeppolId { get; set; } = "";

    /// <summary>ISO 8601 timestamp when the response was created.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";
}

/// <summary>
/// Response from <c>GET /documents/{id}/responses</c>.
/// </summary>
public sealed class InvoiceResponsesListResponse
{
    /// <summary>Document UUID the responses belong to.</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>Array of Invoice Response records.</summary>
    [JsonPropertyName("responses")]
    public List<InvoiceResponseItem> Responses { get; set; } = [];
}

// ---------------------------------------------------------------------------
// Document events
// ---------------------------------------------------------------------------

/// <summary>
/// Query parameters for <c>GET /documents/{id}/events</c>.
/// </summary>
public sealed class DocumentEventsParams
{
    /// <summary>Maximum number of events to return (default 20).</summary>
    public int? Limit { get; set; }
    /// <summary>Cursor from the previous page for cursor-based pagination.</summary>
    public string? Cursor { get; set; }
}

/// <summary>
/// A single event in a document's audit trail.
/// </summary>
public sealed class DocumentEvent
{
    /// <summary>Canonical bare Peppol process URN for this document.</summary>
    [JsonPropertyName("process_id")]
    public string? ProcessId { get; set; }

    /// <summary>Event UUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Event type identifier (e.g. "status_changed", "respond_sent").</summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = "";

    /// <summary>Actor that triggered the event: "system", "user", or "api".</summary>
    [JsonPropertyName("actor")]
    public string Actor { get; set; } = "";

    /// <summary>Human-readable detail about the event, or null.</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>Arbitrary structured metadata attached to the event.</summary>
    [JsonPropertyName("meta")]
    public Dictionary<string, object> Meta { get; set; } = [];

    /// <summary>ISO 8601 timestamp when the event occurred.</summary>
    [JsonPropertyName("occurredAt")]
    public string OccurredAt { get; set; } = "";
}

/// <summary>
/// Response from <c>GET /documents/{id}/events</c>.
/// </summary>
public sealed class DocumentEventsResponse
{
    /// <summary>Canonical bare Peppol process URN for this document.</summary>
    [JsonPropertyName("process_id")]
    public string? ProcessId { get; set; }

    /// <summary>Document UUID the events belong to.</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>Ordered array of events for this document.</summary>
    [JsonPropertyName("events")]
    public List<DocumentEvent> Events { get; set; } = [];

    /// <summary>Cursor pagination metadata returned by the current API.</summary>
    [JsonPropertyName("pagination")]
    public DocumentEventsPagination Pagination { get; set; } = new();
}

public sealed class DocumentEventsPagination
{
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}
