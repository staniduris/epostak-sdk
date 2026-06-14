package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorAutopilotRequest;
import sk.epostak.sdk.models.ConnectorAutopilotRunResponse;
import sk.epostak.sdk.models.ConnectorSubmitDocumentRequest;
import sk.epostak.sdk.models.ConnectorSyncParams;
import sk.epostak.sdk.models.ConnectorSyncResponse;

public final class ConnectorCustomerResource {
    private final ConnectorResource connector;
    private final String customerRef;
    private final ConnectorCustomerDocumentsResource documents;
    private final ConnectorCustomerMailboxResource mailbox;

    ConnectorCustomerResource(ConnectorResource connector, String customerRef) {
        this.connector = connector;
        this.customerRef = customerRef;
        this.documents = new ConnectorCustomerDocumentsResource(connector);
        this.mailbox = new ConnectorCustomerMailboxResource(connector, customerRef);
    }

    public ConnectorAutopilotRunResponse submitDocument(ConnectorSubmitDocumentRequest request) {
        if (request.customerRef() != null && !request.customerRef().equals(customerRef)) {
            throw new IllegalArgumentException("Connector customerRef conflicts with scoped customer");
        }
        request.customerRef(customerRef);
        if (request.mode() == null) {
            request.mode("stage");
        }
        return connector.submitDocument(request);
    }

    public ConnectorAutopilotRunResponse autopilot(ConnectorAutopilotRequest request) {
        return connector.autopilot(new ConnectorAutopilotRequest(
                customerRef,
                request.mode(),
                request.externalId(),
                request.idempotencyKey(),
                request.payload(),
                request.send(),
                request.options()
        ));
    }

    public ConnectorSyncResponse sync(ConnectorSyncParams params) {
        return connector.sync(new ConnectorSyncParams(
                customerRef,
                params == null ? null : params.cursor(),
                params == null ? null : params.limit()
        ));
    }

    public ConnectorCustomerDocumentsResource documents() { return documents; }
    public ConnectorCustomerMailboxResource mailbox() { return mailbox; }
}
