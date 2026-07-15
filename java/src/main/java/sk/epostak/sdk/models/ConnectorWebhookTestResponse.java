package sk.epostak.sdk.models;

/** Result of queueing a signed Connector webhook test event. */
public record ConnectorWebhookTestResponse(
        String deliveryId,
        String status,
        ConnectorWebhookTestEvent event
) {}
