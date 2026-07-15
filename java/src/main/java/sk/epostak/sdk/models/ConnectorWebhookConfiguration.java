package sk.epostak.sdk.models;

/** GET/PUT webhook envelope; secret is present only on first creation. */
public record ConnectorWebhookConfiguration(ConnectorWebhook webhook, String secret) {}
