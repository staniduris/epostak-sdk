<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Pull-based webhook event queue.
 *
 * An alternative to push webhooks: poll for pending events and acknowledge
 * them after processing. Access via `$client->webhooks->queue`.
 */
class WebhookQueue
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Pull pending webhook events from the queue.
     *
     * Events must be acknowledged after processing via ack() or batchAck().
     * Unacknowledged events will be returned again on the next pull.
     *
     * @param array{limit?: int, event_type?: string} $params Optional filters:
     *   - `limit`      Max events to return (default 20).
     *   - `event_type` Filter to a specific event type (e.g. 'document.received').
     * @return array{items: array, has_more: bool} Pending events and pagination flag.
     * @throws EPostakError On API error.
     *
     * @example
     *   $result = $client->webhooks->queue->pull(['limit' => 50]);
     *   foreach ($result['items'] as $item) {
     *       processEvent($item);
     *       $client->webhooks->queue->ack($item['event_id']);
     *   }
     */
    public function pull(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'event_type' => $params['event_type'] ?? null,
        ]);
        return $this->http->request('GET', '/webhook-queue' . $qs);
    }

    /**
     * Acknowledge a single event, removing it from the queue.
     *
     * @param string $eventId Event UUID to acknowledge.
     * @return array{acknowledged: true} Acknowledgement confirmation.
     * @throws EPostakError On API error.
     */
    public function ack(string $eventId): array
    {
        return $this->http->request('DELETE', '/webhook-queue/' . urlencode($eventId));
    }

    /**
     * Batch acknowledge multiple events, removing them from the queue.
     *
     * Max 1000 event IDs per call.
     *
     * @param string[] $eventIds Array of event UUIDs to acknowledge (max 1000).
     * @return array{acknowledged: int} Count of acknowledged events.
     * @throws EPostakError On API error.
     */
    public function batchAck(array $eventIds): array
    {
        return $this->http->request('POST', '/webhook-queue/batch-ack', [
            'json' => ['event_ids' => $eventIds],
        ]);
    }

    /**
     * Pull events across all firms (integrator only).
     *
     * Requires an integrator API key. Returns events from all assigned firms.
     *
     * @param array{limit?: int, since?: string} $params Optional filters:
     *   - `limit` Max events to return (default 100, max 500).
     *   - `since` ISO 8601 datetime cutoff.
     * @return array{items: list<array{event_id: string, firm_id: string, event: string, payload: array, created_at: string}>, has_more: bool}
     *   Pending events from all firms; `has_more` indicates more events are available.
     * @throws EPostakError On API error.
     */
    public function pullAll(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'since' => $params['since'] ?? null,
        ]);
        return $this->http->request('GET', '/webhook-queue/all' . $qs);
    }

    /**
     * Batch acknowledge events across all firms (integrator only).
     *
     * Requires an integrator API key. Max 1000 event IDs per call.
     *
     * @param string[] $eventIds Array of event UUIDs to acknowledge (max 1000).
     * @return array{acknowledged: int} Count of acknowledged events.
     * @throws EPostakError On API error.
     */
    public function batchAckAll(array $eventIds): array
    {
        return $this->http->request('POST', '/webhook-queue/all/batch-ack', [
            'json' => ['event_ids' => $eventIds],
        ]);
    }
}
