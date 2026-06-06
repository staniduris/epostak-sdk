package sk.epostak.sdk.models;

/**
 * Optional request body for executing a pending Connector action.
 */
public record ConnectorActionRequest(String sendAt, String status, String note) {
    public static ConnectorActionRequest empty() {
        return new ConnectorActionRequest(null, null, null);
    }
}
