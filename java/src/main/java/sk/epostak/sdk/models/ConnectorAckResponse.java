package sk.epostak.sdk.models;

/**
 * Response from {@code POST /connector/inbox/{documentId}/ack}.
 */
public record ConnectorAckResponse(
        String documentId,
        String status,
        boolean acknowledged,
        Boolean idempotent,
        String acknowledgedAt
) {}
