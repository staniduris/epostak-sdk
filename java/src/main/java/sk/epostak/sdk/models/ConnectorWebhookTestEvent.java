package sk.epostak.sdk.models;

/** Canonical Connector event used for signed webhook tests. */
public record ConnectorWebhookTestEvent(
        String id,
        String customerRef,
        String documentId,
        String type,
        String state,
        String occurredAt,
        ConnectorBusinessEventData data,
        boolean test
) {}
