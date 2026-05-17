<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;

/**
 * SAPI-SK 1.0 interoperable document send/receive endpoints.
 */
class Sapi
{
    private HttpClient $http;

    public function __construct(HttpClient $http)
    {
        $sapiBase = preg_replace('#/api/v1/?$#', '', $http->getBaseUrl()) ?? $http->getBaseUrl();
        $this->http = new HttpClient(
            $sapiBase,
            $http->getTokenManager(),
            $http->getFirmId(),
            $http->getMaxRetries()
        );
    }

    public function send(array $body, string $participantId, string $idempotencyKey): array
    {
        return $this->http->request('POST', '/sapi/v1/document/send', [
            'json' => $body,
            'headers' => [
                'X-Peppol-Participant-Id' => $participantId,
                'Idempotency-Key' => $idempotencyKey,
            ],
        ]);
    }

    public function receive(string $participantId, array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'status' => $params['status'] ?? null,
            'pageToken' => $params['pageToken'] ?? null,
        ]);
        return $this->http->request('GET', '/sapi/v1/document/receive' . $qs, [
            'headers' => ['X-Peppol-Participant-Id' => $participantId],
        ]);
    }

    public function get(string $documentId, string $participantId): array
    {
        return $this->http->request('GET', '/sapi/v1/document/receive/' . urlencode($documentId), [
            'headers' => ['X-Peppol-Participant-Id' => $participantId],
        ]);
    }

    public function acknowledge(string $documentId, string $participantId): array
    {
        return $this->http->request('POST', '/sapi/v1/document/receive/' . urlencode($documentId) . '/acknowledge', [
            'headers' => ['X-Peppol-Participant-Id' => $participantId],
        ]);
    }
}
