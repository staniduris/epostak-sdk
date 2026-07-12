package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Request body for sending a document via Peppol.
 * <p>
 * Use either JSON mode (structured data with {@code items}) or XML mode
 * (pre-built UBL with {@code xml}). The {@code receiverPeppolId} is always
 * required; JSON mode also requires {@code receiverName} and {@code items}.
 *
 * <pre>{@code
 * // JSON mode
 * SendDocumentRequest req = SendDocumentRequest.builder("0245:12345678")
 *     .receiverName("Firma s.r.o.")
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
 * }</pre>
 */
public final class SendDocumentRequest {

    /** Receiver's Peppol participant ID (required), e.g. {@code "0245:12345678"}. */
    @SerializedName("receiverPeppolId")
    private final String receiverPeppolId;
    /** Invoice number, e.g. {@code "FV-2026-001"}. */
    @SerializedName("invoiceNumber")
    private final String invoiceNumber;
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
    /** Receiver company name. Required in JSON mode. */
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
    /** Amount paid in advance. Do not combine with advance deduction lines. */
    @SerializedName("prepaidAmount")
    private final Double prepaidAmount;
    /** Structured settled prepayments for final-invoice JSON mode. */
    private final List<Prepayment> prepayments;
    /** Line items for JSON mode. Mutually exclusive with {@code xml}. */
    private final List<LineItem> items;
    /** Invoice attachments (BG-24). JSON mode only; embedded as base64 into the generated UBL XML. */
    private final List<Attachment> attachments;
    /** Pre-built UBL XML for XML mode. Mutually exclusive with {@code items}. */
    private final String xml;

    private SendDocumentRequest(Builder builder) {
        this.receiverPeppolId = builder.receiverPeppolId;
        this.invoiceNumber = builder.invoiceNumber;
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
        this.prepaidAmount = builder.prepaidAmount;
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

    /** @return the receiver's Peppol participant ID */
    public String getReceiverPeppolId() { return receiverPeppolId; }
    /** @return the invoice number */
    public String getInvoiceNumber() { return invoiceNumber; }
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
    /** @return the prepaid amount */
    public Double getPrepaidAmount() { return prepaidAmount; }
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
     * @param vatCategoryCode UBL VAT category code, e.g. {@code "S"}, {@code "Z"}, or {@code "AE"}
     * @param vatCategory alias for {@code vatCategoryCode}
     * @param taxTreatment higher-level tax treatment mapped by the API to {@code vatCategoryCode}
     * @param deliveryDate line delivery date in {@code YYYY-MM-DD} format
     * @param lineType line type, e.g. {@code "standard"} or {@code "advance_deduction"}
     * @param advanceInvoiceReference advance invoice reference for deduction lines
     * @param customsTariffCode customs tariff / combined nomenclature code
     * @param commodityClassificationCode generic item classification code
     * @param commodityClassificationListId classification list identifier, e.g. {@code "HS"}
     * @param reverseChargeParagraphLetter domestic reverse-charge paragraph letter evidence
     * @param controlStatementType Slovak control-statement type, e.g. {@code "IO"} or {@code "MT"}
     * @param controlStatementQuantity Slovak control-statement quantity
     * @param controlStatementUnit Slovak control-statement unit, e.g. {@code "kg"}, {@code "t"}, {@code "m"}, or {@code "ks"}
     */
    public record LineItem(
            String description,
            double quantity,
            String unit,
            @SerializedName("unitPrice") double unitPrice,
            @SerializedName("vatRate") double vatRate,
            Double discount,
            @SerializedName("vatCategoryCode") String vatCategoryCode,
            @SerializedName("vatCategory") String vatCategory,
            @SerializedName("taxTreatment") String taxTreatment,
            @SerializedName("deliveryDate") String deliveryDate,
            @SerializedName("lineType") String lineType,
            @SerializedName("advanceInvoiceReference") String advanceInvoiceReference,
            @SerializedName("customsTariffCode") String customsTariffCode,
            @SerializedName("commodityClassificationCode") String commodityClassificationCode,
            @SerializedName("commodityClassificationListId") String commodityClassificationListId,
            @SerializedName("reverseChargeParagraphLetter") String reverseChargeParagraphLetter,
            @SerializedName("controlStatementType") String controlStatementType,
            @SerializedName("controlStatementQuantity") Double controlStatementQuantity,
            @SerializedName("controlStatementUnit") String controlStatementUnit
    ) {
        /**
         * Compatibility constructor with the original optional unit/discount fields.
         */
        public LineItem(String description, double quantity, String unit, double unitPrice, double vatRate, Double discount) {
            this(
                    description,
                    quantity,
                    unit,
                    unitPrice,
                    vatRate,
                    discount,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
            );
        }

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

    // -- prepayment record ----------------------------------------------------

    /**
     * Structured settled prepayment for final-invoice JSON mode.
     *
     * @param advanceInvoiceRef advance/prepayment invoice reference from the ERP
     * @param taxDocumentRef    tax document number for the received advance payment
     * @param settlementDate    settlement date in {@code YYYY-MM-DD} format
     * @param amountWithoutVat  settled amount without VAT, or {@code null}
     * @param vatAmount         VAT amount from the settled prepayment, or {@code null}
     * @param amountWithVat     settled amount including VAT
     * @param vatRate           VAT rate of the prepayment, or {@code null}
     * @param vatCategoryCode   optional VAT category of the prepayment
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

    // -- builder --------------------------------------------------------------

    /**
     * Builder for constructing a {@link SendDocumentRequest}.
     * <p>
     * The receiver Peppol ID is required and set via the constructor. JSON mode
     * also requires {@link #receiverName(String)} and {@link #items(List)}.
     */
    public static final class Builder {
        /** Receiver's Peppol participant ID (required). */
        private final String receiverPeppolId;
        /** Invoice number. */
        private String invoiceNumber;
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
        /** Receiver street and number. */
        private String receiverStreet;
        /** Receiver city. */
        private String receiverCity;
        /** Receiver postal code. */
        private String receiverPostalCode;
        /** Receiver country code. */
        private String receiverCountry;
        /** Amount paid in advance. */
        private Double prepaidAmount;
        /** Structured settled prepayments for final invoices. */
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

        /** @param invoiceNumber the invoice number, e.g. {@code "FV-2026-001"} @return this builder */
        public Builder invoiceNumber(String invoiceNumber) { this.invoiceNumber = invoiceNumber; return this; }
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
        /** @param receiverStreet the receiver street and number @return this builder */
        public Builder receiverStreet(String receiverStreet) { this.receiverStreet = receiverStreet; return this; }
        /** @param receiverCity the receiver city @return this builder */
        public Builder receiverCity(String receiverCity) { this.receiverCity = receiverCity; return this; }
        /** @param receiverPostalCode the receiver postal code @return this builder */
        public Builder receiverPostalCode(String receiverPostalCode) { this.receiverPostalCode = receiverPostalCode; return this; }
        /** @param receiverCountry the receiver's country code (ISO 3166-1 alpha-2) @return this builder */
        public Builder receiverCountry(String receiverCountry) { this.receiverCountry = receiverCountry; return this; }
        /** @param prepaidAmount amount paid in advance @return this builder */
        public Builder prepaidAmount(Double prepaidAmount) { this.prepaidAmount = prepaidAmount; return this; }
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
