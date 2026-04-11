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
}
