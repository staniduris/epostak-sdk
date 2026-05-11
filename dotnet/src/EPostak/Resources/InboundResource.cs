using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Pull API for received (inbound) documents.
/// Use this resource to list, retrieve, download UBL XML, and acknowledge
/// inbound Peppol documents.
/// Requires an API key with the <c>documents:read</c> scope
/// and an <c>api-enterprise</c> or <c>integrator-managed</c> plan.
/// </summary>
public sealed class InboundResource
{
    private readonly HttpRequestor _http;

    internal InboundResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// List inbound documents using cursor-based pagination.
    /// Returns up to <c>Limit</c> (default 100, max 500) documents, newest first.
    /// Advance through pages by passing the returned <c>NextCursor</c> as
    /// <see cref="InboundListParams.Cursor"/> in the next call.
    /// </summary>
    /// <param name="params">Optional filters: cursor, limit, kind, sender, since.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of inbound documents with cursor and <c>HasMore</c> flag.</returns>
    /// <example>
    /// <code>
    /// string? cursor = null;
    /// do
    /// {
    ///     var page = await client.Inbound.ListAsync(new InboundListParams { Cursor = cursor, Limit = 100 });
    ///     foreach (var doc in page.Documents)
    ///         Console.WriteLine($"{doc.Id}: {doc.Kind} from {doc.SenderPeppolId}");
    ///     cursor = page.NextCursor;
    /// } while (page.HasMore);
    /// </code>
    /// </example>
    public Task<InboundListResponse> ListAsync(InboundListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()),
            ("kind", @params?.Kind),
            ("sender", @params?.Sender),
            ("since", @params?.Since));
        return _http.RequestAsync<InboundListResponse>(HttpMethod.Get, $"/inbound/documents{qs}", ct);
    }

    /// <summary>
    /// Get a single inbound document by UUID.
    /// Returns 404 if the document does not belong to the authenticated firm.
    /// </summary>
    /// <param name="id">Inbound document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The inbound document with all metadata and party details.</returns>
    /// <example>
    /// <code>
    /// var doc = await client.Inbound.GetAsync("doc_uuid");
    /// Console.WriteLine($"Received: {doc.IssueDate}, Amount: {doc.AmountDue} {doc.Currency}");
    /// </code>
    /// </example>
    public Task<InboundDocument> GetAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<InboundDocument>(HttpMethod.Get, $"/inbound/documents/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// Download the raw UBL 2.1 XML for an inbound document.
    /// Returns the original Peppol BIS 3.0 XML as received on the network.
    /// Returns 404 for legacy rows that pre-date raw XML storage.
    /// </summary>
    /// <param name="id">Inbound document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>UBL 2.1 XML string (<c>application/xml</c>).</returns>
    /// <example>
    /// <code>
    /// var xml = await client.Inbound.GetUblAsync("doc_uuid");
    /// File.WriteAllText("received_invoice.xml", xml);
    /// </code>
    /// </example>
    public Task<string> GetUblAsync(string id, CancellationToken ct = default)
        => _http.RequestStringAsync(HttpMethod.Get, $"/inbound/documents/{Uri.EscapeDataString(id)}/ubl", ct);

    /// <summary>
    /// Acknowledge an inbound document. Marks the document as processed in the
    /// Pull API (status transitions from <c>received</c> to <c>acked</c>).
    /// This is idempotent — calling it again overwrites <c>ClientAckedAt</c>
    /// and <c>ClientReference</c> with the latest values.
    /// Requires the <c>documents:write</c> scope.
    /// </summary>
    /// <param name="id">Inbound document UUID.</param>
    /// <param name="params">Optional body with <c>ClientReference</c> (max 256 chars).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full document with updated <c>ClientAckedAt</c> and <c>ClientReference</c>.</returns>
    /// <example>
    /// <code>
    /// var doc = await client.Inbound.AckAsync("doc_uuid", new InboundAckParams
    /// {
    ///     ClientReference = "our-internal-id-12345"
    /// });
    /// Console.WriteLine($"Acked at: {doc.ClientAckedAt}, ref: {doc.ClientReference}");
    /// </code>
    /// </example>
    public Task<InboundDocument> AckAsync(string id, InboundAckParams? @params = null, CancellationToken ct = default)
        => _http.RequestAsync<InboundDocument>(HttpMethod.Post, $"/inbound/documents/{Uri.EscapeDataString(id)}/ack", @params ?? new InboundAckParams(), ct);
}
