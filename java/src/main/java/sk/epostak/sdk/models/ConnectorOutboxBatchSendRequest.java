package sk.epostak.sdk.models;

import java.util.List;

/**
 * Request body for {@code POST /connector/outbox/send}.
 */
public record ConnectorOutboxBatchSendRequest(
        List<String> ids,
        Integer limit,
        Boolean force
) {
    public static ConnectorOutboxBatchSendRequest empty() {
        return new ConnectorOutboxBatchSendRequest(null, null, null);
    }
}
