package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Updated Connector mailbox response.
 */
public record ConnectorMailboxUpdateResponse(Map<String, Object> mailbox) {
}
