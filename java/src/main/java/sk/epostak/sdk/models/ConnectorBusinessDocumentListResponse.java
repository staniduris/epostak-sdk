package sk.epostak.sdk.models;

import java.util.List;

public record ConnectorBusinessDocumentListResponse(
        List<ConnectorBusinessDocument> documents,
        String nextCursor,
        boolean hasMore
) {}
