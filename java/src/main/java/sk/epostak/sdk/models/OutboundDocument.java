package sk.epostak.sdk.models;

import java.util.List;

/**
 * An outbound (sent) document from the Pull API ({@code GET /outbound/documents}).
 * <p>
 * Returned by {@code /outbound/documents} (list) and {@code /outbound/documents/{id}}
 * (detail). The detail view includes {@code attemptHistory} which is absent in list
 * items. Requires {@code requireApiEligiblePlan} and scope {@code documents:read}.
 */
public final class OutboundDocument {

    private final String id;
    private final String kind;
    private final String status;
    private final String businessStatus;
    private final String senderPeppolId;
    private final String recipientPeppolId;
    private final String documentNumber;
    private final String issueDate;
    private final String currency;
    private final Double totalWithVat;
    private final String peppolMessageId;
    private final String sentAt;
    private final String deliveredAt;
    private final String createdAt;
    private final List<AttemptRecord> attemptHistory;

    public OutboundDocument(
            String id,
            String kind,
            String status,
            String businessStatus,
            String senderPeppolId,
            String recipientPeppolId,
            String documentNumber,
            String issueDate,
            String currency,
            Double totalWithVat,
            String peppolMessageId,
            String sentAt,
            String deliveredAt,
            String createdAt,
            List<AttemptRecord> attemptHistory
    ) {
        this.id = id;
        this.kind = kind;
        this.status = status;
        this.businessStatus = businessStatus;
        this.senderPeppolId = senderPeppolId;
        this.recipientPeppolId = recipientPeppolId;
        this.documentNumber = documentNumber;
        this.issueDate = issueDate;
        this.currency = currency;
        this.totalWithVat = totalWithVat;
        this.peppolMessageId = peppolMessageId;
        this.sentAt = sentAt;
        this.deliveredAt = deliveredAt;
        this.createdAt = createdAt;
        this.attemptHistory = attemptHistory;
    }

    /** @return document UUID */
    public String getId() { return id; }

    /** @return document kind, e.g. {@code "invoice"}, {@code "self_billing"} */
    public String getKind() { return kind; }

    /**
     * @return transport status: {@code "pending"}, {@code "sending"}, {@code "delivered"},
     *         {@code "failed"}, etc.
     */
    public String getStatus() { return status; }

    /**
     * @return business-level status (from Invoice Response), e.g. {@code "AP"}, {@code "RE"},
     *         or {@code null} when no response has been received
     */
    public String getBusinessStatus() { return businessStatus; }

    /** @return Peppol participant ID of the sender */
    public String getSenderPeppolId() { return senderPeppolId; }

    /** @return Peppol participant ID of the recipient, or {@code null} */
    public String getRecipientPeppolId() { return recipientPeppolId; }

    /** @return document number, or {@code null} */
    public String getDocumentNumber() { return documentNumber; }

    /** @return issue date in ISO 8601 format (YYYY-MM-DD), or {@code null} */
    public String getIssueDate() { return issueDate; }

    /** @return ISO 4217 currency code, or {@code null} */
    public String getCurrency() { return currency; }

    /** @return total amount including VAT, or {@code null} */
    public Double getTotalWithVat() { return totalWithVat; }

    /** @return Peppol AS4 message ID, or {@code null} before delivery */
    public String getPeppolMessageId() { return peppolMessageId; }

    /** @return ISO 8601 timestamp when the document was sent, or {@code null} */
    public String getSentAt() { return sentAt; }

    /** @return ISO 8601 timestamp of confirmed delivery, or {@code null} */
    public String getDeliveredAt() { return deliveredAt; }

    /** @return ISO 8601 creation timestamp */
    public String getCreatedAt() { return createdAt; }

    /**
     * @return delivery attempt history (present only on the detail view
     *         {@code /outbound/documents/{id}}), or {@code null} in list items
     */
    public List<AttemptRecord> getAttemptHistory() { return attemptHistory; }

    /**
     * A single delivery attempt record in the document's transport history.
     */
    public static final class AttemptRecord {
        private final String attemptedAt;
        private final String result;
        private final String errorCode;
        private final String detail;

        public AttemptRecord(String attemptedAt, String result, String errorCode, String detail) {
            this.attemptedAt = attemptedAt;
            this.result = result;
            this.errorCode = errorCode;
            this.detail = detail;
        }

        /** @return ISO 8601 timestamp of this attempt */
        public String getAttemptedAt() { return attemptedAt; }

        /** @return outcome: {@code "success"} or {@code "failure"} */
        public String getResult() { return result; }

        /** @return error code on failure, or {@code null} on success */
        public String getErrorCode() { return errorCode; }

        /** @return human-readable detail, or {@code null} */
        public String getDetail() { return detail; }
    }
}
