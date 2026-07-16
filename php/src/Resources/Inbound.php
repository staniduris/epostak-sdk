<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Pull API — inbound document operations.
 *
 * Access via `$client->inbound`. Provides cursor-paginated listing, detail
 * retrieval, raw UBL download, and acknowledgement of received Peppol documents.
 *
 * Requires an API-eligible plan (`api-enterprise` or `integrator-managed`).
 * Scope: `documents:read` (list/get/ubl), `documents:write` (ack).
 */
class Inbound
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * List received inbound documents with cursor-based pagination.
     *
     * @param array{
     *   since?: string,
     *   limit?: int,
     *   kind?: string,
     *   sender?: string,
     *   next_cursor?: string
     * } $params Optional filters:
     *   - `since`       Opaque cursor from the previous response's `next_cursor`.
     *   - `limit`       Max items to return (1–500, default 100).
     *   - `kind`        Document type filter (e.g. 'invoice', 'credit_note').
     *   - `sender`      Filter by sender Peppol ID.
     *   - `next_cursor` Deprecated input alias for `since`; retained for
     *                   backwards compatibility.
     * @return array{documents: array, next_cursor: ?string, has_more: bool}
     * @throws EPostakError On API error.
     *
     * @example
     *   $page = $client->inbound->list(['limit' => 50]);
     *   foreach ($page['documents'] as $doc) {
     *       echo $doc['id'] . ': ' . ($doc['kind'] ?? 'invoice') . PHP_EOL;
     *   }
     *   if ($page['has_more']) {
     *       $next = $client->inbound->list(['since' => $page['next_cursor']]);
     *   }
     */
    public function list(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            // The API returns the cursor as `next_cursor`, but accepts it on
            // the next request only through the `since` query parameter.
            'since' => $params['since'] ?? $params['next_cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
            'kind' => $params['kind'] ?? null,
            'sender' => $params['sender'] ?? null,
        ]);
        return $this->http->request('GET', '/inbound/documents' . $qs);
    }

    /**
     * Get a single inbound document by ID.
     *
     * Returns 404 if the document does not belong to the authenticated firm.
     *
     * @param string $id Document UUID.
     * @return array Full inbound document object (same shape as a list item plus raw metadata).
     * @throws EPostakError On API error (404 if not found or cross-tenant).
     */
    public function get(string $id): array
    {
        return $this->http->request('GET', '/inbound/documents/' . urlencode($id));
    }

    /**
     * Download the raw UBL XML for an inbound document.
     *
     * Returns `application/xml`. Returns 404 if no raw XML is stored (legacy rows
     * received before XML archiving was enabled).
     *
     * @param string $id Document UUID.
     * @return string UBL 2.1 XML string.
     * @throws EPostakError On API error (404 if XML not available for this document).
     *
     * @example
     *   $xml = $client->inbound->getUbl('doc-uuid');
     *   file_put_contents('/archive/inbound-doc.xml', $xml);
     */
    public function getUbl(string $id): string
    {
        return $this->http->requestRaw('GET', '/inbound/documents/' . urlencode($id) . '/ubl');
    }

    /**
     * Acknowledge (mark as processed) an inbound document.
     *
     * Idempotent — re-acknowledging an already-acknowledged document overwrites
     * the `clientAckedAt` timestamp with the latest call.
     *
     * @param string $id     Document UUID.
     * @param array{client_reference?: string} $params Optional fields:
     *   - `client_reference` Your internal reference (max 256 characters). Stored
     *                        server-side and echoed back in the document detail.
     * @return array Updated inbound document object post-acknowledgement.
     * @throws EPostakError On API error.
     *
     * @example
     *   $doc = $client->inbound->ack('doc-uuid', ['client_reference' => 'ERP-ORDER-4521']);
     *   echo $doc['clientAckedAt'];
     */
    public function ack(string $id, array $params = []): array
    {
        $body = [];
        if (isset($params['client_reference'])) {
            $body['client_reference'] = (string) $params['client_reference'];
        }
        return $this->http->request('POST', '/inbound/documents/' . urlencode($id) . '/ack', [
            'json' => $body,
        ]);
    }
}
