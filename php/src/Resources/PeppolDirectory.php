<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Search the Peppol Business Card directory.
 *
 * Access via `$client->peppol->directory`.
 */
class PeppolDirectory
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Search the Peppol Business Card directory.
     *
     * Requires `documents:read` scope.
     *
     * @param array{q?: string, country?: string, page?: int, page_size?: int} $params Search filters:
     *   - `q`         Free-text search query (company name, ID, etc.).
     *   - `country`   ISO 3166-1 alpha-2 country code (e.g. 'SK', 'CZ').
     *   - `page`      Page number (1-based).
     *   - `page_size` Results per page (default 20).
     * @return array Paginated search results with matching business card entries.
     * @throws EPostakError On API error.
     */
    public function search(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'q' => $params['q'] ?? null,
            'country' => $params['country'] ?? null,
            'page' => $params['page'] ?? null,
            'page_size' => $params['page_size'] ?? null,
        ]);
        return $this->http->request('GET', '/peppol/directory/search' . $qs);
    }
}
