package sk.epostak.sdk.resources;

public final class ConnectorCustomersResource {
    private final ConnectorResource connector;

    ConnectorCustomersResource(ConnectorResource connector) {
        this.connector = connector;
    }

    public ConnectorCustomerResource forCustomer(String customerRef) {
        if (customerRef == null) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        String normalizedCustomerRef = ConnectorResource.trimString(customerRef);
        if (normalizedCustomerRef.isEmpty()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        return new ConnectorCustomerResource(connector, normalizedCustomerRef);
    }
}
