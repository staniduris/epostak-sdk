using EPostak.Models;

namespace EPostak.Resources;

/// <summary>One global Connector webhook per integrator. Every call omits X-Firm-Id.</summary>
public sealed class ConnectorWebhookResource
{
    private readonly HttpRequestor _http;

    internal ConnectorWebhookResource(HttpRequestor http) => _http = http;

    public Task<ConnectorWebhookConfiguration> GetAsync(CancellationToken ct = default)
        => _http.RequestAsync<ConnectorWebhookConfiguration>(HttpMethod.Get, "/connector/webhook", ct, omitFirmId: true);

    public Task<ConnectorWebhookConfiguration> ConfigureAsync(
        string url,
        IReadOnlyList<string>? events = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Connector webhook URL is required.", nameof(url));
        return _http.RequestAsync<ConnectorWebhookConfiguration>(
            HttpMethod.Put,
            "/connector/webhook",
            new { url = url.Trim(), events },
            ct,
            omitFirmId: true);
    }

    public Task DeleteAsync(CancellationToken ct = default)
        => _http.RequestVoidAsync(HttpMethod.Delete, "/connector/webhook", ct, omitFirmId: true);

    public Task<WebhookRotateSecretResponse> RotateSecretAsync(CancellationToken ct = default)
        => _http.RequestAsync<WebhookRotateSecretResponse>(
            HttpMethod.Post,
            "/connector/webhook/rotate-secret",
            ct,
            omitFirmId: true);

    public Task<ConnectorWebhookTestResponse> TestAsync(
        string customerRef,
        CancellationToken ct = default)
        => TestScenarioAsync(customerRef, null, null, ct);

    public Task<ConnectorWebhookTestResponse> TestScenarioAsync(
        string customerRef,
        string? @event = null,
        string? scenario = null,
        CancellationToken ct = default)
    {
        var normalizedCustomerRef = ConnectorResource.TrimString(customerRef ?? "");
        if (normalizedCustomerRef.Length == 0)
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        return _http.RequestAsync<ConnectorWebhookTestResponse>(
            HttpMethod.Post,
            "/connector/webhook/test",
            new Dictionary<string, object?>
            {
                ["customerRef"] = normalizedCustomerRef,
                ["event"] = @event,
                ["scenario"] = scenario,
            },
            ct,
            omitFirmId: true);
    }

    public Task<ConnectorWebhookDeliveriesResponse> DeliveriesAsync(
        string? cursor = null,
        int? limit = null,
        string? status = null,
        CancellationToken ct = default)
        => ListDeliveriesAsync(cursor, limit, status, ct: ct);

    public Task<ConnectorWebhookDeliveriesResponse> ListDeliveriesAsync(
        string? cursor = null,
        int? limit = null,
        string? status = null,
        string? customerRef = null,
        string? type = null,
        bool? test = null,
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        var query = HttpRequestor.BuildQuery(
            ("cursor", cursor),
            ("limit", limit?.ToString()),
            ("status", status?.ToUpperInvariant()),
            ("customerRef", customerRef),
            ("type", type),
            ("test", test?.ToString().ToLowerInvariant()),
            ("from", from),
            ("to", to));
        return _http.RequestAsync<ConnectorWebhookDeliveriesResponse>(
            HttpMethod.Get,
            $"/connector/webhook/deliveries{query}",
            ct,
            omitFirmId: true);
    }

    public Task<ConnectorWebhookDeliveryDetail> GetDeliveryAsync(
        string deliveryId,
        CancellationToken ct = default)
        => _http.RequestAsync<ConnectorWebhookDeliveryDetail>(
            HttpMethod.Get,
            $"/connector/webhook/deliveries/{Uri.EscapeDataString(deliveryId)}",
            ct,
            omitFirmId: true);

    public Task<ConnectorWebhookReplayResult> ReplayDeliveryAsync(
        string deliveryId,
        string idempotencyKey,
        bool confirmSuccessfulReplay = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Connector replay idempotencyKey is required.", nameof(idempotencyKey));
        return _http.RequestIdempotentAsync<ConnectorWebhookReplayResult>(
            HttpMethod.Post,
            $"/connector/webhook/deliveries/{Uri.EscapeDataString(deliveryId)}/replay",
            new { confirmSuccessfulReplay },
            idempotencyKey.Trim(),
            ct,
            omitFirmId: true);
    }

    public Task<ConnectorWebhookTestSuiteAccepted> RunTestSuiteAsync(
        string customerRef,
        string idempotencyKey,
        string? @event = null,
        IReadOnlyList<string>? scenarios = null,
        CancellationToken ct = default)
    {
        var normalizedCustomerRef = ConnectorResource.TrimString(customerRef ?? "");
        if (normalizedCustomerRef.Length == 0)
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Connector test-suite idempotencyKey is required.", nameof(idempotencyKey));
        return _http.RequestIdempotentAsync<ConnectorWebhookTestSuiteAccepted>(
            HttpMethod.Post,
            "/connector/webhook/test-suite",
            new { customerRef = normalizedCustomerRef, @event, scenarios },
            idempotencyKey.Trim(),
            ct,
            omitFirmId: true);
    }

    public Task<ConnectorWebhookTestSuiteStatus> GetTestSuiteAsync(
        string testRunId,
        CancellationToken ct = default)
        => _http.RequestAsync<ConnectorWebhookTestSuiteStatus>(
            HttpMethod.Get,
            $"/connector/webhook/test-suite/{Uri.EscapeDataString(testRunId)}",
            ct,
            omitFirmId: true);
}
