<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Received (inbox) document operations.
 *
 * Access via `$client->documents->inbox`. Provides listing,
 * detail retrieval, and acknowledgement of inbound Peppol documents.
 */
class Inbox
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * List received documents.
     *
     * @param array{offset?: int, limit?: int, status?: string, since?: string} $params Optional filters:
     *   - `offset` Pagination offset (default 0).
     *   - `limit`  Max items to return (default 20, max 100).
     *   - `status` Filter by status ('new', 'acknowledged').
     *   - `since`  ISO 8601 datetime to fetch documents received after.
     * @return array Paginated list with `items` array and `total` count.
     * @throws EPostakError On API error.
     *
     * @example
     *   // Fetch the 10 newest unprocessed documents
     *   $result = $client->documents->inbox->list(['limit' => 10, 'status' => 'new']);
     *   foreach ($result['items'] as $doc) {
     *       echo $doc['id'] . ': ' . $doc['invoiceNumber'] . "\n";
     *   }
     */
    public function list(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'offset' => $params['offset'] ?? null,
            'limit' => $params['limit'] ?? null,
            'status' => $params['status'] ?? null,
            'since' => $params['since'] ?? null,
        ]);
        return $this->http->request('GET', '/documents/inbox' . $qs);
    }

    /**
     * Get full inbox document detail including parsed data and UBL XML.
     *
     * @param string $id Document UUID.
     * @return array Document object with metadata, line items, and raw UBL XML.
     * @throws EPostakError On API error.
     */
    public function get(string $id): array
    {
        return $this->http->request('GET', '/documents/inbox/' . urlencode($id));
    }

    /**
     * Acknowledge (mark as processed) a received document.
     *
     * Once acknowledged, the document no longer appears in unprocessed listings.
     *
     * @param string $id Document UUID.
     * @return array Acknowledgement confirmation.
     * @throws EPostakError On API error.
     */
    public function acknowledge(string $id): array
    {
        return $this->http->request('POST', '/documents/inbox/' . urlencode($id) . '/acknowledge');
    }

    /**
     * List received documents across all firms (integrator only).
     *
     * Requires an integrator API key. Returns documents from all assigned firms.
     *
     * @param array{offset?: int, limit?: int, status?: string, since?: string, firm_id?: string} $params Optional filters:
     *   - `offset`  Pagination offset.
     *   - `limit`   Max items to return.
     *   - `status`  Filter by status.
     *   - `since`   ISO 8601 datetime cutoff.
     *   - `firm_id` Filter to a specific firm.
     * @return array Paginated list with `items` array and `total` count.
     * @throws EPostakError On API error.
     */
    public function listAll(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'offset' => $params['offset'] ?? null,
            'limit' => $params['limit'] ?? null,
            'status' => $params['status'] ?? null,
            'since' => $params['since'] ?? null,
            'firm_id' => $params['firm_id'] ?? null,
        ]);
        return $this->http->request('GET', '/documents/inbox/all' . $qs);
    }
}
