package sk.epostak.sdk.models;

import java.util.Map;

/** Enriched Connector debugger delivery summary. */
public record ConnectorWebhookDebuggerDelivery(
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
        String createdAt,
        String documentId,
        Boolean test,
        String testScenario,
        String diagnosisCode,
        String nextAction,
        String replayedFromId,
        Boolean canReplay,
        Boolean attemptHistoryComplete,
        Map<String, String> links
) {}
