package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.ConnectorWebhookConfiguration;
import sk.epostak.sdk.models.ConnectorWebhookDeliveriesResponse;
import sk.epostak.sdk.models.ConnectorWebhookDeliveryDetail;
import sk.epostak.sdk.models.ConnectorWebhookDebuggerDeliveriesResponse;
import sk.epostak.sdk.models.ConnectorWebhookReplayResult;
import sk.epostak.sdk.models.ConnectorWebhookTestResponse;
import sk.epostak.sdk.models.ConnectorWebhookTestSuiteAccepted;
import sk.epostak.sdk.models.ConnectorWebhookTestSuiteStatus;
import sk.epostak.sdk.models.WebhookRotateSecretResponse;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/** One global Connector webhook per integrator. Every call omits X-Firm-Id. */
public final class ConnectorWebhookResource {
    private final HttpClient http;

    ConnectorWebhookResource(HttpClient http) {
        this.http = http;
    }

    public ConnectorWebhookConfiguration get() {
        return http.getNoFirm("/connector/webhook", ConnectorWebhookConfiguration.class);
    }

    public ConnectorWebhookConfiguration configure(String url, List<String> events) {
        if (url == null || url.isBlank()) {
            throw new IllegalArgumentException("Connector webhook URL is required");
        }
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("url", url.trim());
        if (events != null) body.put("events", List.copyOf(events));
        return http.putNoFirm("/connector/webhook", body, ConnectorWebhookConfiguration.class);
    }

    public ConnectorWebhookConfiguration configure(String url) {
        return configure(url, null);
    }

    public void delete() {
        http.deleteNoFirm("/connector/webhook", Void.class);
    }

    public WebhookRotateSecretResponse rotateSecret() {
        return http.postNoFirm(
                "/connector/webhook/rotate-secret",
                null,
                WebhookRotateSecretResponse.class
        );
    }

    public ConnectorWebhookTestResponse test(String customerRef) {
        return test(customerRef, null, null);
    }

    public ConnectorWebhookTestResponse test(String customerRef, String event, String scenario) {
        String normalizedCustomerRef = customerRef == null ? "" : ConnectorResource.trimString(customerRef);
        if (normalizedCustomerRef.isEmpty()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("customerRef", normalizedCustomerRef);
        if (event != null) body.put("event", event);
        if (scenario != null) body.put("scenario", scenario);
        return http.postNoFirm(
                "/connector/webhook/test",
                body,
                ConnectorWebhookTestResponse.class
        );
    }

    public ConnectorWebhookDeliveriesResponse deliveries(String cursor, Integer limit, String status) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("cursor", cursor);
        params.put("limit", limit);
        params.put("status", status == null ? null : status.toUpperCase(Locale.ROOT));
        return http.getNoFirm(
                "/connector/webhook/deliveries" + HttpClient.buildQuery(params),
                ConnectorWebhookDeliveriesResponse.class
        );
    }

    public ConnectorWebhookDeliveriesResponse deliveries() {
        return deliveries(null, null, null);
    }

    public ConnectorWebhookDebuggerDeliveriesResponse listDeliveries(Map<String, Object> filters) {
        Map<String, Object> params = new LinkedHashMap<>(filters == null ? Map.of() : filters);
        Object status = params.get("status");
        if (status != null) params.put("status", String.valueOf(status).toUpperCase(Locale.ROOT));
        return http.getNoFirm(
                "/connector/webhook/deliveries" + HttpClient.buildQuery(params),
                ConnectorWebhookDebuggerDeliveriesResponse.class
        );
    }

    public ConnectorWebhookDeliveryDetail getDelivery(String deliveryId) {
        return http.getNoFirm(
                "/connector/webhook/deliveries/" + HttpClient.encode(deliveryId),
                ConnectorWebhookDeliveryDetail.class
        );
    }

    public ConnectorWebhookReplayResult replayDelivery(
            String deliveryId,
            String idempotencyKey,
            boolean confirmSuccessfulReplay
    ) {
        if (idempotencyKey == null || idempotencyKey.isBlank()) {
            throw new IllegalArgumentException("Connector replay idempotencyKey is required");
        }
        return http.postIdempotentNoFirm(
                "/connector/webhook/deliveries/" + HttpClient.encode(deliveryId) + "/replay",
                Map.of("confirmSuccessfulReplay", confirmSuccessfulReplay),
                ConnectorWebhookReplayResult.class,
                idempotencyKey.trim()
        );
    }

    public ConnectorWebhookTestSuiteAccepted runTestSuite(
            String customerRef,
            String event,
            List<String> scenarios,
            String idempotencyKey
    ) {
        String normalizedCustomerRef = customerRef == null ? "" : ConnectorResource.trimString(customerRef);
        if (normalizedCustomerRef.isEmpty()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        if (idempotencyKey == null || idempotencyKey.isBlank()) {
            throw new IllegalArgumentException("Connector test-suite idempotencyKey is required");
        }
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("customerRef", normalizedCustomerRef);
        if (event != null) body.put("event", event);
        if (scenarios != null) body.put("scenarios", List.copyOf(scenarios));
        return http.postIdempotentNoFirm(
                "/connector/webhook/test-suite",
                body,
                ConnectorWebhookTestSuiteAccepted.class,
                idempotencyKey.trim()
        );
    }

    public ConnectorWebhookTestSuiteStatus getTestSuite(String testRunId) {
        return http.getNoFirm(
                "/connector/webhook/test-suite/" + HttpClient.encode(testRunId),
                ConnectorWebhookTestSuiteStatus.class
        );
    }
}
