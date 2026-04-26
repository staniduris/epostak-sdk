package sk.epostak.sdk.models;

/**
 * Response from batch-acknowledging events from the webhook queue.
 *
 * @param acknowledged the number of events successfully acknowledged
 */
public record BatchAckResponse(int acknowledged) {}
