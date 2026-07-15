package sk.epostak.sdk.models;

/** Delivery state of a business-level Connector invoice response. */
public record ConnectorInvoiceResponseDelivery(
        String status,
        String direction,
        String delivery,
        String respondedAt
) {}
