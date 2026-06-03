package sk.epostak.sdk.models;

import java.util.Map;

/**
 * One item accepted by {@code POST /connector/outbox}.
 */
public record ConnectorOutboxStageItem(
        String externalId,
        String idempotencyKey,
        String scheduledFor,
        Map<String, Object> payload
) {}
