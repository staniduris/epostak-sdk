package sk.epostak.sdk.models;

/**
 * Simple pagination/filter params for Connector outbox.
 */
public record ConnectorOutboxListParams(String status, Integer limit, Integer offset) {
    public static ConnectorOutboxListParams empty() {
        return new ConnectorOutboxListParams(null, null, null);
    }
}
