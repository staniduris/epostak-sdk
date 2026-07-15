package sk.epostak.sdk.resources;

import sk.epostak.sdk.models.ConnectorInvoiceResponseRequest;
import sk.epostak.sdk.models.ConnectorInvoiceResponseResult;

import java.util.Map;

public final class ConnectorDocumentsResource {
    private final ConnectorResource connector;

    ConnectorDocumentsResource(ConnectorResource connector) {
        this.connector = connector;
    }

    public Map<String, Object> get(String documentId) { return connector.getDocument(documentId); }
    public ConnectorInvoiceResponseResult respond(
            String documentId,
            String customerRef,
            ConnectorInvoiceResponseRequest request
    ) { return connector.respondDocument(documentId, customerRef, request); }
    public String ubl(String documentId) { return connector.getDocumentUbl(documentId); }
    public Map<String, Object> evidence(String documentId) { return connector.getDocumentEvidence(documentId); }
    public Map<String, Object> evidenceBundle(String documentId) { return connector.getDocumentEvidenceBundle(documentId); }
    public Map<String, Object> supportPacket(String documentId) { return connector.getDocumentSupportPacket(documentId); }
}
