package sk.epostak.sdk.models;

import java.util.Map;
import java.util.List;

/** Business-only Connector document response matching the public OpenAPI contract. */
public record ConnectorBusinessDocument(
        String id,
        String customerRef,
        String externalId,
        String direction,
        String type,
        String number,
        String state,
        Boolean replayed,
        String currency,
        ConnectorBusinessAmounts amounts,
        ConnectorBusinessParty sender,
        ConnectorBusinessParty recipient,
        String issueDate,
        String dueDate,
        String taxPointDate,
        String deliveryDate,
        Double documentDiscountPercent,
        String processedAt,
        String processedReference,
        String createdAt,
        String updatedAt,
        ConnectorBusinessInvoiceResponse response,
        String businessType,
        List<ConnectorBusinessLine> lines,
        String delivery,
        List<ConnectorBusinessAttachment> attachments,
        String precedingDocumentNumber,
        String note,
        String iban,
        String paymentMethod,
        String variableSymbol,
        String buyerReference,
        String paymentTerms,
        String orderReference,
        Double prepaidAmount,
        List<ConnectorBusinessPrepayment> prepayments,
        Map<String, String> links
) {
    /** Source-compatible constructor retained for the pre-1.3 response shape. */
    public ConnectorBusinessDocument(
            String id, String customerRef, String externalId, String direction,
            String type, String number, String state, Boolean replayed, String currency,
            ConnectorBusinessAmounts amounts, ConnectorBusinessParty sender,
            ConnectorBusinessParty recipient, String issueDate, String dueDate,
            String taxPointDate, String deliveryDate, Double documentDiscountPercent,
            String processedAt, String processedReference, String createdAt, String updatedAt,
            ConnectorBusinessInvoiceResponse response, Map<String, String> links) {
        this(id, customerRef, externalId, direction, type, number, state, replayed, currency,
                amounts, sender, recipient, issueDate, dueDate, taxPointDate, deliveryDate,
                documentDiscountPercent, processedAt, processedReference, createdAt, updatedAt,
                response, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, links);
    }
}
