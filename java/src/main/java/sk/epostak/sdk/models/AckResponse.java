package sk.epostak.sdk.models;

/**
 * Response from acknowledging a single event from the webhook queue.
 *
 * @param acknowledged always {@code true} on success
 */
public record AckResponse(boolean acknowledged) {}
