package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorBusinessAcknowledgeResponse;
import sk.epostak.sdk.models.ConnectorBusinessDocument;
import sk.epostak.sdk.models.ConnectorBusinessDocumentListParams;
import sk.epostak.sdk.models.ConnectorBusinessDocumentListResponse;
import sk.epostak.sdk.models.ConnectorBusinessDocumentRequest;
import sk.epostak.sdk.models.ConnectorInvoiceResponseRequest;
import sk.epostak.sdk.models.ConnectorInvoiceResponseResult;

import java.util.Map;

public final class ConnectorCustomerDocumentsResource {
    private final ConnectorResource connector;
    private final String customerRef;

    ConnectorCustomerDocumentsResource(ConnectorResource connector, String customerRef) {
        this.connector = connector;
        this.customerRef = customerRef;
    }

    public ConnectorBusinessDocument send(ConnectorBusinessDocumentRequest request) {
        return send(request, null);
    }
    public ConnectorBusinessDocument send(ConnectorBusinessDocumentRequest request, String idempotencyKey) {
        return connector.submitCustomerDocument(customerRef, request, "send", idempotencyKey);
    }
    public ConnectorBusinessDocument stage(ConnectorBusinessDocumentRequest request) {
        return stage(request, null);
    }
    public ConnectorBusinessDocument stage(ConnectorBusinessDocumentRequest request, String idempotencyKey) {
        return connector.submitCustomerDocument(customerRef, request, "stage", idempotencyKey);
    }
    public ConnectorBusinessDocumentListResponse list(ConnectorBusinessDocumentListParams params) {
        return connector.listCustomerDocuments(customerRef, params);
    }
    public ConnectorBusinessDocumentListResponse list() {
        return list(ConnectorBusinessDocumentListParams.empty());
    }
    public ConnectorBusinessAcknowledgeResponse acknowledge(String documentId, String reference) {
        return connector.acknowledgeDocument(documentId, reference, customerRef);
    }

    /** Send a previously staged business document. */
    public ConnectorBusinessDocument sendDocument(String documentId) {
        return connector.sendDocument(documentId, customerRef);
    }

    /** Cancel a staged business document before delivery. */
    public ConnectorBusinessDocument cancelDocument(String documentId) {
        return connector.cancelDocument(documentId, customerRef);
    }
    /**
     * Dictionary-style detail retained with its original JVM descriptor for
     * source and binary compatibility.
     */
    public Map<String, Object> get(String documentId) {
        return connector.getCustomerDocument(documentId, customerRef);
    }

    /** Typed business-document detail for new Connector integrations. */
    public ConnectorBusinessDocument getBusinessDocument(String documentId) {
        return connector.getBusinessDocument(documentId, customerRef);
    }
    public ConnectorInvoiceResponseResult respond(String documentId, ConnectorInvoiceResponseRequest request) {
        return connector.respondDocument(documentId, customerRef, request);
    }
    public String ubl(String documentId) { return connector.getDocumentUbl(documentId, customerRef); }
    public Map<String, Object> evidence(String documentId) { return connector.getDocumentEvidence(documentId, customerRef); }
    public Map<String, Object> evidenceBundle(String documentId) { return connector.getDocumentEvidenceBundle(documentId, customerRef); }
    public Map<String, Object> supportPacket(String documentId) { return connector.getDocumentSupportPacket(documentId, customerRef); }
}
