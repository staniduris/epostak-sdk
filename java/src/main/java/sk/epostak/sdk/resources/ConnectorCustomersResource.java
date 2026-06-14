package sk.epostak.sdk.resources;

public final class ConnectorCustomersResource {
    private final ConnectorResource connector;

    ConnectorCustomersResource(ConnectorResource connector) {
        this.connector = connector;
    }

    public ConnectorCustomerResource forCustomer(String customerRef) {
        return new ConnectorCustomerResource(connector, customerRef);
    }
}
