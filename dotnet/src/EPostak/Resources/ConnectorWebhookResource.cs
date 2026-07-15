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

    public Task<ConnectorWebhookTestResponse> TestAsync(string customerRef, CancellationToken ct = default)
    {
        var normalizedCustomerRef = ConnectorResource.TrimString(customerRef ?? "");
        if (normalizedCustomerRef.Length == 0)
            throw new ArgumentException("Connector customerRef is required.", nameof(customerRef));
        return _http.RequestAsync<ConnectorWebhookTestResponse>(
            HttpMethod.Post,
            "/connector/webhook/test",
            new Dictionary<string, object?> { ["customerRef"] = normalizedCustomerRef },
            ct,
            omitFirmId: true);
    }

    public Task<ConnectorWebhookDeliveriesResponse> DeliveriesAsync(
        string? cursor = null,
        int? limit = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var query = HttpRequestor.BuildQuery(
            ("cursor", cursor),
            ("limit", limit?.ToString()),
            ("status", status?.ToUpperInvariant()));
        return _http.RequestAsync<ConnectorWebhookDeliveriesResponse>(
            HttpMethod.Get,
            $"/connector/webhook/deliveries{query}",
            ct,
            omitFirmId: true);
    }
}
