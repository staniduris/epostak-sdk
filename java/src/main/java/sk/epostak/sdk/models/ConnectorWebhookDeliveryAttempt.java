package sk.epostak.sdk.models;

/** One persisted HTTP execution in a Connector webhook delivery timeline. */
public record ConnectorWebhookDeliveryAttempt(
        String id,
        int number,
        String outcome,
        String startedAt,
        String completedAt,
        Integer durationMs,
        String endpoint,
        String requestTimestamp,
        String requestBodySha256,
        Integer responseStatus,
        String responseContentType,
        String responseBody,
        String responseBodySha256,
        boolean responseBodyTruncated,
        Boolean retryable,
        Integer retryAfterMs,
        String nextRetryAt,
        String diagnosisCode,
        String errorMessage
) {}
