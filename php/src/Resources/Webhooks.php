<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Canonical webhook event types (v1 contract).
 *
 * @phpstan-type WebhookEvent
 *   'document.created'|'document.sent'|'document.received'|'document.validated'|
 *   'document.delivered'|'document.delivery_failed'|'document.rejected'|
 *   'document.response_received'
 */

/**
 * Business-data shape carried in every webhook event payload.
 *
 * Common fields are always present; event-specific extras are optional so a
 * single handler can branch on `event` without casts.
 *
 * @phpstan-type WebhookPayloadData array{
 *   document_id: string,
 *   direction: 'inbound'|'outbound',
 *   doctype_key: string,
 *   status: string,
 *   previous_status: string|null,
 *   document_number?: string|null,
 *   total_amount?: string|null,
 *   currency?: string|null,
 *   issue_date?: string|null,
 *   due_date?: string|null,
 *   sender_peppol_id?: string|null,
 *   receiver_peppol_id?: string|null,
 *   sent_at?: string,
 *   received_at?: string,
 *   delivered_at?: string,
 *   rejected_at?: string,
 *   responded_at?: string,
 *   as4_message_id?: string,
 *   response_code?: string,
 *   response_reason?: string,
 *   responder?: 'peer_ap'|'buyer'|'loopback',
 *   failure_reason?: string,
 *   attempts?: int
 * }
 */

/**
 * Common envelope shape for every v1 webhook payload (push POST body or pull
 * queue item).
 *
 * @phpstan-type WebhookPayloadEnvelope array{
 *   event: WebhookEvent,
 *   event_version: '1',
 *   webhook_id: string|null,
 *   webhook_event_id: string|null,
 *   timestamp: string,
 *   data: WebhookPayloadData
 * }
 */

/**
 * Manage webhook subscriptions for real-time event notifications.
 *
 * Access via `$client->webhooks`. The pull-based event queue is available
 * through the nested `$client->webhooks->queue` resource.
 */
class Webhooks
{
    /** @var WebhookQueue Pull-based webhook event queue operations. */
    public WebhookQueue $queue;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
        $this->queue = new WebhookQueue($http);
    }

    /**
     * Register a webhook subscription.
     *
     * Pass an HTTPS `$url` to receive POST deliveries. Omit it (or pass
     * `null`) to create a pull-only subscription: events land in the queue
     * readable via `$client->webhooks->queue->pull()`.
     *
     * @param string|null   $url            HTTPS URL that will receive POST webhook payloads,
     *                                      or `null` for a pull-only subscription.
     * @param string[]|null $events         Event types to subscribe to (e.g. ['document.received', 'document.sent']).
     *                                      Pass null to subscribe to all event types.
     * @param string|null   $idempotencyKey Optional `Idempotency-Key` header value
     *                                      for safe retries.
     * @return array Created webhook object with id, url, events, and secret.
     * @throws EPostakError On API error.
     *
     * @example
     *   // Push subscription
     *   $webhook = $client->webhooks->create(
     *       'https://example.com/webhooks/epostak',
     *       ['document.received', 'document.sent']
     *   );
     *   echo 'Webhook secret: ' . $webhook['secret'];
     *
     *   // Pull-only subscription
     *   $webhook = $client->webhooks->create(null, ['document.received']);
     */
    public function create(?string $url = null, ?array $events = null, ?string $idempotencyKey = null): array
    {
        $body = [];
        if ($url !== null) {
            $body['url'] = $url;
        }
        if ($events !== null) {
            $body['events'] = $events;
        }
        $options = ['json' => empty($body) ? new \stdClass() : $body];
        if ($idempotencyKey !== null) {
            $options['headers'] = ['Idempotency-Key' => $idempotencyKey];
        }
        return $this->http->request('POST', '/webhooks', $options);
    }

    /**
     * List all registered webhooks.
     *
     * @return array Array of webhook objects.
     * @throws EPostakError On API error.
     */
    public function list(): array
    {
        return $this->http->request('GET', '/webhooks');
    }

    /**
     * Get webhook detail with recent delivery attempts.
     *
     * @param string $id Webhook UUID.
     * @return array Webhook object with configuration and recent delivery history.
     * @throws EPostakError On API error.
     */
    public function get(string $id): array
    {
        return $this->http->request('GET', '/webhooks/' . urlencode($id));
    }

    /**
     * Update a webhook configuration.
     *
     * @param string $id   Webhook UUID.
     * @param array{url?: string|null, events?: string[], isActive?: bool} $data
     *   Fields to update. Set `url` to `null` to switch to a pull-only subscription.
     * @return array Updated webhook object.
     * @throws EPostakError On API error.
     */
    public function update(string $id, array $data): array
    {
        return $this->http->request('PATCH', '/webhooks/' . urlencode($id), [
            'json' => $data,
        ]);
    }

    /**
     * Delete a webhook subscription.
     *
     * Returns no content on success (HTTP 204).
     *
     * @param string $id Webhook UUID.
     * @return void
     * @throws EPostakError On API error.
     */
    public function delete(string $id): void
    {
        $this->http->request('DELETE', '/webhooks/' . urlencode($id));
    }

    /**
     * Send a test event to a webhook endpoint.
     *
     * The event type is forwarded as a `?event=` query parameter (takes
     * precedence server-side over the body field per PR #114). An array
     * `$params` is accepted so callers can use named-key syntax:
     *
     * ```php
     * $client->webhooks->test($id, ['event' => 'document.delivered']);
     * ```
     *
     * @param string                    $id     Webhook UUID to test.
     * @param array{event?: string}|string|null $params Event type string or params array.
     *                                          Null uses the server default (`document.created`).
     * @return array Test result with success, statusCode, responseTime, webhookId, event, and optional error.
     * @throws EPostakError On API error.
     *
     * @example
     *   $result = $client->webhooks->test('webhook-uuid', ['event' => 'document.delivered']);
     *   echo $result['success'] ? 'OK' : $result['error'];
     */
    public function test(string $id, array|string|null $params = null): array
    {
        $event = null;
        if (is_string($params)) {
            $event = $params;
        } elseif (is_array($params) && isset($params['event'])) {
            $event = (string) $params['event'];
        }

        $options = ['json' => new \stdClass()];  // empty body for POST
        if ($event !== null) {
            $options['query'] = ['event' => $event];
        }
        return $this->http->request('POST', '/webhooks/' . urlencode($id) . '/test', $options);
    }

    /**
     * Get paginated delivery history for a webhook.
     *
     * Each delivery object in the response includes an optional `idempotency_key`
     * field (present when the triggering event was submitted with an idempotency
     * key). The `responseBody` field is omitted by default; pass
     * `includeResponseBody: true` to include it.
     *
     * @param string $id     Webhook UUID.
     * @param array{
     *   limit?: int,
     *   offset?: int,
     *   cursor?: string,
     *   status?: string,
     *   event?: string,
     *   includeResponseBody?: bool,
     *   include?: string
     * } $params Optional query params:
     *   - `limit`               Page size (1–100).
     *   - `offset`              Pagination offset.
     *   - `cursor`              Cursor-based pagination cursor (alternative to offset).
     *   - `status`              Filter by status: SUCCESS | FAILED | PENDING | RETRYING.
     *   - `event`               Filter by event type.
     *   - `includeResponseBody` Include the raw receiver response body in each item.
     *   - `include`             Comma-separated field inclusions (e.g. 'responseBody').
     * @return array{
     *   deliveries: list<array{
     *     id: string,
     *     webhookId: string,
     *     event: string,
     *     status: string,
     *     attempts: int,
     *     responseStatus: int|null,
     *     idempotency_key: string|null,
     *     createdAt: string,
     *     responseBody?: string
     *   }>,
     *   total: int,
     *   limit: int,
     *   offset: int,
     *   nextCursor: string|null
     * } Paginated delivery history.
     * @throws EPostakError On API error.
     *
     * @example
     *   $result = $client->webhooks->deliveries('webhook-uuid', ['status' => 'FAILED', 'limit' => 50]);
     *   foreach ($result['deliveries'] as $d) {
     *       echo $d['event'] . ': ' . $d['status'];
     *       if (isset($d['idempotency_key'])) {
     *           echo ' (idem: ' . $d['idempotency_key'] . ')';
     *       }
     *       echo PHP_EOL;
     *   }
     */
    public function deliveries(string $id, array $params = []): array
    {
        $query = array_filter([
            'limit' => $params['limit'] ?? null,
            'offset' => $params['offset'] ?? null,
            'cursor' => $params['cursor'] ?? null,
            'status' => $params['status'] ?? null,
            'event' => $params['event'] ?? null,
            'includeResponseBody' => isset($params['includeResponseBody'])
                ? ($params['includeResponseBody'] ? 'true' : 'false')
                : null,
            'include' => $params['include'] ?? null,
        ], fn ($v) => $v !== null);
        return $this->http->request('GET', '/webhooks/' . urlencode($id) . '/deliveries', [
            'query' => $query,
        ]);
    }

    /**
     * Rotate a webhook's HMAC-SHA256 signing secret. The new secret is
     * returned ONCE — store it immediately. The previous secret is
     * invalidated on the server side; any in-flight deliveries signed
     * with it will stop verifying. Non-destructive alternative to
     * delete+recreate when a secret leaks.
     *
     * @param string $id Webhook UUID.
     * @return array { id: string, secret: string, message: string } — secret only shown once.
     * @throws EPostakError On API error.
     *
     * @example
     *   $res = $client->webhooks->rotateSecret('webhook-uuid');
     *   file_put_contents('/secure/webhook-secret', $res['secret']);
     */
    public function rotateSecret(string $id): array
    {
        return $this->http->request('POST', '/webhooks/' . urlencode($id) . '/rotate-secret');
    }

}
