package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Cross-firm inbox response for integrator keys. Contains documents
 * from all assigned firms with firm identification on each document.
 *
 * @param documents the list of documents on this page
 * @param total     total number of matching documents across all firms
 * @param limit     max results per page
 * @param offset    current pagination offset
 */
public record InboxAllResponse(
        List<InboxAllDocument> documents,
        int total,
        int limit,
        int offset
) {
    /**
     * A document in the cross-firm inbox, with firm identification fields.
     *
     * @param firmId          the firm UUID this document belongs to
     * @param firmName        the firm name
     * @param id              document UUID
     * @param number          document number
     * @param status          current status
     * @param direction       {@code "inbound"} or {@code "outbound"}
     * @param docType         document type, e.g. {@code "invoice"}
     * @param issueDate       issue date (YYYY-MM-DD)
     * @param dueDate         due date (YYYY-MM-DD), or {@code null}
     * @param currency        currency code
     * @param supplier        supplier party summary
     * @param customer        customer party summary
     * @param totals          document monetary totals
     * @param peppolMessageId Peppol AS4 message ID
     * @param createdAt       ISO 8601 creation timestamp
     */
    public record InboxAllDocument(
            @SerializedName("firm_id") String firmId,
            @SerializedName("firm_name") String firmName,
            String id,
            String number,
            String status,
            String direction,
            @SerializedName("doc_type") String docType,
            @SerializedName("issue_date") String issueDate,
            @SerializedName("due_date") String dueDate,
            String currency,
            InboxAllParty supplier,
            InboxAllParty customer,
            InboxAllTotals totals,
            @SerializedName("peppol_message_id") String peppolMessageId,
            @SerializedName("created_at") String createdAt
    ) {}

    /**
     * Simplified party representation in cross-firm responses.
     *
     * @param name     company name
     * @param ico      Slovak ICO (company registration number)
     * @param peppolId Peppol participant ID
     */
    public record InboxAllParty(
            String name,
            String ico,
            @SerializedName("peppol_id") String peppolId
    ) {}

    /**
     * Monetary totals in cross-firm responses.
     *
     * @param withoutVat total excluding VAT, or {@code null}
     * @param vat        total VAT amount, or {@code null}
     * @param withVat    total including VAT, or {@code null}
     */
    public record InboxAllTotals(
            @SerializedName("without_vat") Double withoutVat,
            Double vat,
            @SerializedName("with_vat") Double withVat
    ) {}
}
