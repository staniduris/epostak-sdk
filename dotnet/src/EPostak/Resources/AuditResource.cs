using EPostak.Models;

namespace EPostak.Resources;

/// <summary>
/// Resource for the per-firm security/auth audit feed (Wave 3.4).
/// <para>
/// Tenant-isolated: every row is filtered by the firm the calling key is
/// bound to. Integrators with multiple managed firms see only the firm
/// specified by <c>X-Firm-Id</c> (set automatically on the client when you
/// pass <c>FirmId</c> to <see cref="EPostakConfig"/> or use
/// <see cref="EPostakClient.WithFirm"/>).
/// </para>
/// <para>
/// Cursor pagination over <c>(occurred_at DESC, id DESC)</c> — pass the
/// <see cref="CursorPage{T}.NextCursor"/> from one page back into the next
/// call to walk the feed deterministically, even across rows with identical
/// timestamps.
/// </para>
/// </summary>
/// <example>
/// <code>
/// string? cursor = null;
/// do
/// {
///     var page = await client.Audit.ListAsync(new AuditListParams
///     {
///         Event = "jwt.issued",
///         Since = "2026-04-01T00:00:00Z",
///         Cursor = cursor,
///         Limit = 50,
///     });
///     foreach (var ev in page.Items)
///         Console.WriteLine($"{ev.OccurredAt}: {ev.Event} ({ev.ActorId})");
///     cursor = page.NextCursor;
/// } while (cursor is not null);
/// </code>
/// </example>
public sealed class AuditResource
{
    private readonly HttpRequestor _http;

    internal AuditResource(HttpRequestor http) => _http = http;

    /// <summary>
    /// List audit events for the current firm. Cursor-paginated.
    /// </summary>
    /// <param name="params">Optional filters and pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<CursorPage<AuditEvent>> ListAsync(AuditListParams? @params = null, CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("event", @params?.Event),
            ("actor_type", ActorTypeToString(@params?.ActorType)),
            ("since", @params?.Since),
            ("until", @params?.Until),
            ("cursor", @params?.Cursor),
            ("limit", @params?.Limit?.ToString()));
        return _http.RequestAsync<CursorPage<AuditEvent>>(HttpMethod.Get, $"/audit{qs}", ct);
    }

    private static string? ActorTypeToString(AuditActorType? t) => t switch
    {
        AuditActorType.User => "user",
        AuditActorType.ApiKey => "apiKey",
        AuditActorType.IntegratorKey => "integratorKey",
        AuditActorType.System => "system",
        _ => null,
    };
}
