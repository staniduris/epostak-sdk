package sk.epostak.sdk.models;

/**
 * Optional request body for Connector mailbox repair.
 */
public record ConnectorMailboxRepairRequest(String customerRef) {
    public static ConnectorMailboxRepairRequest empty() {
        return new ConnectorMailboxRepairRequest(null);
    }
}
