package sk.epostak.sdk.models;

import java.util.List;

/**
 * Document as returned by the API (same shape for sent and received documents).
 * <p>
 * Contains all document metadata including parties, line items, totals, and
 * Peppol delivery information.
 */
public final class Document {

    /** Unique document UUID. */
    private final String id;
    /** Document number, e.g. {@code "FV-2026-001"}. */
    private final String number;
    /** Current status: {@code "DRAFT"}, {@code "SENDING"}, {@code "DELIVERED"}, {@code "FAILED"}, etc. */
    private final String status;
    /** Direction: {@code "outbound"} (sent) or {@code "inbound"} (received). */
    private final String direction;
    /** Document type, e.g. {@code "invoice"}, {@code "credit_note"}. */
    private final String docType;
    /** Issue date in ISO 8601 format (YYYY-MM-DD). */
    private final String issueDate;
    /** Due date in ISO 8601 format (YYYY-MM-DD). May be {@code null}. */
    private final String dueDate;
    /** Currency code, e.g. {@code "EUR"}. */
    private final String currency;
    /** Supplier (seller) party. */
    private final Party supplier;
    /** Customer (buyer) party. */
    private final Party customer;
    /** Invoice line items. */
    private final List<LineItemResponse> lines;
    /** Document monetary totals. */
    private final DocumentTotals totals;
    /** Peppol AS4 message ID. May be {@code null} for drafts. */
    private final String peppolMessageId;
    /** ISO 8601 timestamp of document creation. */
    private final String createdAt;
    /** ISO 8601 timestamp of last update. */
    private final String updatedAt;

    public Document(String id, String number, String status, String direction,
                    String docType, String issueDate, String dueDate, String currency,
                    Party supplier, Party customer, List<LineItemResponse> lines,
                    DocumentTotals totals, String peppolMessageId,
                    String createdAt, String updatedAt) {
        this.id = id;
        this.number = number;
        this.status = status;
        this.direction = direction;
        this.docType = docType;
        this.issueDate = issueDate;
        this.dueDate = dueDate;
        this.currency = currency;
        this.supplier = supplier;
        this.customer = customer;
        this.lines = lines;
        this.totals = totals;
        this.peppolMessageId = peppolMessageId;
        this.createdAt = createdAt;
        this.updatedAt = updatedAt;
    }

    /** @return unique document UUID */
    public String getId() { return id; }
    /** @return document number, e.g. {@code "FV-2026-001"} */
    public String getNumber() { return number; }
    /** @return current status: {@code "DRAFT"}, {@code "SENDING"}, {@code "DELIVERED"}, {@code "FAILED"}, etc. */
    public String getStatus() { return status; }
    /** @return direction: {@code "outbound"} or {@code "inbound"} */
    public String getDirection() { return direction; }
    /** @return document type, e.g. {@code "invoice"}, {@code "credit_note"} */
    public String getDocType() { return docType; }
    /** @return issue date in ISO 8601 format */
    public String getIssueDate() { return issueDate; }
    /** @return due date in ISO 8601 format, or {@code null} */
    public String getDueDate() { return dueDate; }
    /** @return currency code, e.g. {@code "EUR"} */
    public String getCurrency() { return currency; }
    /** @return the supplier (seller) party */
    public Party getSupplier() { return supplier; }
    /** @return the customer (buyer) party */
    public Party getCustomer() { return customer; }
    /** @return the invoice line items */
    public List<LineItemResponse> getLines() { return lines; }
    /** @return the document monetary totals */
    public DocumentTotals getTotals() { return totals; }
    /** @return Peppol AS4 message ID, or {@code null} for drafts */
    public String getPeppolMessageId() { return peppolMessageId; }
    /** @return ISO 8601 creation timestamp */
    public String getCreatedAt() { return createdAt; }
    /** @return ISO 8601 last-update timestamp */
    public String getUpdatedAt() { return updatedAt; }

    // -- nested records -------------------------------------------------------

    /**
     * A party (supplier or customer) on a document.
     *
     * @param name     company or individual name
     * @param ico      Slovak company registration number (ICO)
     * @param dic      tax identification number (DIC)
     * @param icDph    VAT registration number (IC DPH), may be {@code null}
     * @param address  postal address
     * @param peppolId Peppol participant ID, e.g. {@code "0245:12345678"}
     */
    public record Party(
            String name,
            String ico,
            String dic,
            String icDph,
            PartyAddress address,
            String peppolId
    ) {}

    /**
     * Postal address of a party.
     *
     * @param street  street address
     * @param city    city name
     * @param zip     postal code
     * @param country ISO 3166-1 alpha-2 country code, e.g. {@code "SK"}
     */
    public record PartyAddress(
            String street,
            String city,
            String zip,
            String country
    ) {}

    /**
     * A single line item on a document (response shape).
     *
     * @param description item description
     * @param quantity    item quantity
     * @param unit        unit of measure, e.g. {@code "EA"}, {@code "HUR"}
     * @param unitPrice   price per unit excluding VAT
     * @param vatRate     VAT rate as a percentage (e.g. {@code 23.0})
     * @param vatCategory VAT category code, e.g. {@code "S"} for standard rate
     * @param lineTotal   total for this line excluding VAT
     */
    public record LineItemResponse(
            String description,
            double quantity,
            String unit,
            double unitPrice,
            double vatRate,
            String vatCategory,
            double lineTotal
    ) {}

    /**
     * Monetary totals for a document.
     *
     * @param withoutVat total amount excluding VAT
     * @param vat        total VAT amount
     * @param withVat    total amount including VAT
     */
    public record DocumentTotals(
            double withoutVat,
            double vat,
            double withVat
    ) {}
}
