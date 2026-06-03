package sk.epostak.sdk.models;

import java.util.List;

/**
 * Connector document status response.
 */
public record ConnectorStatusResponse(
        String documentId,
        String status,
        String deliveredAt,
        List<ConnectorEvent> events
) {}
