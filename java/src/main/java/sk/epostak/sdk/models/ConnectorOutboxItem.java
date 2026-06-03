package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Staged Connector outbox item.
 */
public record ConnectorOutboxItem(
        String outboxId,
        String externalId,
        String status,
        String scheduledFor,
        String documentId,
        boolean ready,
        ConnectorRepairReport repairReport,
        List<ConnectorSafeFix> safeFixes,
        Map<String, Object> lastError,
        Integer attemptCount,
        String sentAt,
        String cancelledAt,
        String createdAt,
        String updatedAt,
        Map<String, String> links
) {}
