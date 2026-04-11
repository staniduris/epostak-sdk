using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Access received (inbound) documents from the Peppol network.
/// Provides listing, retrieval, and acknowledgement of incoming invoices
/// and other business documents.
/// </summary>
public sealed class InboxResource
{
    private readonly HttpRequestor _http;

    internal InboxResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// List inbox documents with optional filtering by status or date.
    /// Results are paginated -- use <c>Offset</c> and <c>Limit</c> to page through results.
    /// </summary>
    /// <param name="params">Optional filters: pagination, status (RECEIVED/ACKNOWLEDGED), and date cutoff.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated list of inbox documents with total count.</returns>
    /// <example>
    /// <code>
    /// var inbox = await client.Documents.Inbox.ListAsync(new InboxListParams
    /// {
    ///     Status = InboxStatus.RECEIVED,
    ///     Limit = 10
    /// });
    /// foreach (var doc in inbox.Documents)
    ///     Console.WriteLine($"{doc.Number} from {doc.Supplier.Name}: {doc.Totals.WithVat} {doc.Currency}");
    /// </code>
    /// </example>
    public Task<InboxListResponse> ListAsync(InboxListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("offset", @params?.Offset?.ToString()),
            ("limit", @params?.Limit?.ToString()),
            ("status", @params?.Status?.ToString()),
            ("since", @params?.Since));
        return _http.RequestAsync<InboxListResponse>(HttpMethod.Get, $"/documents/inbox{qs}", ct);
    }

    /// <summary>
    /// Get a single inbox document including the full UBL XML payload.
    /// Use this to retrieve the raw Peppol BIS 3.0 XML for processing in your system.
    /// </summary>
    /// <param name="id">The inbox document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The document metadata and UBL XML payload (null if not yet available).</returns>
    /// <example>
    /// <code>
    /// var detail = await client.Documents.Inbox.GetAsync("doc_abc123");
    /// if (detail.Payload is not null)
    ///     File.WriteAllText("received_invoice.xml", detail.Payload);
    /// </code>
    /// </example>
    public Task<InboxDocumentDetailResponse> GetAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<InboxDocumentDetailResponse>(HttpMethod.Get, $"/documents/inbox/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// Acknowledge receipt of an inbox document. This marks the document as processed
    /// in your system. Acknowledged documents won't appear in unprocessed filters
    /// and won't trigger repeated webhook/queue events.
    /// </summary>
    /// <param name="id">The inbox document UUID to acknowledge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Confirmation with the document ID, new status, and acknowledgement timestamp.</returns>
    /// <example>
    /// <code>
    /// var ack = await client.Documents.Inbox.AcknowledgeAsync("doc_abc123");
    /// Console.WriteLine($"Acknowledged at: {ack.AcknowledgedAt}");
    /// </code>
    /// </example>
    public Task<AcknowledgeResponse> AcknowledgeAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<AcknowledgeResponse>(HttpMethod.Post, $"/documents/inbox/{Uri.EscapeDataString(id)}/acknowledge", ct);

    /// <summary>
    /// List inbox documents across all firms managed by an integrator key.
    /// Only available with integrator API keys (<c>sk_int_*</c>). Returns documents
    /// from all assigned firms with the firm ID and name on each entry.
    /// </summary>
    /// <param name="params">Optional filters: pagination, status, date cutoff, and specific firm UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated list of inbox documents across all firms with total count.</returns>
    /// <example>
    /// <code>
    /// // Get all unacknowledged documents across firms
    /// var all = await client.Documents.Inbox.ListAllAsync(new InboxAllParams
    /// {
    ///     Status = InboxStatus.RECEIVED,
    ///     Limit = 50
    /// });
    /// foreach (var doc in all.Documents)
    ///     Console.WriteLine($"[{doc.FirmName}] {doc.Number}: {doc.Totals.WithVat} {doc.Currency}");
    /// </code>
    /// </example>
    public Task<InboxAllResponse> ListAllAsync(InboxAllParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("offset", @params?.Offset?.ToString()),
            ("limit", @params?.Limit?.ToString()),
            ("status", @params?.Status?.ToString()),
            ("since", @params?.Since),
            ("firm_id", @params?.FirmId));
        return _http.RequestAsync<InboxAllResponse>(HttpMethod.Get, $"/documents/inbox/all{qs}", ct);
    }
}
