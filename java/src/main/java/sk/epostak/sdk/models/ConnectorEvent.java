package sk.epostak.sdk.models;

import java.util.Map;

/**
 * One Connector polling event.
 */
public record ConnectorEvent(
        String id,
        String documentId,
        String type,
        String occurredAt,
        String status,
        Map<String, Object> data
) {}
