package sk.epostak.sdk.models;

/** One global Connector webhook delivery attempt. */
public record ConnectorWebhookDelivery(
        String id,
        String webhookId,
        String eventId,
        String customerRef,
        String type,
        String status,
        int attempts,
        Integer responseStatus,
        Integer responseTimeMs,
        String lastAttemptAt,
        String nextRetryAt,
        String createdAt
) {}
