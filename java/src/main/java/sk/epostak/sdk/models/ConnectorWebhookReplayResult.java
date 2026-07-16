package sk.epostak.sdk.models;

import java.util.Map;

/** Result of reserving an exact-body Connector webhook replay. */
public record ConnectorWebhookReplayResult(
        boolean accepted,
        boolean deduplicated,
        String replayedFrom,
        String deliveryId,
        String webhookId,
        String eventId,
        String status,
        Map<String, String> links
) {}
