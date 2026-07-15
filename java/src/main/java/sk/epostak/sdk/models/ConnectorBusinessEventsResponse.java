package sk.epostak.sdk.models;

import java.util.List;

/** Cursor-paginated customer-scoped business event response. */
public record ConnectorBusinessEventsResponse(
        List<ConnectorBusinessEvent> events,
        String nextCursor,
        boolean hasMore
) {}
