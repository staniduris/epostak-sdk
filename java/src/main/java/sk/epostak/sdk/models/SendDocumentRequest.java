package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Request body for sending a document via Peppol.
 * <p>
 * Use either JSON mode (structured data with {@code items}) or XML mode
 * (pre-built UBL with {@code xml}). The {@code receiverPeppolId} is always required.
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
 * }</pre>
 */
public final class SendDocumentRequest {

    /** Receiver's Peppol participant ID (required), e.g. {@code "0245:12345678"}. */
    @SerializedName("receiver_peppol_id")
    private final String receiverPeppolId;
    /** Invoice number, e.g. {@code "FV-2026-001"}. */
    @SerializedName("invoice_number")
    private final String invoiceNumber;
    /** Issue date in ISO 8601 format (YYYY-MM-DD). */
    @SerializedName("issue_date")
    private final String issueDate;
    /** Due date in ISO 8601 format (YYYY-MM-DD). */
    @SerializedName("due_date")
    private final String dueDate;
    /** Currency code, e.g. {@code "EUR"}. Defaults to {@code "EUR"} if not set. */
    private final String currency;
    /** Optional note / payment instructions. */
    private final String note;
    /** IBAN for bank transfer payment. */
    private final String iban;
    /** Payment method code, e.g. {@code "30"} for bank transfer. */
    @SerializedName("payment_method")
    private final String paymentMethod;
    /** Slovak variable symbol for payment matching. */
    @SerializedName("variable_symbol")
    private final String variableSymbol;
    /** Buyer reference (purchase order number, contract ID, etc.). */
    @SerializedName("buyer_reference")
    private final String buyerReference;
    /** Receiver company name (optional, auto-resolved from Peppol ID). */
    @SerializedName("receiver_name")
    private final String receiverName;
    /** Receiver ICO (company registration number). */
    @SerializedName("receiver_ico")
    private final String receiverIco;
    /** Receiver DIC (tax ID). */
    @SerializedName("receiver_dic")
    private final String receiverDic;
    /** Receiver IC DPH (VAT registration number). */
    @SerializedName("receiver_ic_dph")
    private final String receiverIcDph;
    /** Receiver postal address (street, city, zip). */
    @SerializedName("receiver_address")
    private final String receiverAddress;
    /** Receiver country code (ISO 3166-1 alpha-2). */
    @SerializedName("receiver_country")
    private final String receiverCountry;
    /** Line items for JSON mode. Mutually exclusive with {@code xml}. */
    private final List<LineItem> items;
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
        this.receiverCountry = builder.receiverCountry;
        this.items = builder.items;
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
    /** @return the receiver country code */
    public String getReceiverCountry() { return receiverCountry; }
    /** @return the line items (JSON mode) */
    public List<LineItem> getItems() { return items; }
    /** @return the pre-built UBL XML (XML mode) */
    public String getXml() { return xml; }

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
            @SerializedName("unit_price") double unitPrice,
            @SerializedName("vat_rate") double vatRate,
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
        /** Receiver country code. */
        private String receiverCountry;
        /** Line items for JSON mode. */
        private List<LineItem> items;
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
        /** @param receiverCountry the receiver's country code (ISO 3166-1 alpha-2) @return this builder */
        public Builder receiverCountry(String receiverCountry) { this.receiverCountry = receiverCountry; return this; }
        /** @param items the line items (JSON mode, mutually exclusive with XML) @return this builder */
        public Builder items(List<LineItem> items) { this.items = items; return this; }
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
