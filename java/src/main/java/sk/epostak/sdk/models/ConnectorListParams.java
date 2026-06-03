package sk.epostak.sdk.models;

/**
 * Cursor pagination params for Connector inbox and events.
 */
public record ConnectorListParams(String cursor, Integer limit) {
    public static ConnectorListParams empty() {
        return new ConnectorListParams(null, null);
    }
}
