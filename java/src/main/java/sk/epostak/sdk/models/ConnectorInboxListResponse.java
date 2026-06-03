package sk.epostak.sdk.models;

import java.util.List;

/**
 * Cursor-paginated Connector inbox response.
 */
public record ConnectorInboxListResponse(
        List<ConnectorInboxDocument> documents,
        String nextCursor,
        boolean hasMore
) {}
