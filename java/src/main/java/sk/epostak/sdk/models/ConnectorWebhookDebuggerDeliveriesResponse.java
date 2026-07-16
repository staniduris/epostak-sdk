package sk.epostak.sdk.models;

import java.util.List;

/** Cursor-paged enriched Connector webhook debugger deliveries. */
public record ConnectorWebhookDebuggerDeliveriesResponse(
        List<ConnectorWebhookDebuggerDelivery> deliveries,
        String nextCursor,
        boolean hasMore
) {}
