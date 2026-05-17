namespace EPostak.Resources;

/// <summary>SAPI-SK 1.0 interoperable document send/receive endpoints.</summary>
public sealed class SapiResource
{
    private readonly HttpRequestor _http;

    internal SapiResource(HttpRequestor http) => _http = http;

    public Task<Dictionary<string, object?>> SendAsync(
        Dictionary<string, object?> body,
        string participantId,
        string idempotencyKey,
        CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Post,
            "/sapi/v1/document/send",
            body,
            idempotencyKey,
            new Dictionary<string, string> { ["X-Peppol-Participant-Id"] = participantId },
            ct);

    public Task<Dictionary<string, object?>> ReceiveAsync(
        string participantId,
        int? limit = null,
        string? status = null,
        string? pageToken = null,
        CancellationToken ct = default)
    {
        var qs = HttpRequestor.BuildQuery(
            ("limit", limit?.ToString()),
            ("status", status),
            ("pageToken", pageToken));
        return _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/sapi/v1/document/receive{qs}",
            new Dictionary<string, string> { ["X-Peppol-Participant-Id"] = participantId },
            ct);
    }

    public Task<Dictionary<string, object?>> GetAsync(string documentId, string participantId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Get,
            $"/sapi/v1/document/receive/{Uri.EscapeDataString(documentId)}",
            new Dictionary<string, string> { ["X-Peppol-Participant-Id"] = participantId },
            ct);

    public Task<Dictionary<string, object?>> AcknowledgeAsync(string documentId, string participantId, CancellationToken ct = default)
        => _http.RequestAsync<Dictionary<string, object?>>(
            HttpMethod.Post,
            $"/sapi/v1/document/receive/{Uri.EscapeDataString(documentId)}/acknowledge",
            new Dictionary<string, string> { ["X-Peppol-Participant-Id"] = participantId },
            ct);
}
