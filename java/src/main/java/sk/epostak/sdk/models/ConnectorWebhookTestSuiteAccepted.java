package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/** Accepted deterministic Connector webhook diagnostic suite. */
public record ConnectorWebhookTestSuiteAccepted(
        String testRunId,
        String status,
        boolean deduplicated,
        List<String> deliveryIds,
        String expiresAt,
        Map<String, String> links
) {}
