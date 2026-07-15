package sk.epostak.sdk.models;

import java.util.List;

/** Stored configuration of the single global Connector webhook. */
public record ConnectorWebhook(
        String id,
        String url,
        List<String> events,
        boolean active,
        int failedAttempts,
        String createdAt,
        String updatedAt
) {}
