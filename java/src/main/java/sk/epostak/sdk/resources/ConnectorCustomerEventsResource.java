package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorBusinessEventsResponse;
import sk.epostak.sdk.models.ConnectorListParams;

public final class ConnectorCustomerEventsResource {
    private final ConnectorResource connector;
    private final String customerRef;

    ConnectorCustomerEventsResource(ConnectorResource connector, String customerRef) {
        this.connector = connector;
        this.customerRef = customerRef;
    }

    public ConnectorBusinessEventsResponse list(ConnectorListParams params) {
        return connector.listCustomerEvents(customerRef, params);
    }

    public ConnectorBusinessEventsResponse list() {
        return list(ConnectorListParams.empty());
    }
}
