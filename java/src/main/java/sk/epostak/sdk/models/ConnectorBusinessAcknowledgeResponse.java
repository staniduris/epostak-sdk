package sk.epostak.sdk.models;

public record ConnectorBusinessAcknowledgeResponse(
        String id,
        String customerRef,
        String state,
        String processedAt,
        String reference,
        boolean idempotent
) {}
