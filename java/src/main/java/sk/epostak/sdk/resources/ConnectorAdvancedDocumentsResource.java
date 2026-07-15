package sk.epostak.sdk.resources;

import java.util.Map;

/** Advanced UBL and evidence artifacts outside the golden business flow. */
public final class ConnectorAdvancedDocumentsResource {
    private final ConnectorResource connector;
    private final String customerRef;

    ConnectorAdvancedDocumentsResource(ConnectorResource connector) {
        this(connector, null);
    }

    ConnectorAdvancedDocumentsResource(ConnectorResource connector, String customerRef) {
        this.connector = connector;
        this.customerRef = customerRef;
    }

    public String ubl(String documentId) { return connector.getDocumentUbl(documentId, customerRef); }
    public Map<String, Object> evidence(String documentId) { return connector.getDocumentEvidence(documentId, customerRef); }
    public Map<String, Object> evidenceBundle(String documentId) { return connector.getDocumentEvidenceBundle(documentId, customerRef); }
    public Map<String, Object> supportPacket(String documentId) { return connector.getDocumentSupportPacket(documentId, customerRef); }
}
