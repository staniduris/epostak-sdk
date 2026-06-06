package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Connector-managed mailbox list response.
 */
public record ConnectorMailboxListResponse(List<Map<String, Object>> mailboxes) {
}
