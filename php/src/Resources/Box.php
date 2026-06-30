<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;

/**
 * ePošťák Box durable execution layer for staged, scheduled, and retryable
 * Peppol dispatch.
 */
class Box
{
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * @param array{status?: string, direction?: string, limit?: int, offset?: int} $params
     */
    public function list(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'status' => $params['status'] ?? null,
            'direction' => $params['direction'] ?? null,
            'limit' => $params['limit'] ?? null,
            'offset' => $params['offset'] ?? null,
        ]);
        return $this->http->request('GET', '/box/items' . $qs);
    }

    /**
     * @param array{payloadXml: string, scheduledFor?: string, externalId?: string, metadata?: array<string, mixed>} $body
     */
    public function create(array $body): array
    {
        return $this->http->request('POST', '/box/items', [
            'json' => $body,
        ]);
    }

    public function get(string $itemId): array
    {
        return $this->http->request('GET', '/box/items/' . urlencode($itemId));
    }

    public function schedule(string $itemId, string $scheduledFor): array
    {
        return $this->http->request('POST', '/box/items/' . urlencode($itemId) . '/schedule', [
            'json' => ['scheduledFor' => $scheduledFor],
        ]);
    }

    public function sendNow(string $itemId): array
    {
        return $this->http->request('POST', '/box/items/' . urlencode($itemId) . '/send-now');
    }

    public function retry(string $itemId): array
    {
        return $this->http->request('POST', '/box/items/' . urlencode($itemId) . '/retry');
    }

    public function cancel(string $itemId): array
    {
        return $this->http->request('POST', '/box/items/' . urlencode($itemId) . '/cancel');
    }
}
