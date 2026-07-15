package sk.epostak.sdk.models;

/** Canonical Connector response after accepting or queueing an Invoice Response. */
public record ConnectorInvoiceResponseResult(
        String id,
        String customerRef,
        ConnectorInvoiceResponseDelivery response,
        boolean idempotent
) {}
