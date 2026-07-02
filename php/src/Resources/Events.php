<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;

class Events
{
    public function __construct(private HttpClient $http)
    {
    }

    public function pull(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'event_type' => $params['event_type'] ?? null,
        ]);
        return $this->normalizePullResponse($this->http->request('GET', '/events/pull' . $qs) ?? []);
    }

    public function ack(string $eventId): array
    {
        return $this->http->request('POST', '/events/' . urlencode($eventId) . '/ack');
    }

    public function batchAck(array $eventIds): array
    {
        return $this->http->request('POST', '/events/batch-ack', [
            'json' => ['event_ids' => $eventIds],
        ]);
    }

    private function normalizePullResponse(array $response): array
    {
        if (!array_key_exists('items', $response) && isset($response['events']) && is_array($response['events'])) {
            $response['items'] = $response['events'];
        }
        return $response;
    }
}
