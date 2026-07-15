package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.ConnectorWebhookConfiguration;
import sk.epostak.sdk.models.ConnectorWebhookDeliveriesResponse;
import sk.epostak.sdk.models.ConnectorWebhookTestResponse;
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
        String normalizedCustomerRef = customerRef == null ? "" : ConnectorResource.trimString(customerRef);
        if (normalizedCustomerRef.isEmpty()) {
            throw new IllegalArgumentException("Connector customerRef is required");
        }
        return http.postNoFirm(
                "/connector/webhook/test",
                Map.of("customerRef", normalizedCustomerRef),
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
}
