package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorAutopilotRequest;
import sk.epostak.sdk.models.ConnectorAutopilotRunResponse;
import sk.epostak.sdk.models.ConnectorBusinessDocument;
import sk.epostak.sdk.models.ConnectorSubmitDocumentRequest;
import sk.epostak.sdk.models.ConnectorSyncParams;
import sk.epostak.sdk.models.ConnectorSyncResponse;

import java.util.LinkedHashMap;
import java.util.Map;

public final class ConnectorCustomerResource {
    private final ConnectorResource connector;
    private final String customerRef;
    private final ConnectorCustomerDocumentsResource documents;
    private final ConnectorCustomerEventsResource events;
    private final ConnectorCustomerAdvancedResource advanced;
    private final ConnectorCustomerMailboxResource mailbox;

    ConnectorCustomerResource(ConnectorResource connector, String customerRef) {
        this.connector = connector;
        this.customerRef = customerRef;
        this.documents = new ConnectorCustomerDocumentsResource(connector, customerRef);
        this.events = new ConnectorCustomerEventsResource(connector, customerRef);
        this.advanced = new ConnectorCustomerAdvancedResource(this, connector, customerRef);
        this.mailbox = advanced.mailbox();
    }

    public ConnectorAutopilotRunResponse submitDocument(ConnectorSubmitDocumentRequest request) {
        if (request.customerRef() != null && !customerRef.equals(request.customerRef())) {
            throw new IllegalArgumentException("Connector customerRef conflicts with scoped customer");
        }
        Map<String, Object> scoped = new LinkedHashMap<>(request.toMap());
        scoped.put("customerRef", customerRef);
        if (request.mode() == null) scoped.put("mode", "stage");
        return connector.advanced().autopilot(new ConnectorAutopilotRequest(
                customerRef,
                String.valueOf(scoped.get("mode")),
                request.externalId(),
                request.idempotencyKey(),
                request.payload(),
                null,
                Map.of()
        ));
    }

    public ConnectorAutopilotRunResponse autopilot(ConnectorAutopilotRequest request) {
        return connector.advanced().autopilot(new ConnectorAutopilotRequest(
                customerRef,
                request.mode(),
                request.externalId(),
                request.idempotencyKey(),
                request.payload(),
                request.send(),
                request.options()
        ));
    }

    public Map<String, Object> mapper(Map<String, Object> request) {
        Object execute = request.get("execute");
        if (execute != null && !"preview".equals(execute.toString())) {
            throw new IllegalArgumentException("Connector Mapper only supports preview normalization");
        }
        Object existingCustomerRef = request.get("customerRef");
        if (existingCustomerRef != null && !customerRef.equals(existingCustomerRef)) {
            throw new IllegalArgumentException("Connector customerRef conflicts with scoped customer");
        }
        Map<String, Object> scoped = new LinkedHashMap<>(request);
        scoped.put("customerRef", customerRef);
        scoped.put("execute", "preview");
        return connector.advanced().mapper(scoped);
    }

    public ConnectorAutopilotRunResponse zenInput(Map<String, Object> request) {
        Object existingCustomerRef = request.get("customerRef");
        if (existingCustomerRef != null && !customerRef.equals(existingCustomerRef.toString().strip())) {
            throw new IllegalArgumentException("Connector customerRef conflicts with scoped customer");
        }
        Map<String, Object> scoped = new LinkedHashMap<>(request);
        scoped.put("customerRef", customerRef);
        return connector.advanced().zenInput(scoped);
    }

    public ConnectorSyncResponse sync(ConnectorSyncParams params) {
        return connector.advanced().sync(new ConnectorSyncParams(
                customerRef,
                params == null ? null : params.cursor(),
                params == null ? null : params.limit()
        ));
    }

    public ConnectorCustomerDocumentsResource documents() { return documents; }
    public ConnectorCustomerEventsResource events() { return events; }
    public ConnectorCustomerAdvancedResource advanced() { return advanced; }
    public ConnectorCustomerMailboxResource mailbox() { return mailbox; }
}
