package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Request body for sending a document via Peppol.
 * <p>
 * Use either JSON mode (structured data with {@code items}) or XML mode
 * (pre-built UBL with {@code xml}). JSON mode supports {@code invoice},
 * {@code credit_note}, {@code self_billing}, and
 * {@code self_billing_credit_note}. XML mode requires {@code receiverPeppolId}.
 *
 * <pre>{@code
 * // JSON mode
 * SendDocumentRequest req = SendDocumentRequest.builder("0245:12345678")
 *     .invoiceNumber("FV-2026-001")
 *     .issueDate("2026-04-04")
 *     .dueDate("2026-04-18")
 *     .currency("EUR")
 *     .items(List.of(new SendDocumentRequest.LineItem("Service", 1, 100.0, 23)))
 *     .build();
 *
 * // XML mode
 * SendDocumentRequest xmlReq = SendDocumentRequest.builder("0245:12345678")
 *     .xml(ublXmlString)
 *     .build();
 *
 * // Self-billing JSON mode
 * SendDocumentRequest selfBilling = SendDocumentRequest.builder()
 *     .documentType("self_billing")
 *     .supplierPeppolId("0245:2123038963")
 *     .supplierName("Dodavatel s.r.o.")
 *     .items(List.of(new SendDocumentRequest.LineItem("Dodany material", 100, 25.0, 23)))
 *     .build();
 * }</pre>
 */
public final class SendDocumentRequest {

    /** Business JSON document type. Defaults to {@code invoice}. */
    @SerializedName("documentType")
    private final String documentType;
    /** Receiver's Peppol participant ID, e.g. {@code "0245:12345678"}. */
    @SerializedName("receiverPeppolId")
    private final String receiverPeppolId;
    /** Self-billing alias for the supplier/Peppol receiver. */
    @SerializedName("supplierPeppolId")
    private final String supplierPeppolId;
    /** Invoice number, e.g. {@code "FV-2026-001"}. */
    @SerializedName("invoiceNumber")
    private final String invoiceNumber;
    /** Original invoice number for credit notes. */
    @SerializedName("precedingInvoiceRef")
    private final String precedingInvoiceRef;
    /** Issue date in ISO 8601 format (YYYY-MM-DD). */
    @SerializedName("issueDate")
    private final String issueDate;
    /** Due date in ISO 8601 format (YYYY-MM-DD). */
    @SerializedName("dueDate")
    private final String dueDate;
    /** Currency code, e.g. {@code "EUR"}. Defaults to {@code "EUR"} if not set. */
    private final String currency;
    /** Optional note / payment instructions. */
    private final String note;
    /** IBAN for bank transfer payment. */
    private final String iban;
    /** Payment method code, e.g. {@code "30"} for bank transfer. */
    @SerializedName("paymentMethod")
    private final String paymentMethod;
    /** Slovak variable symbol for payment matching. */
    @SerializedName("variableSymbol")
    private final String variableSymbol;
    /** Buyer reference (purchase order number, contract ID, etc.). */
    @SerializedName("buyerReference")
    private final String buyerReference;
    /** Receiver company name (optional, auto-resolved from Peppol ID). */
    @SerializedName("receiverName")
    private final String receiverName;
    /** Receiver ICO (company registration number). */
    @SerializedName("receiverIco")
    private final String receiverIco;
    /** Receiver DIC (tax ID). */
    @SerializedName("receiverDic")
    private final String receiverDic;
    /** Receiver IC DPH (VAT registration number). */
    @SerializedName("receiverIcDph")
    private final String receiverIcDph;
    /** Receiver postal address (street, city, zip). */
    @SerializedName("receiverAddress")
    private final String receiverAddress;
    /** Receiver street and number. */
    @SerializedName("receiverStreet")
    private final String receiverStreet;
    /** Receiver city. */
    @SerializedName("receiverCity")
    private final String receiverCity;
    /** Receiver postal code. */
    @SerializedName("receiverPostalCode")
    private final String receiverPostalCode;
    /** Receiver country code (ISO 3166-1 alpha-2). */
    @SerializedName("receiverCountry")
    private final String receiverCountry;
    @SerializedName("supplierName")
    private final String supplierName;
    @SerializedName("supplierIco")
    private final String supplierIco;
    @SerializedName("supplierDic")
    private final String supplierDic;
    @SerializedName("supplierIcDph")
    private final String supplierIcDph;
    @SerializedName("supplierStreet")
    private final String supplierStreet;
    @SerializedName("supplierCity")
    private final String supplierCity;
    @SerializedName("supplierPostalCode")
    private final String supplierPostalCode;
    @SerializedName("supplierAddress")
    private final String supplierAddress;
    @SerializedName("supplierCountry")
    private final String supplierCountry;
    /** Structured settled prepayments for final-invoice JSON mode. */
    private final List<Prepayment> prepayments;
    /** Line items for JSON mode. Mutually exclusive with {@code xml}. */
    private final List<LineItem> items;
    /** Invoice attachments (BG-24). JSON mode only; embedded as base64 into the generated UBL XML. */
    private final List<Attachment> attachments;
    /** Pre-built UBL XML for XML mode. Mutually exclusive with {@code items}. */
    private final String xml;

    private SendDocumentRequest(Builder builder) {
        this.documentType = builder.documentType;
        this.receiverPeppolId = builder.receiverPeppolId;
        this.supplierPeppolId = builder.supplierPeppolId;
        this.invoiceNumber = builder.invoiceNumber;
        this.precedingInvoiceRef = builder.precedingInvoiceRef;
        this.issueDate = builder.issueDate;
        this.dueDate = builder.dueDate;
        this.currency = builder.currency;
        this.note = builder.note;
        this.iban = builder.iban;
        this.paymentMethod = builder.paymentMethod;
        this.variableSymbol = builder.variableSymbol;
        this.buyerReference = builder.buyerReference;
        this.receiverName = builder.receiverName;
        this.receiverIco = builder.receiverIco;
        this.receiverDic = builder.receiverDic;
        this.receiverIcDph = builder.receiverIcDph;
        this.receiverAddress = builder.receiverAddress;
        this.receiverStreet = builder.receiverStreet;
        this.receiverCity = builder.receiverCity;
        this.receiverPostalCode = builder.receiverPostalCode;
        this.receiverCountry = builder.receiverCountry;
        this.supplierName = builder.supplierName;
        this.supplierIco = builder.supplierIco;
        this.supplierDic = builder.supplierDic;
        this.supplierIcDph = builder.supplierIcDph;
        this.supplierStreet = builder.supplierStreet;
        this.supplierCity = builder.supplierCity;
        this.supplierPostalCode = builder.supplierPostalCode;
        this.supplierAddress = builder.supplierAddress;
        this.supplierCountry = builder.supplierCountry;
        this.prepayments = builder.prepayments;
        this.items = builder.items;
        this.attachments = builder.attachments;
        this.xml = builder.xml;
    }

    /**
     * Create a new builder for a send document request.
     *
     * @param receiverPeppolId the receiver's Peppol ID (required), e.g. {@code "0245:12345678"}
     * @return a new builder instance
     */
    public static Builder builder(String receiverPeppolId) {
        return new Builder(receiverPeppolId);
    }

    /**
     * Create a builder without a receiver Peppol ID. Use this for self-billing
     * JSON payloads that set {@code supplierPeppolId} instead.
     *
     * @return a new builder instance
     */
    public static Builder builder() {
        return new Builder(null);
    }

    /** @return the JSON document type */
    public String getDocumentType() { return documentType; }
    /** @return the receiver's Peppol participant ID */
    public String getReceiverPeppolId() { return receiverPeppolId; }
    /** @return self-billing supplier Peppol ID */
    public String getSupplierPeppolId() { return supplierPeppolId; }
    /** @return the invoice number */
    public String getInvoiceNumber() { return invoiceNumber; }
    /** @return original invoice reference for credit notes */
    public String getPrecedingInvoiceRef() { return precedingInvoiceRef; }
    /** @return the issue date in ISO 8601 format */
    public String getIssueDate() { return issueDate; }
    /** @return the due date in ISO 8601 format */
    public String getDueDate() { return dueDate; }
    /** @return the currency code */
    public String getCurrency() { return currency; }
    /** @return the optional note */
    public String getNote() { return note; }
    /** @return the IBAN for payment */
    public String getIban() { return iban; }
    /** @return the payment method code */
    public String getPaymentMethod() { return paymentMethod; }
    /** @return the Slovak variable symbol */
    public String getVariableSymbol() { return variableSymbol; }
    /** @return the buyer reference */
    public String getBuyerReference() { return buyerReference; }
    /** @return the receiver company name */
    public String getReceiverName() { return receiverName; }
    /** @return the receiver ICO */
    public String getReceiverIco() { return receiverIco; }
    /** @return the receiver DIC */
    public String getReceiverDic() { return receiverDic; }
    /** @return the receiver IC DPH */
    public String getReceiverIcDph() { return receiverIcDph; }
    /** @return the receiver postal address */
    public String getReceiverAddress() { return receiverAddress; }
    /** @return the receiver street */
    public String getReceiverStreet() { return receiverStreet; }
    /** @return the receiver city */
    public String getReceiverCity() { return receiverCity; }
    /** @return the receiver postal code */
    public String getReceiverPostalCode() { return receiverPostalCode; }
    /** @return the receiver country code */
    public String getReceiverCountry() { return receiverCountry; }
    /** @return self-billing supplier name */
    public String getSupplierName() { return supplierName; }
    /** @return self-billing supplier ICO */
    public String getSupplierIco() { return supplierIco; }
    /** @return self-billing supplier DIC */
    public String getSupplierDic() { return supplierDic; }
    /** @return self-billing supplier IC DPH */
    public String getSupplierIcDph() { return supplierIcDph; }
    /** @return self-billing supplier street */
    public String getSupplierStreet() { return supplierStreet; }
    /** @return self-billing supplier city */
    public String getSupplierCity() { return supplierCity; }
    /** @return self-billing supplier postal code */
    public String getSupplierPostalCode() { return supplierPostalCode; }
    /** @return self-billing supplier address */
    public String getSupplierAddress() { return supplierAddress; }
    /** @return self-billing supplier country */
    public String getSupplierCountry() { return supplierCountry; }
    /** @return structured settled prepayments */
    public List<Prepayment> getPrepayments() { return prepayments; }
    /** @return the line items (JSON mode) */
    public List<LineItem> getItems() { return items; }
    /** @return the attachments (JSON mode, BG-24) */
    public List<Attachment> getAttachments() { return attachments; }
    /** @return the pre-built UBL XML (XML mode) */
    public String getXml() { return xml; }

    // -- attachment record ----------------------------------------------------

    /**
     * An invoice attachment (BG-24) embedded as base64 into the generated UBL XML.
     * <p>
     * MIME type is verified by magic-byte sniffing server-side; the declared
     * {@code mimeType} must match the actual file content, or the request is
     * rejected with {@code VALIDATION_ERROR}.
     * <p>
     * Limits: max 20 attachments per invoice, 10 MB per file, 15 MB aggregated.
     *
     * @param fileName    original file name (max 255 chars)
     * @param mimeType    one of {@code application/pdf}, {@code image/png},
     *                    {@code image/jpeg}, {@code text/csv},
     *                    {@code application/vnd.openxmlformats-officedocument.spreadsheetml.sheet},
     *                    {@code application/vnd.oasis.opendocument.spreadsheet}
     * @param content     base64-encoded file content (no {@code data:} prefix), max 10 MB decoded
     * @param description optional short description (max 100 chars), or {@code null}
     */
    public record Attachment(
            @SerializedName("fileName") String fileName,
            @SerializedName("mimeType") String mimeType,
            String content,
            String description
    ) {
        /**
         * Convenience constructor without description.
         *
         * @param fileName original file name
         * @param mimeType allowed MIME type (see {@link Attachment})
         * @param content  base64-encoded file content
         */
        public Attachment(String fileName, String mimeType, String content) {
            this(fileName, mimeType, content, null);
        }
    }

    // -- prepayment record -----------------------------------------------------

    /**
     * Structured settled prepayment for final-invoice JSON mode.
     *
     * @param advanceInvoiceRef advance/prepayment invoice reference from the ERP
     * @param taxDocumentRef    tax document number for the received advance payment
     * @param settlementDate    settlement date in YYYY-MM-DD format
     * @param amountWithoutVat  settled amount without VAT, or {@code null}
     * @param vatAmount         VAT amount from the settled prepayment, or {@code null}
     * @param amountWithVat     settled amount including VAT; summed into prepaidAmount
     * @param vatRate           VAT rate of the prepayment, or {@code null}
     * @param vatCategoryCode   optional VAT category, e.g. {@code "S"} or {@code "AE"}
     */
    public record Prepayment(
            @SerializedName("advanceInvoiceRef") String advanceInvoiceRef,
            @SerializedName("taxDocumentRef") String taxDocumentRef,
            @SerializedName("settlementDate") String settlementDate,
            @SerializedName("amountWithoutVat") Double amountWithoutVat,
            @SerializedName("vatAmount") Double vatAmount,
            @SerializedName("amountWithVat") double amountWithVat,
            @SerializedName("vatRate") Double vatRate,
            @SerializedName("vatCategoryCode") String vatCategoryCode
    ) {
        public Prepayment(String advanceInvoiceRef, String taxDocumentRef, String settlementDate, double amountWithVat) {
            this(advanceInvoiceRef, taxDocumentRef, settlementDate, null, null, amountWithVat, null, null);
        }
    }

    // -- line item record -----------------------------------------------------

    /**
     * A single line item on the document being sent.
     *
     * @param description item description text
     * @param quantity    item quantity
     * @param unit        unit of measure (e.g. {@code "EA"}, {@code "HUR"}), or {@code null} for default
     * @param unitPrice   price per unit excluding VAT
     * @param vatRate     VAT rate as a percentage (e.g. {@code 23.0})
     * @param discount    optional discount percentage, or {@code null}
     */
    public record LineItem(
            String description,
            double quantity,
            String unit,
            @SerializedName("unitPrice") double unitPrice,
            @SerializedName("vatRate") double vatRate,
            Double discount
    ) {
        /**
         * Convenience constructor without optional fields (unit defaults to {@code null},
         * no discount).
         *
         * @param description item description text
         * @param quantity    item quantity
         * @param unitPrice   price per unit excluding VAT
         * @param vatRate     VAT rate as a percentage (e.g. {@code 23.0})
         */
        public LineItem(String description, double quantity, double unitPrice, double vatRate) {
            this(description, quantity, null, unitPrice, vatRate, null);
        }
    }

    // -- builder --------------------------------------------------------------

    /**
     * Builder for constructing a {@link SendDocumentRequest}.
     * <p>
     * The receiver Peppol ID is required and set via the constructor. All other
     * fields are optional.
     */
    public static final class Builder {
        /** JSON document type. */
        private String documentType;
        /** Receiver's Peppol participant ID. */
        private String receiverPeppolId;
        /** Self-billing supplier Peppol ID. */
        private String supplierPeppolId;
        /** Invoice number. */
        private String invoiceNumber;
        /** Original invoice reference for credit notes. */
        private String precedingInvoiceRef;
        /** Issue date (YYYY-MM-DD). */
        private String issueDate;
        /** Due date (YYYY-MM-DD). */
        private String dueDate;
        /** Currency code (e.g. "EUR"). */
        private String currency;
        /** Optional note. */
        private String note;
        /** IBAN for bank transfer. */
        private String iban;
        /** Payment method code. */
        private String paymentMethod;
        /** Slovak variable symbol. */
        private String variableSymbol;
        /** Buyer reference. */
        private String buyerReference;
        /** Receiver company name. */
        private String receiverName;
        /** Receiver ICO. */
        private String receiverIco;
        /** Receiver DIC. */
        private String receiverDic;
        /** Receiver IC DPH. */
        private String receiverIcDph;
        /** Receiver postal address. */
        private String receiverAddress;
        /** Receiver street. */
        private String receiverStreet;
        /** Receiver city. */
        private String receiverCity;
        /** Receiver postal code. */
        private String receiverPostalCode;
        /** Receiver country code. */
        private String receiverCountry;
        private String supplierName;
        private String supplierIco;
        private String supplierDic;
        private String supplierIcDph;
        private String supplierStreet;
        private String supplierCity;
        private String supplierPostalCode;
        private String supplierAddress;
        private String supplierCountry;
        private List<Prepayment> prepayments;
        /** Line items for JSON mode. */
        private List<LineItem> items;
        /** Invoice attachments (BG-24), JSON mode only. */
        private List<Attachment> attachments;
        /** Pre-built UBL XML for XML mode. */
        private String xml;

        private Builder(String receiverPeppolId) {
            this.receiverPeppolId = receiverPeppolId;
        }

        /** @param documentType invoice, credit_note, self_billing, or self_billing_credit_note @return this builder */
        public Builder documentType(String documentType) { this.documentType = documentType; return this; }
        /** @param receiverPeppolId the receiver Peppol ID @return this builder */
        public Builder receiverPeppolId(String receiverPeppolId) { this.receiverPeppolId = receiverPeppolId; return this; }
        /** @param supplierPeppolId self-billing supplier Peppol ID @return this builder */
        public Builder supplierPeppolId(String supplierPeppolId) { this.supplierPeppolId = supplierPeppolId; return this; }
        /** @param invoiceNumber the invoice number, e.g. {@code "FV-2026-001"} @return this builder */
        public Builder invoiceNumber(String invoiceNumber) { this.invoiceNumber = invoiceNumber; return this; }
        /** @param precedingInvoiceRef original invoice number for credit-note document types @return this builder */
        public Builder precedingInvoiceRef(String precedingInvoiceRef) { this.precedingInvoiceRef = precedingInvoiceRef; return this; }
        /** @param issueDate the issue date in YYYY-MM-DD format @return this builder */
        public Builder issueDate(String issueDate) { this.issueDate = issueDate; return this; }
        /** @param dueDate the due date in YYYY-MM-DD format @return this builder */
        public Builder dueDate(String dueDate) { this.dueDate = dueDate; return this; }
        /** @param currency the ISO 4217 currency code, e.g. {@code "EUR"} @return this builder */
        public Builder currency(String currency) { this.currency = currency; return this; }
        /** @param note optional note or payment instructions @return this builder */
        public Builder note(String note) { this.note = note; return this; }
        /** @param iban IBAN for bank transfer payment @return this builder */
        public Builder iban(String iban) { this.iban = iban; return this; }
        /** @param paymentMethod payment method code, e.g. {@code "30"} for bank transfer @return this builder */
        public Builder paymentMethod(String paymentMethod) { this.paymentMethod = paymentMethod; return this; }
        /** @param variableSymbol Slovak variable symbol for payment matching @return this builder */
        public Builder variableSymbol(String variableSymbol) { this.variableSymbol = variableSymbol; return this; }
        /** @param buyerReference buyer reference (PO number, contract ID, etc.) @return this builder */
        public Builder buyerReference(String buyerReference) { this.buyerReference = buyerReference; return this; }
        /** @param receiverName the receiver company name @return this builder */
        public Builder receiverName(String receiverName) { this.receiverName = receiverName; return this; }
        /** @param receiverIco the receiver's Slovak ICO @return this builder */
        public Builder receiverIco(String receiverIco) { this.receiverIco = receiverIco; return this; }
        /** @param receiverDic the receiver's DIC (tax ID) @return this builder */
        public Builder receiverDic(String receiverDic) { this.receiverDic = receiverDic; return this; }
        /** @param receiverIcDph the receiver's IC DPH (VAT number) @return this builder */
        public Builder receiverIcDph(String receiverIcDph) { this.receiverIcDph = receiverIcDph; return this; }
        /** @param receiverAddress the receiver's postal address @return this builder */
        public Builder receiverAddress(String receiverAddress) { this.receiverAddress = receiverAddress; return this; }
        /** @param receiverStreet the receiver's street and number @return this builder */
        public Builder receiverStreet(String receiverStreet) { this.receiverStreet = receiverStreet; return this; }
        /** @param receiverCity the receiver's city @return this builder */
        public Builder receiverCity(String receiverCity) { this.receiverCity = receiverCity; return this; }
        /** @param receiverPostalCode the receiver's postal code @return this builder */
        public Builder receiverPostalCode(String receiverPostalCode) { this.receiverPostalCode = receiverPostalCode; return this; }
        /** @param receiverCountry the receiver's country code (ISO 3166-1 alpha-2) @return this builder */
        public Builder receiverCountry(String receiverCountry) { this.receiverCountry = receiverCountry; return this; }
        /** @param supplierName self-billing supplier name @return this builder */
        public Builder supplierName(String supplierName) { this.supplierName = supplierName; return this; }
        /** @param supplierIco self-billing supplier ICO @return this builder */
        public Builder supplierIco(String supplierIco) { this.supplierIco = supplierIco; return this; }
        /** @param supplierDic self-billing supplier DIC @return this builder */
        public Builder supplierDic(String supplierDic) { this.supplierDic = supplierDic; return this; }
        /** @param supplierIcDph self-billing supplier IC DPH @return this builder */
        public Builder supplierIcDph(String supplierIcDph) { this.supplierIcDph = supplierIcDph; return this; }
        /** @param supplierStreet self-billing supplier street @return this builder */
        public Builder supplierStreet(String supplierStreet) { this.supplierStreet = supplierStreet; return this; }
        /** @param supplierCity self-billing supplier city @return this builder */
        public Builder supplierCity(String supplierCity) { this.supplierCity = supplierCity; return this; }
        /** @param supplierPostalCode self-billing supplier postal code @return this builder */
        public Builder supplierPostalCode(String supplierPostalCode) { this.supplierPostalCode = supplierPostalCode; return this; }
        /** @param supplierAddress self-billing supplier address @return this builder */
        public Builder supplierAddress(String supplierAddress) { this.supplierAddress = supplierAddress; return this; }
        /** @param supplierCountry self-billing supplier country @return this builder */
        public Builder supplierCountry(String supplierCountry) { this.supplierCountry = supplierCountry; return this; }
        /** @param prepayments structured settled prepayments for final invoices @return this builder */
        public Builder prepayments(List<Prepayment> prepayments) { this.prepayments = prepayments; return this; }
        /** @param items the line items (JSON mode, mutually exclusive with XML) @return this builder */
        public Builder items(List<LineItem> items) { this.items = items; return this; }
        /**
         * Invoice attachments (BG-24), JSON mode only. Embedded into the generated
         * UBL XML as base64 via {@code AdditionalDocumentReference} /
         * {@code EmbeddedDocumentBinaryObject}, so the receiver sees them inline.
         * Limits: max 20 files, 10 MB each, 15 MB total.
         *
         * @param attachments list of attachments to embed
         * @return this builder
         */
        public Builder attachments(List<Attachment> attachments) { this.attachments = attachments; return this; }
        /** @param xml the pre-built UBL XML (XML mode, mutually exclusive with items) @return this builder */
        public Builder xml(String xml) { this.xml = xml; return this; }

        /**
         * Build the send document request.
         *
         * @return the constructed request
         */
        public SendDocumentRequest build() {
            return new SendDocumentRequest(this);
        }
    }
}
