package sk.epostak.sdk.models;

import java.util.LinkedHashMap;
import java.util.Map;

/** Request body for the major-release Connector submitDocument workflow. */
public final class ConnectorSubmitDocumentRequest {
    private String customerRef;
    private String mode;
    private final String externalId;
    private final String idempotencyKey;
    private final Map<String, Object> payload;

    public ConnectorSubmitDocumentRequest(String externalId, String idempotencyKey, Map<String, Object> payload) {
        this.externalId = externalId;
        this.idempotencyKey = idempotencyKey;
        this.payload = payload;
    }

    public String customerRef() { return customerRef; }
    public String mode() { return mode; }

    public void customerRef(String customerRef) { this.customerRef = customerRef; }
    public void mode(String mode) { this.mode = mode; }

    public Map<String, Object> toMap() {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("customerRef", customerRef);
        body.put("mode", mode);
        body.put("externalId", externalId);
        body.put("idempotencyKey", idempotencyKey);
        body.put("payload", payload);
        return body;
    }
}
