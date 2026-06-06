package sk.epostak.sdk.models;

/**
 * Query params for GET /connector/reconcile.
 */
public record ConnectorReconcileParams(String status, String since) {
    public static ConnectorReconcileParams empty() {
        return new ConnectorReconcileParams(null, null);
    }
}
