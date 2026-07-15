package sk.epostak.sdk.models;

/** Business-only event data shared by Connector polling and push webhooks. */
public record ConnectorBusinessEventData(
        String customerRef,
        String direction,
        String type,
        String number,
        ConnectorBusinessInvoiceResponse response
) {
    /** Map-like compatibility accessor for existing event consumers. */
    public Object get(String key) {
        return switch (key) {
            case "customerRef" -> customerRef;
            case "direction" -> direction;
            case "type" -> type;
            case "number" -> number;
            case "response" -> response;
            default -> null;
        };
    }

    public boolean containsKey(String key) {
        return key.equals("customerRef") || key.equals("direction") || key.equals("type")
                || key.equals("number") || key.equals("response");
    }
}
