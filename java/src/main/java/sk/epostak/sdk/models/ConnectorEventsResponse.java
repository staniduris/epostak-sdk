package sk.epostak.sdk.models;

import java.util.List;

/**
 * Cursor-paginated Connector events response.
 */
public record ConnectorEventsResponse(
        List<ConnectorEvent> events,
        String nextCursor,
        boolean hasMore
) {}
