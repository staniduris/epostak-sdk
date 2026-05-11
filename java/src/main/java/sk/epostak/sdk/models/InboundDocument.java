package sk.epostak.sdk.models;

/**
 * An inbound (received) document from the Pull API ({@code GET /inbound/documents}).
 * <p>
 * This is the new Pull API shape returned by {@code /inbound/documents} and
 * {@code /inbound/documents/{id}}. It differs from the legacy inbox
 * ({@code /documents/inbox}) which returns a different shape and requires the
 * {@code api-enterprise} plan. The Pull API requires {@code requireApiEligiblePlan}
 * and scope {@code documents:read}.
 */
public final class InboundDocument {

    private final String id;
    private final String kind;
    private final String status;
    private final String senderPeppolId;
    private final String receiverPeppolId;
    private final String documentNumber;
    private final String issueDate;
    private final String currency;
    private final Double totalWithVat;
    private final String clientReference;
    private final String clientAckedAt;
    private final String receivedAt;
    private final String createdAt;

    public InboundDocument(
            String id,
            String kind,
            String status,
            String senderPeppolId,
            String receiverPeppolId,
            String documentNumber,
            String issueDate,
            String currency,
            Double totalWithVat,
            String clientReference,
            String clientAckedAt,
            String receivedAt,
            String createdAt
    ) {
        this.id = id;
        this.kind = kind;
        this.status = status;
        this.senderPeppolId = senderPeppolId;
        this.receiverPeppolId = receiverPeppolId;
        this.documentNumber = documentNumber;
        this.issueDate = issueDate;
        this.currency = currency;
        this.totalWithVat = totalWithVat;
        this.clientReference = clientReference;
        this.clientAckedAt = clientAckedAt;
        this.receivedAt = receivedAt;
        this.createdAt = createdAt;
    }

    /** @return document UUID */
    public String getId() { return id; }

    /** @return document kind, e.g. {@code "invoice"}, {@code "credit_note"} */
    public String getKind() { return kind; }

    /**
     * @return processing status: {@code "received"} (not yet acknowledged) or
     *         {@code "acknowledged"} (client called {@code ack()})
     */
    public String getStatus() { return status; }

    /** @return Peppol participant ID of the sender, e.g. {@code "0245:2012345678"} */
    public String getSenderPeppolId() { return senderPeppolId; }

    /** @return Peppol participant ID of the receiver */
    public String getReceiverPeppolId() { return receiverPeppolId; }

    /** @return document number as extracted from the UBL, or {@code null} */
    public String getDocumentNumber() { return documentNumber; }

    /** @return issue date in ISO 8601 format (YYYY-MM-DD), or {@code null} */
    public String getIssueDate() { return issueDate; }

    /** @return ISO 4217 currency code, e.g. {@code "EUR"}, or {@code null} */
    public String getCurrency() { return currency; }

    /** @return total amount including VAT, or {@code null} if not available */
    public Double getTotalWithVat() { return totalWithVat; }

    /**
     * @return caller-supplied reference set at the time of {@code ack()}, or {@code null}
     */
    public String getClientReference() { return clientReference; }

    /** @return ISO 8601 timestamp of the last acknowledgement, or {@code null} */
    public String getClientAckedAt() { return clientAckedAt; }

    /** @return ISO 8601 timestamp when the document arrived at the AP */
    public String getReceivedAt() { return receivedAt; }

    /** @return ISO 8601 timestamp when the pull-API row was created */
    public String getCreatedAt() { return createdAt; }
}
