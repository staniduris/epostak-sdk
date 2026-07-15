using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Explicit opt-in surface for lower-level and compatibility Connector APIs.
/// Most ERP integrations only need
/// <c>Connector.Customers.For(customerRef).Documents</c> and <c>Events</c>.
/// </summary>
public sealed class ConnectorAdvancedResource
{
    private readonly ConnectorResource _connector;

    internal ConnectorAdvancedResource(ConnectorResource connector)
    {
        _connector = connector;
        Documents = new ConnectorAdvancedDocumentsResource(connector);
    }

    /// <summary>UBL and evidence artifacts kept outside the golden business flow.</summary>
    public ConnectorAdvancedDocumentsResource Documents { get; }


    public Task<ConnectorPreflightResponse> PreflightAsync(ConnectorPreflightRequest request, CancellationToken ct = default)
        => _connector.PreflightAsync(request, ct);

    public Task<ConnectorSendResponse> SendAsync(ConnectorSendRequest request, CancellationToken ct = default)
        => _connector.SendAsync(request, ct);

    public Task<ConnectorSendResponse> SendAsync(ConnectorSendRequest request, string? idempotencyKey, CancellationToken ct = default)
        => _connector.SendAsync(request, idempotencyKey, ct);

    public Task<ConnectorStatusResponse> StatusAsync(string documentId, CancellationToken ct = default)
        => _connector.StatusAsync(documentId, ct);

    public Task<ConnectorInboxListResponse> InboxAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
        => _connector.InboxAsync(@params, ct);

    public Task<ConnectorInboxDocument> GetInboxDocumentAsync(string documentId, CancellationToken ct = default)
        => _connector.GetInboxDocumentAsync(documentId, ct);

    public Task<ConnectorAckResponse> AckAsync(string documentId, CancellationToken ct = default)
        => _connector.AckAsync(documentId, ct);

    /// <summary>
    /// Compatibility event polling. Prefer
    /// <c>Connector.Customers.For(customerRef).Events.ListAsync(...)</c> for the
    /// customer-scoped business feed.
    /// </summary>
    public Task<ConnectorEventsResponse> EventsAsync(ConnectorListParams? @params = null, CancellationToken ct = default)
        => _connector.EventsAsync(@params, ct);

    public Task<ConnectorOutboxStageResponse> StageOutboxAsync(ConnectorOutboxStageRequest request, CancellationToken ct = default)
        => _connector.StageOutboxAsync(request, ct);

    public Task<ConnectorOutboxListResponse> ListOutboxAsync(ConnectorOutboxListParams? @params = null, CancellationToken ct = default)
        => _connector.ListOutboxAsync(@params, ct);

    public Task<ConnectorOutboxItem> GetOutboxItemAsync(string outboxId, CancellationToken ct = default)
        => _connector.GetOutboxItemAsync(outboxId, ct);

    public Task<ConnectorOutboxItem> SendOutboxItemAsync(string outboxId, ConnectorOutboxSendOptions? options = null, CancellationToken ct = default)
        => _connector.SendOutboxItemAsync(outboxId, options, ct);

    public Task<ConnectorOutboxBatchSendResponse> SendOutboxBatchAsync(ConnectorOutboxBatchSendRequest? request = null, CancellationToken ct = default)
        => _connector.SendOutboxBatchAsync(request, ct);

    public Task<ConnectorOutboxItem> CancelOutboxItemAsync(string outboxId, CancellationToken ct = default)
        => _connector.CancelOutboxItemAsync(outboxId, ct);

    public Task<ConnectorAutopilotRunResponse> AutopilotAsync(ConnectorAutopilotRequest request, CancellationToken ct = default)
        => _connector.AutopilotAsync(request, ct);

    public Task<Dictionary<string, object?>> MapperAsync(ConnectorMapperRequest request, CancellationToken ct = default)
        => _connector.MapperAsync(request, ct);

    public Task<ConnectorAutopilotRunResponse> ZenInputAsync(ConnectorZenInputRequest request, CancellationToken ct = default)
        => _connector.ZenInputAsync(request, ct);

    public Task<ConnectorAutopilotRunResponse> GetAutopilotRunAsync(string autopilotId, CancellationToken ct = default)
        => _connector.GetAutopilotRunAsync(autopilotId, ct);

    public Task<ConnectorAutopilotRunResponse> SendAutopilotRunAsync(string autopilotId, CancellationToken ct = default)
        => _connector.SendAutopilotRunAsync(autopilotId, ct);

    public Task<ConnectorReconcileResponse> ReconcileAsync(ConnectorReconcileParams? @params = null, CancellationToken ct = default)
        => _connector.ReconcileAsync(@params, ct);

    public Task<ConnectorMailboxListResponse> MailboxesAsync(CancellationToken ct = default)
        => _connector.MailboxesAsync(ct);

    public Task<Dictionary<string, object?>> RepairMailboxAsync(ConnectorMailboxRepairRequest? request = null, CancellationToken ct = default)
        => _connector.RepairMailboxAsync(request, ct);

    public Task<ConnectorMailboxUpdateResponse> UpdateMailboxSendPolicyAsync(string customerRef, ConnectorSendPolicyOptions request, CancellationToken ct = default)
        => _connector.UpdateMailboxSendPolicyAsync(customerRef, request, ct);

    public Task<ConnectorSyncResponse> SyncAsync(ConnectorSyncParams? @params = null, CancellationToken ct = default)
        => _connector.SyncAsync(@params, ct);

    public Task<ConnectorActionResponse> RunActionAsync(string actionId, ConnectorActionRequest? request = null, CancellationToken ct = default)
        => _connector.RunActionAsync(actionId, request, ct);

}
