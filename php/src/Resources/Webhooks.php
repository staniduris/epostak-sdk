<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

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
     * Register a webhook endpoint.
     *
     * @param string        $url    HTTPS URL that will receive POST webhook payloads.
     * @param string[]|null $events Event types to subscribe to (e.g. ['document.received', 'document.sent']).
     *                              Pass null to subscribe to all event types.
     * @return array Created webhook object with id, url, events, and secret.
     * @throws EPostakError On API error.
     *
     * @example
     *   $webhook = $client->webhooks->create(
     *       'https://example.com/webhooks/epostak',
     *       ['document.received', 'document.status_changed']
     *   );
     *   echo 'Webhook secret: ' . $webhook['secret'];
     */
    public function create(string $url, ?array $events = null): array
    {
        $body = ['url' => $url];
        if ($events !== null) {
            $body['events'] = $events;
        }
        return $this->http->request('POST', '/webhooks', [
            'json' => $body,
        ]);
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
     * @param array  $data Fields to update (url, events, active).
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
     * @param string $id Webhook UUID.
     * @return array Deletion confirmation.
     * @throws EPostakError On API error.
     */
    public function delete(string $id): array
    {
        return $this->http->request('DELETE', '/webhooks/' . urlencode($id));
    }

    /**
     * Send a test event to a webhook endpoint.
     *
     * @param string      $id    Webhook UUID to test.
     * @param string|null $event Event type to simulate (e.g. 'document.created'). Null uses server default.
     * @return array Test result with success, statusCode, responseTime, webhookId, event, and optional error.
     * @throws EPostakError On API error.
     *
     * @example
     *   $result = $client->webhooks->test('webhook-uuid', 'document.received');
     *   echo $result['success'] ? 'OK' : $result['error'];
     */
    public function test(string $id, ?string $event = null): array
    {
        $body = [];
        if ($event !== null) {
            $body['event'] = $event;
        }
        return $this->http->request('POST', '/webhooks/' . urlencode($id) . '/test', [
            'json' => $body,
        ]);
    }

    /**
     * Get paginated delivery history for a webhook.
     *
     * @param string $id     Webhook UUID.
     * @param array  $params Optional query params: limit (1-100), offset, status (SUCCESS/FAILED/PENDING/RETRYING), event.
     * @return array Paginated response with deliveries, total, limit, offset.
     * @throws EPostakError On API error.
     *
     * @example
     *   $result = $client->webhooks->deliveries('webhook-uuid', ['status' => 'FAILED', 'limit' => 50]);
     *   foreach ($result['deliveries'] as $d) {
     *       echo $d['event'] . ': ' . $d['status'] . PHP_EOL;
     *   }
     */
    public function deliveries(string $id, array $params = []): array
    {
        return $this->http->request('GET', '/webhooks/' . urlencode($id) . '/deliveries', [
            'query' => $params,
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
