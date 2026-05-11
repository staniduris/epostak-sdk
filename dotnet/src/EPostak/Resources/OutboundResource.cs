using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Pull API for sent (outbound) documents and their event stream.
/// Use this resource to list, retrieve, and download UBL XML for outbound
/// Peppol documents, and to stream the outbound event cursor.
/// Requires an API key with the <c>documents:read</c> scope and an
/// <c>api-enterprise</c> or <c>integrator-managed</c> plan.
/// </summary>
public sealed class OutboundResource
{
    private readonly HttpRequestor _http;

    internal OutboundResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// List outbound documents using cursor-based pagination.
    /// Returns up to <c>Limit</c> (default 100, max 500) documents, newest first.
    /// </summary>
    /// <param name="params">Optional filters: cursor, limit, kind, status, business_status, recipient, since.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of outbound documents with cursor and <c>HasMore</c> flag.</returns>
    /// <example>
    /// <code>
    /// var page = await client.Outbound.ListAsync(new OutboundListParams
    /// {
    ///     Status = "delivered",
    ///     Limit = 50
    /// });
    /// foreach (var doc in page.Documents)
    ///     Console.WriteLine($"{doc.Id}: {doc.Status} → {doc.ReceiverPeppolId}");
    /// </code>
    /// </example>
    public Task<OutboundListResponse> ListAsync(OutboundListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()),
            ("kind", @params?.Kind),
            ("status", @params?.Status),
            ("business_status", @params?.BusinessStatus),
            ("recipient", @params?.Recipient),
            ("since", @params?.Since));
        return _http.RequestAsync<OutboundListResponse>(HttpMethod.Get, $"/outbound/documents{qs}", ct);
    }

    /// <summary>
    /// Get a single outbound document by UUID.
    /// The detail view includes <c>AttemptHistory</c> (delivery attempts),
    /// which is absent from list responses.
    /// Returns 404 if the document does not belong to the authenticated firm.
    /// </summary>
    /// <param name="id">Outbound document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outbound document with all metadata and delivery attempt history.</returns>
    public Task<OutboundDocument> GetAsync(string id, CancellationToken ct = default)
        => _http.RequestAsync<OutboundDocument>(HttpMethod.Get, $"/outbound/documents/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// Download the raw UBL 2.1 XML for an outbound document.
    /// Returns the canonical UBL 2.1 XML that was transmitted on the Peppol network.
    /// Returns 404 for legacy rows that pre-date UBL storage.
    /// </summary>
    /// <param name="id">Outbound document UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>UBL 2.1 XML string (<c>application/xml</c>).</returns>
    public Task<string> GetUblAsync(string id, CancellationToken ct = default)
        => _http.RequestStringAsync(HttpMethod.Get, $"/outbound/documents/{Uri.EscapeDataString(id)}/ubl", ct);

    /// <summary>
    /// Stream outbound document events using cursor-based pagination.
    /// Returns a time-ordered cursor of delivery status changes and other
    /// lifecycle events. Currently covers invoice-backed documents only.
    /// Use <see cref="OutboundEventsParams.DocumentId"/> to narrow to a specific document.
    /// </summary>
    /// <param name="params">Optional filters: cursor, limit, document_id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of outbound events with cursor and <c>HasMore</c> flag.</returns>
    /// <example>
    /// <code>
    /// var events = await client.Outbound.EventsAsync(new OutboundEventsParams
    /// {
    ///     DocumentId = "doc_uuid",
    ///     Limit = 20
    /// });
    /// foreach (var ev in events.Events)
    ///     Console.WriteLine($"{ev.OccurredAt}: {ev.Type} — {ev.Detail}");
    /// </code>
    /// </example>
    public Task<OutboundEventsResponse> EventsAsync(OutboundEventsParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()),
            ("document_id", @params?.DocumentId));
        return _http.RequestAsync<OutboundEventsResponse>(HttpMethod.Get, $"/outbound/events{qs}", ct);
    }
}
