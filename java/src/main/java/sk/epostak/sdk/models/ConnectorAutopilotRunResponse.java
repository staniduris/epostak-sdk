package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Connector Autopilot lifecycle response.
 */
public record ConnectorAutopilotRunResponse(
        String autopilotId,
        String externalId,
        String idempotencyKey,
        String mode,
        String lifecycleStatus,
        Boolean replayed,
        Map<String, Object> preflight,
        Map<String, Object> repairReport,
        List<ConnectorSafeFix> safeFixes,
        Map<String, Object> send,
        Map<String, Object> status,
        Map<String, Object> lastError,
        String documentId,
        String outboxId,
        String sentAt,
        String deliveredAt,
        String createdAt,
        String updatedAt,
        List<String> nextActions,
        Map<String, String> links
) {
}
