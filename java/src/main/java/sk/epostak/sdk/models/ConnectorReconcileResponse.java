package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from GET /connector/reconcile.
 */
public record ConnectorReconcileResponse(
        String status,
        String since,
        String generatedAt,
        Integer total,
        List<ConnectorReconcileItem> items
) {
}
