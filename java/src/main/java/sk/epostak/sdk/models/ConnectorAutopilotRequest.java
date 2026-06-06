package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Request body for POST /connector/autopilot.
 */
public record ConnectorAutopilotRequest(
        String customerRef,
        String mode,
        String externalId,
        String idempotencyKey,
        Map<String, Object> payload,
        ConnectorSendPolicyOptions send,
        Map<String, Object> options
) {
    public ConnectorAutopilotRequest(
            String mode,
            String externalId,
            String idempotencyKey,
            Map<String, Object> payload,
            Map<String, Object> options
    ) {
        this(null, mode, externalId, idempotencyKey, payload, null, options);
    }
}
