package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/** Exact signed body, safe receiver evidence, and complete attempt timeline. */
public record ConnectorWebhookDeliveryDetail(
        ConnectorWebhookDebuggerDelivery delivery,
        Map<String, Object> payload,
        String rawBody,
        String rawBodySha256,
        boolean attemptHistoryComplete,
        String endpoint,
        Map<String, Object> signature,
        List<ConnectorWebhookDeliveryAttempt> attempts
) {}
