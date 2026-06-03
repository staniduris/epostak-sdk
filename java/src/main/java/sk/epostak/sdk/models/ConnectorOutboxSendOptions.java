package sk.epostak.sdk.models;

/**
 * Options for sending one Connector outbox item.
 */
public record ConnectorOutboxSendOptions(Boolean force) {
    public static ConnectorOutboxSendOptions empty() {
        return new ConnectorOutboxSendOptions(null);
    }
}
