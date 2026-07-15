package sk.epostak.sdk.models;

import java.util.Map;

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
        String processedAt,
        String processedReference,
        String createdAt,
        String updatedAt,
        ConnectorBusinessInvoiceResponse response,
        Map<String, String> links
) {}
