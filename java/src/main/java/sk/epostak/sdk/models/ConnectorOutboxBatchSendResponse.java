package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from {@code POST /connector/outbox/send}.
 */
public record ConnectorOutboxBatchSendResponse(
        int total,
        int sent,
        int failed,
        int skipped,
        List<ConnectorOutboxItem> results
) {}
