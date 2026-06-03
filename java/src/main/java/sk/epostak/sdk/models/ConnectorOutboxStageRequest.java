package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Request body for {@code POST /connector/outbox}.
 */
public record ConnectorOutboxStageRequest(
        List<ConnectorOutboxStageItem> items,
        Map<String, Object> payload,
        String externalId,
        String scheduledFor
) {}
