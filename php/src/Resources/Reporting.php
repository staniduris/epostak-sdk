<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Document statistics and usage reports.
 *
 * Access via `$client->reporting`.
 */
class Reporting
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Get aggregated document statistics.
     *
     * @param array{from?: string, to?: string} $params Optional date range:
     *   - `from` ISO 8601 date/datetime for the start of the period.
     *   - `to`   ISO 8601 date/datetime for the end of the period.
     * @return array Statistics object with counts by direction, status, and document type.
     * @throws EPostakError On API error.
     */
    public function statistics(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'from' => $params['from'] ?? null,
            'to' => $params['to'] ?? null,
        ]);
        return $this->http->request('GET', '/reporting/statistics' . $qs);
    }
}
