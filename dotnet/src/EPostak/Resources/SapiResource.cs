namespace EPostak.Resources;

/// <summary>SAPI-SK 1.0 interoperable document send/receive endpoints.</summary>
public sealed class SapiResource
{
    private readonly HttpRequestor _http;

    internal SapiResource(HttpRequestor http)
    {
        _http = http;
        Participants = new SapiParticipantsResource(this);
    }

    public SapiParticipantsResource Participants { get; }

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

public sealed class SapiParticipantsResource
{
    private readonly SapiResource _sapi;

    internal SapiParticipantsResource(SapiResource sapi) => _sapi = sapi;

    public SapiParticipantResource For(string participantId) => new(_sapi, participantId);
}

public sealed class SapiParticipantResource
{
    public SapiParticipantDocumentsResource Documents { get; }

    internal SapiParticipantResource(SapiResource sapi, string participantId)
        => Documents = new SapiParticipantDocumentsResource(sapi, participantId);
}

public sealed class SapiParticipantDocumentsResource
{
    private readonly SapiResource _sapi;
    private readonly string _participantId;

    internal SapiParticipantDocumentsResource(SapiResource sapi, string participantId)
    {
        _sapi = sapi;
        _participantId = participantId;
    }

    public Task<Dictionary<string, object?>> SendAsync(
        Dictionary<string, object?> body,
        string idempotencyKey,
        CancellationToken ct = default)
        => _sapi.SendAsync(body, _participantId, idempotencyKey, ct);

    public Task<Dictionary<string, object?>> ReceiveAsync(
        int? limit = null,
        string? status = null,
        string? pageToken = null,
        CancellationToken ct = default)
        => _sapi.ReceiveAsync(_participantId, limit, status, pageToken, ct);

    public Task<Dictionary<string, object?>> GetAsync(string documentId, CancellationToken ct = default)
        => _sapi.GetAsync(documentId, _participantId, ct);

    public Task<Dictionary<string, object?>> AcknowledgeAsync(string documentId, CancellationToken ct = default)
        => _sapi.AcknowledgeAsync(documentId, _participantId, ct);
}
