<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;

/** Integrator-scoped White Label participant registration and migration. */
class WhiteLabel
{
    public function __construct(private HttpClient $http)
    {
    }

    /** @param array{limit?: int, cursor?: string} $params */
    public function listParticipants(array $params = []): array
    {
        $query = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'cursor' => $params['cursor'] ?? null,
        ]);
        return $this->http->request('GET', '/white-label/participants' . $query, [
            'omitFirmId' => true,
        ]) ?? [];
    }

    /**
     * @param array{customerRef: string, dic: string, companyEmail: string, verificationToken: string} $request
     */
    public function registerParticipant(array $request, string $idempotencyKey): array
    {
        return $this->postIdempotent('/white-label/participants/registrations', $request, $idempotencyKey);
    }

    /**
     * @param array{customerRef: string, dic: string, companyEmail: string, migrationCode: string} $request
     */
    public function migrateParticipant(array $request, string $idempotencyKey): array
    {
        return $this->postIdempotent('/white-label/participants/migrations', $request, $idempotencyKey);
    }

    public function getParticipant(string $participantId): array
    {
        return $this->http->request(
            'GET',
            '/white-label/participants/' . rawurlencode($participantId),
            ['omitFirmId' => true]
        ) ?? [];
    }

    public function requestMigrationCode(string $participantId, string $idempotencyKey): array
    {
        $key = $this->idempotencyKey($idempotencyKey);
        return $this->http->request(
            'POST',
            '/white-label/participants/' . rawurlencode($participantId) . '/migration-code',
            [
                'headers' => ['Idempotency-Key' => $key],
                'omitFirmId' => true,
                'retryOnFailure' => true,
            ]
        ) ?? [];
    }

    public function getOperation(string $operationId): array
    {
        return $this->http->request(
            'GET',
            '/white-label/operations/' . rawurlencode($operationId),
            ['omitFirmId' => true]
        ) ?? [];
    }

    private function postIdempotent(string $path, array $request, string $idempotencyKey): array
    {
        $key = $this->idempotencyKey($idempotencyKey);
        return $this->http->request('POST', $path, [
            'json' => $request,
            'headers' => ['Idempotency-Key' => $key],
            'omitFirmId' => true,
            'retryOnFailure' => true,
        ]) ?? [];
    }

    private function idempotencyKey(string $value): string
    {
        if (trim($value) === '' || strlen($value) > 255) {
            throw new \InvalidArgumentException('White Label idempotency key must be 1-255 UTF-8 bytes');
        }
        return $value;
    }
}
