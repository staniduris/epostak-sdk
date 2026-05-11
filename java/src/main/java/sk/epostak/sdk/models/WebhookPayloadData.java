package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Business-data payload carried by all v1 webhook events.
 *
 * <p>Common fields ({@code documentId}, {@code direction}, {@code doctypeKey},
 * {@code status}, {@code previousStatus}) are always present. Event-specific
 * extras (e.g. {@code sentAt}, {@code responseCode}) are present only for the
 * relevant event type; callers should null-check before use.
 *
 * <p>Wire format is snake_case JSON; all field names are mapped via
 * {@link SerializedName}.
 */
public final class WebhookPayloadData {

    // -------------------------------------------------------------------------
    // Always-present fields
    // -------------------------------------------------------------------------

    /** Document UUID. Renamed from legacy {@code invoice_id} (2026-05-12). */
    @SerializedName("document_id")
    public String documentId;

    /** {@code "inbound"} or {@code "outbound"} relative to the authenticated firm. */
    public String direction;

    /** Peppol doctype key (e.g. {@code "invoice"}, {@code "credit_note"}, {@code "order"}). */
    @SerializedName("doctype_key")
    public String doctypeKey;

    /** Document status after this event's state transition. */
    public String status;

    /** Document status BEFORE this event, or {@code null} for create-type events. */
    @SerializedName("previous_status")
    public String previousStatus;

    // -------------------------------------------------------------------------
    // Often-nullable fields (billing / identification)
    // -------------------------------------------------------------------------

    /** Human invoice/document number; present on billing events. */
    @SerializedName("document_number")
    public String documentNumber;

    /** Total amount as a string-encoded decimal; present on billing events. */
    @SerializedName("total_amount")
    public String totalAmount;

    /** ISO 4217 currency code (e.g. {@code "EUR"}); present on billing events. */
    public String currency;

    /** YYYY-MM-DD issue date; present on billing events. */
    @SerializedName("issue_date")
    public String issueDate;

    /** YYYY-MM-DD due date; present on billing events, may be {@code null}. */
    @SerializedName("due_date")
    public String dueDate;

    /** Sender Peppol participant identifier (e.g. {@code "0245:1122334455"}). */
    @SerializedName("sender_peppol_id")
    public String senderPeppolId;

    /** Receiver Peppol participant identifier. */
    @SerializedName("receiver_peppol_id")
    public String receiverPeppolId;

    // -------------------------------------------------------------------------
    // Event-specific extras (null when not applicable)
    // -------------------------------------------------------------------------

    /** {@code document.sent} — wall-clock time the AS4 send succeeded. */
    @SerializedName("sent_at")
    public String sentAt;

    /** {@code document.received} — AS4 ingest moment. */
    @SerializedName("received_at")
    public String receivedAt;

    /** {@code document.delivered} — when the receiving AP confirmed delivery. */
    @SerializedName("delivered_at")
    public String deliveredAt;

    /**
     * {@code document.delivered}, {@code document.sent}, {@code document.received}
     * — AS4 EBMS message ID.
     */
    @SerializedName("as4_message_id")
    public String as4MessageId;

    /** {@code document.rejected} — when the rejection arrived. */
    @SerializedName("rejected_at")
    public String rejectedAt;

    /** {@code document.response_received} — when the buyer response arrived. */
    @SerializedName("responded_at")
    public String respondedAt;

    /**
     * {@code document.rejected} / {@code document.response_received} — buyer response code.
     * One of {@code "RE"}, {@code "AB"}, {@code "IP"}, {@code "UQ"}, {@code "CA"},
     * {@code "AP"}, {@code "PD"}.
     */
    @SerializedName("response_code")
    public String responseCode;

    /** Human-readable rejection / response note (when supplied by the buyer). */
    @SerializedName("response_reason")
    public String responseReason;

    /**
     * {@code document.rejected} — which side produced the response.
     * One of {@code "peer_ap"}, {@code "buyer"}, or {@code "loopback"}.
     */
    public String responder;

    /** {@code document.delivery_failed} — final error message (truncated to 400 chars). */
    @SerializedName("failure_reason")
    public String failureReason;

    /** {@code document.delivery_failed} — total number of delivery attempts before giving up. */
    public Integer attempts;
}
