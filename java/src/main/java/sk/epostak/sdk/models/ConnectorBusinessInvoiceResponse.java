package sk.epostak.sdk.models;

/** Latest business-level invoice response projected on list/detail results. */
public record ConnectorBusinessInvoiceResponse(
        String status,
        String direction,
        String reason,
        String respondedAt
) {}
