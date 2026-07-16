package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/** Current state of every scenario in a Connector webhook diagnostic suite. */
public record ConnectorWebhookTestSuiteStatus(
        String testRunId,
        String event,
        String status,
        List<Scenario> scenarios,
        String createdAt,
        String expiresAt,
        Map<String, String> links
) {
    public record Scenario(
            String scenario,
            boolean complete,
            Boolean passed,
            String failureReason,
            String actionRequired,
            String replayDeliveryId,
            List<Delivery> deliveries
    ) {}

    public record Delivery(
            String id,
            String status,
            int attempts,
            Integer responseStatus,
            String nextRetryAt,
            String detail
    ) {}
}
