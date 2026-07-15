package sk.epostak.sdk.models;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

/** Strict OpenAPI business request for customer-scoped Connector documents. */
public final class ConnectorBusinessDocumentRequest {
    private final String externalId;
    private final String number;
    private final ConnectorBusinessRecipient recipient;
    private final List<ConnectorBusinessLine> lines;
    private String type = "invoice";
    private String precedingDocumentNumber;
    private String issueDate;
    private String dueDate;
    private String currency;
    private String note;
    private String iban;
    private String paymentMethod;
    private String variableSymbol;
    private String buyerReference;
    private Double prepaidAmount;
    private List<ConnectorBusinessPrepayment> prepayments;
    private List<ConnectorBusinessAttachment> attachments;

    public ConnectorBusinessDocumentRequest(
            String externalId,
            String number,
            ConnectorBusinessRecipient recipient,
            List<ConnectorBusinessLine> lines
    ) {
        if (externalId == null) {
            throw new IllegalArgumentException("Connector externalId is required");
        }
        if (number == null || number.isBlank()) {
            throw new IllegalArgumentException("Connector number is required");
        }
        this.externalId = externalId;
        this.number = number;
        this.recipient = Objects.requireNonNull(recipient, "Connector recipient is required");
        this.lines = List.copyOf(Objects.requireNonNull(lines, "Connector lines are required"));
        if (this.lines.isEmpty()) {
            throw new IllegalArgumentException("Connector lines must contain at least one item");
        }
    }

    public String externalId() { return externalId; }
    public ConnectorBusinessDocumentRequest type(String value) { this.type = value; return this; }
    public ConnectorBusinessDocumentRequest precedingDocumentNumber(String value) { this.precedingDocumentNumber = value; return this; }
    public ConnectorBusinessDocumentRequest issueDate(String value) { this.issueDate = value; return this; }
    public ConnectorBusinessDocumentRequest dueDate(String value) { this.dueDate = value; return this; }
    public ConnectorBusinessDocumentRequest currency(String value) { this.currency = value; return this; }
    public ConnectorBusinessDocumentRequest note(String value) { this.note = value; return this; }
    public ConnectorBusinessDocumentRequest iban(String value) { this.iban = value; return this; }
    public ConnectorBusinessDocumentRequest paymentMethod(String value) { this.paymentMethod = value; return this; }
    public ConnectorBusinessDocumentRequest variableSymbol(String value) { this.variableSymbol = value; return this; }
    public ConnectorBusinessDocumentRequest buyerReference(String value) { this.buyerReference = value; return this; }
    public ConnectorBusinessDocumentRequest prepaidAmount(Double value) { this.prepaidAmount = value; return this; }
    public ConnectorBusinessDocumentRequest prepayments(List<ConnectorBusinessPrepayment> value) {
        this.prepayments = value == null ? null : List.copyOf(value);
        return this;
    }
    public ConnectorBusinessDocumentRequest attachments(List<ConnectorBusinessAttachment> value) {
        this.attachments = value == null ? null : List.copyOf(value);
        return this;
    }

    public Map<String, Object> toMap(String customerRef, String delivery) {
        return toMap(customerRef, delivery, externalId);
    }

    public Map<String, Object> toMap(String customerRef, String delivery, String normalizedExternalId) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("customerRef", customerRef);
        body.put("externalId", normalizedExternalId);
        body.put("delivery", delivery);
        body.put("type", type);
        body.put("number", number);
        body.put("recipient", recipient);
        body.put("lines", lines);
        put(body, "precedingDocumentNumber", precedingDocumentNumber);
        put(body, "issueDate", issueDate);
        put(body, "dueDate", dueDate);
        put(body, "currency", currency);
        put(body, "note", note);
        put(body, "iban", iban);
        put(body, "paymentMethod", paymentMethod);
        put(body, "variableSymbol", variableSymbol);
        put(body, "buyerReference", buyerReference);
        put(body, "prepaidAmount", prepaidAmount);
        put(body, "prepayments", prepayments);
        put(body, "attachments", attachments);
        return body;
    }

    private static void put(Map<String, Object> body, String key, Object value) {
        if (value != null) body.put(key, value);
    }
}
