package sk.epostak.sdk.models;

import java.util.Set;

/** Business-level response to an inbound Connector invoice. */
public record ConnectorInvoiceResponseRequest(String status, String note) {
    private static final Set<String> STATUSES = Set.of(
            "received",
            "in_process",
            "under_query",
            "conditionally_accepted",
            "rejected",
            "accepted",
            "paid"
    );

    public ConnectorInvoiceResponseRequest {
        if (status == null || !STATUSES.contains(status)) {
            throw new IllegalArgumentException("Invalid Connector response status");
        }
    }

    public ConnectorInvoiceResponseRequest(String status) {
        this(status, null);
    }
}
