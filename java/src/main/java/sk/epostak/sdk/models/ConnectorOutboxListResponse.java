package sk.epostak.sdk.models;

import java.util.List;

/**
 * Offset-paginated Connector outbox list response.
 */
public record ConnectorOutboxListResponse(
        List<ConnectorOutboxItem> items,
        int total,
        int limit,
        int offset
) {}
