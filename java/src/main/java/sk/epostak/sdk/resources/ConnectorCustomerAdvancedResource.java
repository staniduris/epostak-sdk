package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorAutopilotRequest;
import sk.epostak.sdk.models.ConnectorAutopilotRunResponse;
import sk.epostak.sdk.models.ConnectorSyncParams;
import sk.epostak.sdk.models.ConnectorSyncResponse;

import java.util.Map;

/** Advanced controls for a manually approved Connector customer. */
public final class ConnectorCustomerAdvancedResource {
    private final ConnectorCustomerResource customer;
    private final ConnectorAdvancedDocumentsResource documents;
    private final ConnectorCustomerMailboxResource mailbox;

    ConnectorCustomerAdvancedResource(
            ConnectorCustomerResource customer,
            ConnectorResource connector,
            String customerRef
    ) {
        this.customer = customer;
        this.documents = new ConnectorAdvancedDocumentsResource(connector, customerRef);
        this.mailbox = new ConnectorCustomerMailboxResource(connector, customerRef);
    }

    public ConnectorAdvancedDocumentsResource documents() { return documents; }
    public ConnectorCustomerMailboxResource mailbox() { return mailbox; }

    @SuppressWarnings("deprecation")
    public ConnectorAutopilotRunResponse autopilot(ConnectorAutopilotRequest request) {
        return customer.autopilot(request);
    }

    /** Preview and normalize source data without staging or sending. */
    @SuppressWarnings("deprecation")
    public Map<String, Object> mapper(Map<String, Object> request) {
        return customer.mapper(request);
    }

    @SuppressWarnings("deprecation")
    public ConnectorAutopilotRunResponse zenInput(Map<String, Object> request) {
        return customer.zenInput(request);
    }

    @SuppressWarnings("deprecation")
    public ConnectorSyncResponse sync(ConnectorSyncParams params) {
        return customer.sync(params);
    }

    @SuppressWarnings("deprecation")
    public ConnectorSyncResponse sync() {
        return customer.sync(null);
    }
}
