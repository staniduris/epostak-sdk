using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Connector workflow endpoints for ERP teams.
/// Connector is a polling-first workflow over the Enterprise API.
/// </summary>
public sealed class ConnectorResource
{
    private readonly HttpRequestor _http;

    internal ConnectorResource(HttpRequestor http) => _http = http;

    /// <summary>Validate receiver reachability and payload readiness before sending.</summary>
    public Task<ConnectorPreflightResponse> PreflightAsync(ConnectorPreflightRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorPreflightResponse>(HttpMethod.Post, "/connector/preflight", request, ct);

    /// <summary>Send an ERP document payload through Connector.</summary>
    public Task<ConnectorSendResponse> SendAsync(ConnectorSendRequest request, CancellationToken ct = default)
        => SendAsync(request, idempotencyKey: null, ct);

    /// <summary>Send an ERP document payload through Connector with an optional Idempotency-Key header.</summary>
    public Task<ConnectorSendResponse> SendAsync(ConnectorSendRequest request, string? idempotencyKey, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorSendResponse>(HttpMethod.Post, "/connector/send", request, idempotencyKey, ct);

    /// <summary>Get Connector status for a document ID.</summary>
    public Task<ConnectorStatusResponse> StatusAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorStatusResponse>(HttpMethod.Get, $"/connector/status/{Uri.EscapeDataString(documentId)}", ct);

    /// <summary>List Connector inbox documents with cursor pagination.</summary>
    public Task<ConnectorInboxListResponse> InboxAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorInboxListResponse>(HttpMethod.Get, $"/connector/inbox{qs}", ct);
    }

    /// <summary>Retrieve a single Connector inbox document.</summary>
    public Task<ConnectorInboxDocument> GetInboxDocumentAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorInboxDocument>(HttpMethod.Get, $"/connector/inbox/{Uri.EscapeDataString(documentId)}", ct);

    /// <summary>Acknowledge a Connector inbox document as processed.</summary>
    public Task<ConnectorAckResponse> AckAsync(string documentId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorAckResponse>(
            HttpMethod.Post,
            $"/connector/inbox/{Uri.EscapeDataString(documentId)}/ack",
            new { },
            ct);

    /// <summary>List Connector polling events with cursor pagination.</summary>
    public Task<ConnectorEventsResponse> EventsAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<ConnectorEventsResponse>(HttpMethod.Get, $"/connector/events{qs}", ct);
    }

    /// <summary>Stage one or more ERP invoices without immediate Peppol delivery.</summary>
    public Task<ConnectorOutboxStageResponse> StageOutboxAsync(ConnectorOutboxStageRequest request, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxStageResponse>(HttpMethod.Post, "/connector/outbox", request, ct);

    /// <summary>List staged Connector outbox items.</summary>
    public Task<ConnectorOutboxListResponse> ListOutboxAsync(ConnectorOutboxListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("status", @params?.Status),
            ("limit", @params?.Limit?.ToString()),
            ("offset", @params?.Offset?.ToString()));
        return _http.RequestAsync<ConnectorOutboxListResponse>(HttpMethod.Get, $"/connector/outbox{qs}", ct);
    }

    /// <summary>Retrieve a single Connector outbox item.</summary>
    public Task<ConnectorOutboxItem> GetOutboxItemAsync(string outboxId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxItem>(HttpMethod.Get, $"/connector/outbox/{Uri.EscapeDataString(outboxId)}", ct);

    /// <summary>Send one staged outbox item through the Connector workflow.</summary>
    public Task<ConnectorOutboxItem> SendOutboxItemAsync(string outboxId, ConnectorOutboxSendOptions? options = null, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxItem>(
            HttpMethod.Post,
            $"/connector/outbox/{Uri.EscapeDataString(outboxId)}/send",
            options ?? new ConnectorOutboxSendOptions(),
            ct);

    /// <summary>Send ready, failed, or due scheduled outbox items in a batch.</summary>
    public Task<ConnectorOutboxBatchSendResponse> SendOutboxBatchAsync(ConnectorOutboxBatchSendRequest? request = null, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxBatchSendResponse>(
            HttpMethod.Post,
            "/connector/outbox/send",
            request ?? new ConnectorOutboxBatchSendRequest(),
            ct);

    /// <summary>Cancel a staged outbox item before it is sent.</summary>
    public Task<ConnectorOutboxItem> CancelOutboxItemAsync(string outboxId, CancellationToken ct = default)
        => _http.RequestAsync<ConnectorOutboxItem>(HttpMethod.Delete, $"/connector/outbox/{Uri.EscapeDataString(outboxId)}", ct);
}
