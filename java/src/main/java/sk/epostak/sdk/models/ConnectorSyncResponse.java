package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Connector sync page for ERP reconciliation cursors.
 */
public record ConnectorSyncResponse(List<Map<String, Object>> items, String nextCursor, Boolean hasMore) {
}
