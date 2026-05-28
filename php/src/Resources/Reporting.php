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
     * The returned envelope is:
     * ```
     * [
     *   'period'          => 'month' | 'quarter' | 'year' | 'custom',
     *   'sent'            => ['total' => int, 'by_type' => array],
     *   'received'        => ['total' => int, 'by_type' => array],
     *   'delivery_rate'   => float,
     *   'top_recipients'  => array<array{name?: string, peppolId?: string, count: int}>,
     *   'top_senders'     => array<array{name?: string, peppolId?: string, count: int}>,
     * ]
     * ```
     *
     * @param array{from?: string, to?: string, period?: string} $params Optional filters:
     *   - `from`   ISO 8601 date/datetime for the start of the period (custom range).
     *   - `to`     ISO 8601 date/datetime for the end of the period (custom range).
     *   - `period` One of `month`, `quarter`, `year` (mutually exclusive with `from`/`to`).
     * @return array Statistics envelope (see above).
     * @throws EPostakError On API error.
     *
     * @example
     *   $stats = $client->reporting->statistics(['period' => 'month']);
     *   echo $stats['sent']['total'], ' / ', $stats['delivery_rate'];
     */
    public function statistics(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'from' => $params['from'] ?? null,
            'to' => $params['to'] ?? null,
            'period' => $params['period'] ?? null,
        ]);
        return $this->http->request('GET', '/reporting/statistics' . $qs);
    }

    /**
     * List EUSR/TSR reports submitted to FS SR by ePošťák as the AP operator.
     *
     * @param array{limit?: int, offset?: int, report_type?: string} $params Optional filters.
     * @return array{items: array, total: int, limit: int, offset: int}
     * @throws EPostakError On API error.
     */
    public function submissions(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'offset' => $params['offset'] ?? null,
            'report_type' => $params['report_type'] ?? null,
        ]);
        return $this->http->request('GET', '/reporting/submissions' . $qs);
    }
}
