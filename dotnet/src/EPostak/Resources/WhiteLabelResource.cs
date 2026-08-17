using EPostak.Models;
using System.Text;

namespace EPostak.Resources;

/// <summary>Integrator-scoped White Label participant registration and migration.</summary>
public sealed class WhiteLabelResource
{
    private readonly HttpRequestor _http;

    internal WhiteLabelResource(HttpRequestor http) => _http = http;

    public Task<WhiteLabelParticipantList> ListParticipantsAsync(
        WhiteLabelListParticipantsParams? parameters = null,
        CancellationToken ct = default)
    {
        var query = HttpRequestor.BuildQuery(
            ("limit", parameters?.Limit?.ToString()),
            ("cursor", parameters?.Cursor));
        return _http.RequestAsync<WhiteLabelParticipantList>(
            HttpMethod.Get,
            $"/white-label/participants{query}",
            ct,
            omitFirmId: true);
    }

    public Task<WhiteLabelParticipantOperation> RegisterParticipantAsync(
        WhiteLabelParticipantRegistrationRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
        => _http.RequestIdempotentAsync<WhiteLabelParticipantOperation>(
            HttpMethod.Post,
            "/white-label/participants/registrations",
            request,
            IdempotencyKey(idempotencyKey),
            ct,
            omitFirmId: true);

    public Task<WhiteLabelParticipantOperation> MigrateParticipantAsync(
        WhiteLabelParticipantMigrationRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
        => _http.RequestIdempotentAsync<WhiteLabelParticipantOperation>(
            HttpMethod.Post,
            "/white-label/participants/migrations",
            request,
            IdempotencyKey(idempotencyKey),
            ct,
            omitFirmId: true);

    public Task<WhiteLabelParticipant> GetParticipantAsync(
        string participantId,
        CancellationToken ct = default)
        => _http.RequestAsync<WhiteLabelParticipant>(
            HttpMethod.Get,
            $"/white-label/participants/{Uri.EscapeDataString(participantId)}",
            ct,
            omitFirmId: true);

    public Task<WhiteLabelMigrationCodeResponse> RequestMigrationCodeAsync(
        string participantId,
        string idempotencyKey,
        CancellationToken ct = default)
        => _http.RequestIdempotentAsync<WhiteLabelMigrationCodeResponse>(
            HttpMethod.Post,
            $"/white-label/participants/{Uri.EscapeDataString(participantId)}/migration-code",
            IdempotencyKey(idempotencyKey),
            ct,
            omitFirmId: true);

    public Task<WhiteLabelParticipantOperation> GetOperationAsync(
        string operationId,
        CancellationToken ct = default)
        => _http.RequestAsync<WhiteLabelParticipantOperation>(
            HttpMethod.Get,
            $"/white-label/operations/{Uri.EscapeDataString(operationId)}",
            ct,
            omitFirmId: true);

    private static string IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > 255)
            throw new ArgumentException("White Label idempotency key must be 1-255 UTF-8 bytes.", nameof(value));
        return value;
    }
}
