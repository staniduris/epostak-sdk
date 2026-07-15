package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorMailboxRepairRequest;
import sk.epostak.sdk.models.ConnectorMailboxUpdateResponse;
import sk.epostak.sdk.models.ConnectorSendPolicyOptions;

import java.util.Map;

public final class ConnectorCustomerMailboxResource {
    private final ConnectorResource connector;
    private final String customerRef;

    ConnectorCustomerMailboxResource(ConnectorResource connector, String customerRef) {
        this.connector = connector;
        this.customerRef = customerRef;
    }

    public Map<String, Object> repair() {
        return connector.advanced().repairMailbox(new ConnectorMailboxRepairRequest(customerRef));
    }

    public ConnectorMailboxUpdateResponse updateSendPolicy(ConnectorSendPolicyOptions options) {
        return connector.advanced().updateMailboxSendPolicy(customerRef, options);
    }
}
