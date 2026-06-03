package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from {@code POST /connector/outbox}.
 */
public record ConnectorOutboxStageResponse(
        int total,
        Integer ready,
        Integer blocked,
        Integer staged,
        List<ConnectorOutboxItem> items
) {}
