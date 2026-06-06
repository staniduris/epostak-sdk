package sk.epostak.sdk.models;

import java.util.Map;

/**
 * One item returned by Connector reconciliation.
 */
public record ConnectorReconcileItem(
        String type,
        String id,
        String externalId,
        String lifecycleStatus,
        String reason,
        String owner,
        String updatedAt,
        Map<String, Object> repairReport,
        Map<String, Object> lastError,
        Map<String, String> links
) {
}
