package sk.epostak.sdk.models;

/**
 * Cursor and customer filters for Connector sync.
 */
public record ConnectorSyncParams(String customerRef, String cursor, Integer limit) {
    public static ConnectorSyncParams empty() {
        return new ConnectorSyncParams(null, null, null);
    }
}
