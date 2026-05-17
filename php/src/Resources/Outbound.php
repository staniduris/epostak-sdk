<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Pull API — outbound document operations.
 *
 * Access via `$client->outbound`. Provides cursor-paginated listing, detail
 * retrieval, raw UBL download, and the cross-document event stream.
 *
 * Requires an API-eligible plan (`api-enterprise` or `integrator-managed`).
 * Scope: `documents:read`.
 */
class Outbound
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * List sent outbound documents with cursor-based pagination.
     *
     * @param array{
     *   since?: string,
     *   limit?: int,
     *   kind?: string,
     *   status?: string,
     *   business_status?: string,
     *   recipient?: string,
     *   next_cursor?: string
     * } $params Optional filters:
     *   - `since`           ISO 8601 datetime cutoff.
     *   - `limit`           Max items to return (1–500, default 100).
     *   - `kind`            Document type filter (e.g. 'invoice', 'credit_note').
     *   - `status`          Transport status filter (e.g. 'SENT', 'FAILED', 'PENDING').
     *   - `business_status` Business status filter (e.g. 'ACCEPTED', 'REJECTED').
     *   - `recipient`       Filter by recipient Peppol ID.
     *   - `next_cursor`     Cursor from the previous response.
     * @return array{documents: array, next_cursor: ?string, has_more: bool}
     * @throws EPostakError On API error.
     *
     * @example
     *   $page = $client->outbound->list(['limit' => 100, 'status' => 'SENT']);
     *   foreach ($page['documents'] as $doc) {
     *       echo $doc['id'] . ': ' . $doc['status'] . PHP_EOL;
     *   }
     */
    public function list(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'since' => $params['since'] ?? null,
            'limit' => $params['limit'] ?? null,
            'kind' => $params['kind'] ?? null,
            'status' => $params['status'] ?? null,
            'business_status' => $params['business_status'] ?? null,
            'recipient' => $params['recipient'] ?? null,
            'next_cursor' => $params['next_cursor'] ?? null,
        ]);
        return $this->http->request('GET', '/outbound/documents' . $qs);
    }

    /**
     * Get a single outbound document by ID.
     *
     * The detail view includes `attempt_history` (delivery attempt log) which is
     * NOT present in the list response.
     *
     * @param string $id Document UUID.
     * @return array Full outbound document object including attempt_history.
     * @throws EPostakError On API error (404 if not found or cross-tenant).
     */
    public function get(string $id): array
    {
        return $this->http->request('GET', '/outbound/documents/' . urlencode($id));
    }

    /**
     * Download the raw UBL XML for an outbound document.
     *
     * Returns `application/xml`. Probes Invoice.ublXmlPath and
     * PeppolDocument.rawXmlPath, returning the first available.
     *
     * @param string $id Document UUID.
     * @return string UBL 2.1 XML string.
     * @throws EPostakError On API error (404 if XML not available).
     *
     * @example
     *   $xml = $client->outbound->getUbl('doc-uuid');
     *   file_put_contents('/archive/outbound-doc.xml', $xml);
     */
    public function getUbl(string $id): string
    {
        return $this->http->requestRaw('GET', '/outbound/documents/' . urlencode($id) . '/ubl');
    }

    public function getMdn(string $id): string
    {
        return $this->http->requestRaw('GET', '/outbound/documents/' . urlencode($id) . '/mdn');
    }

    /**
     * Stream the outbound document event log with cursor-based pagination.
     *
     * Returns lifecycle events (state transitions, delivery attempts, invoice
     * responses, etc.) across all outbound documents. Optionally filter to a
     * single document with the `document_id` parameter.
     *
     * Note: coverage is invoice-backed documents only.
     *
     * @param array{
     *   document_id?: string,
     *   next_cursor?: string,
     *   limit?: int
     * } $params Optional filters:
     *   - `document_id` Scope to events for a single document UUID.
     *   - `next_cursor` Cursor from the previous response.
     *   - `limit`       Max events to return per page.
     * @return array{events: array, next_cursor: ?string, has_more: bool}
     * @throws EPostakError On API error.
     *
     * @example
     *   $result = $client->outbound->events(['document_id' => 'doc-uuid']);
     *   foreach ($result['events'] as $event) {
     *       echo $event['type'] . ' at ' . $event['occurred_at'] . PHP_EOL;
     *   }
     */
    public function events(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'document_id' => $params['document_id'] ?? null,
            'next_cursor' => $params['next_cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/outbound/events' . $qs);
    }
}
