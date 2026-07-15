package sk.epostak.sdk.models;

import java.util.List;

/** Cursor-paged delivery history for the global Connector webhook. */
public record ConnectorWebhookDeliveriesResponse(
        List<ConnectorWebhookDelivery> deliveries,
        String nextCursor,
        boolean hasMore
) {}
