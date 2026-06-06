package sk.epostak.sdk.models;

/**
 * Managed send policy for Connector Autopilot and mailbox workflows.
 */
public record ConnectorSendPolicyOptions(String policy, String sendAt) {
}
