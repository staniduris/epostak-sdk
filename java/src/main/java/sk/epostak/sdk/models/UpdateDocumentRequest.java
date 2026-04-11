package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Request body for updating a draft document. All fields are optional --
 * only non-null fields will be updated.
 *
 * <pre>{@code
 * Document updated = client.documents().update("doc_uuid",
 *     UpdateDocumentRequest.builder()
 *         .dueDate("2026-05-01")
 *         .note("Updated payment terms")
 *         .build());
 * }</pre>
 */
public final class UpdateDocumentRequest {

    /** Invoice number. */
    @SerializedName("invoice_number")
    private final String invoiceNumber;
    /** Issue date (YYYY-MM-DD). */
    @SerializedName("issue_date")
    private final String issueDate;
    /** Due date (YYYY-MM-DD). */
    @SerializedName("due_date")
    private final String dueDate;
    /** Currency code (e.g. {@code "EUR"}). */
    private final String currency;
    /** Optional note or payment instructions. */
    private final String note;
    /** IBAN for bank transfer. */
    private final String iban;
    /** Slovak variable symbol. */
    @SerializedName("variable_symbol")
    private final String variableSymbol;
    /** Buyer reference. */
    @SerializedName("buyer_reference")
    private final String buyerReference;
    /** Receiver company name. */
    @SerializedName("receiver_name")
    private final String receiverName;
    /** Receiver ICO. */
    @SerializedName("receiver_ico")
    private final String receiverIco;
    /** Receiver DIC. */
    @SerializedName("receiver_dic")
    private final String receiverDic;
    /** Receiver IC DPH. */
    @SerializedName("receiver_ic_dph")
    private final String receiverIcDph;
    /** Receiver postal address. */
    @SerializedName("receiver_address")
    private final String receiverAddress;
    /** Receiver country code. */
    @SerializedName("receiver_country")
    private final String receiverCountry;
    /** Receiver Peppol ID. */
    @SerializedName("receiver_peppol_id")
    private final String receiverPeppolId;
    /** Replacement line items. */
    private final List<SendDocumentRequest.LineItem> items;

    private UpdateDocumentRequest(Builder builder) {
        this.invoiceNumber = builder.invoiceNumber;
        this.issueDate = builder.issueDate;
        this.dueDate = builder.dueDate;
        this.currency = builder.currency;
        this.note = builder.note;
        this.iban = builder.iban;
        this.variableSymbol = builder.variableSymbol;
        this.buyerReference = builder.buyerReference;
        this.receiverName = builder.receiverName;
        this.receiverIco = builder.receiverIco;
        this.receiverDic = builder.receiverDic;
        this.receiverIcDph = builder.receiverIcDph;
        this.receiverAddress = builder.receiverAddress;
        this.receiverCountry = builder.receiverCountry;
        this.receiverPeppolId = builder.receiverPeppolId;
        this.items = builder.items;
    }

    /**
     * Create a new builder for an update document request.
     *
     * @return a new builder instance
     */
    public static Builder builder() {
        return new Builder();
    }

    /**
     * Builder for constructing an {@link UpdateDocumentRequest}.
     * All fields are optional -- only set the fields you want to update.
     */
    public static final class Builder {
        /** Invoice number. */
        private String invoiceNumber;
        /** Issue date (YYYY-MM-DD). */
        private String issueDate;
        /** Due date (YYYY-MM-DD). */
        private String dueDate;
        /** Currency code. */
        private String currency;
        /** Optional note. */
        private String note;
        /** IBAN for bank transfer. */
        private String iban;
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
        /** Receiver Peppol ID. */
        private String receiverPeppolId;
        /** Replacement line items. */
        private List<SendDocumentRequest.LineItem> items;

        private Builder() {}

        /** @param v the invoice number @return this builder */
        public Builder invoiceNumber(String v) { this.invoiceNumber = v; return this; }
        /** @param v the issue date (YYYY-MM-DD) @return this builder */
        public Builder issueDate(String v) { this.issueDate = v; return this; }
        /** @param v the due date (YYYY-MM-DD) @return this builder */
        public Builder dueDate(String v) { this.dueDate = v; return this; }
        /** @param v the currency code @return this builder */
        public Builder currency(String v) { this.currency = v; return this; }
        /** @param v the note text @return this builder */
        public Builder note(String v) { this.note = v; return this; }
        /** @param v the IBAN @return this builder */
        public Builder iban(String v) { this.iban = v; return this; }
        /** @param v the variable symbol @return this builder */
        public Builder variableSymbol(String v) { this.variableSymbol = v; return this; }
        /** @param v the buyer reference @return this builder */
        public Builder buyerReference(String v) { this.buyerReference = v; return this; }
        /** @param v the receiver name @return this builder */
        public Builder receiverName(String v) { this.receiverName = v; return this; }
        /** @param v the receiver ICO @return this builder */
        public Builder receiverIco(String v) { this.receiverIco = v; return this; }
        /** @param v the receiver DIC @return this builder */
        public Builder receiverDic(String v) { this.receiverDic = v; return this; }
        /** @param v the receiver IC DPH @return this builder */
        public Builder receiverIcDph(String v) { this.receiverIcDph = v; return this; }
        /** @param v the receiver address @return this builder */
        public Builder receiverAddress(String v) { this.receiverAddress = v; return this; }
        /** @param v the receiver country code @return this builder */
        public Builder receiverCountry(String v) { this.receiverCountry = v; return this; }
        /** @param v the receiver Peppol ID @return this builder */
        public Builder receiverPeppolId(String v) { this.receiverPeppolId = v; return this; }
        /** @param v the replacement line items @return this builder */
        public Builder items(List<SendDocumentRequest.LineItem> v) { this.items = v; return this; }

        /**
         * Build the update document request.
         *
         * @return the constructed request
         */
        public UpdateDocumentRequest build() {
            return new UpdateDocumentRequest(this);
        }
    }
}
