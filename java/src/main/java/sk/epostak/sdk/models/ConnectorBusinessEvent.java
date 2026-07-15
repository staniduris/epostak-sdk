package sk.epostak.sdk.models;

/** Canonical customer-scoped Connector event with business lifecycle state. */
public record ConnectorBusinessEvent(
        String id,
        String customerRef,
        String documentId,
        String type,
        String state,
        String occurredAt,
        ConnectorBusinessEventData data
) {}
