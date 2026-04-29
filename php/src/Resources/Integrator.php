<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Integrator-aggregate endpoints (`sk_int_*` keys only).
 *
 * Access via `$client->integrator->licenses->info(...)`.
 *
 * For per-firm `/account` and `/licenses/info` views (which integrators also
 * reach via `X-Firm-Id`), use `$client->account` instead. This namespace
 * exposes the integrator-level views that don't take a firm context.
 */
class Integrator
{
    public IntegratorLicenses $licenses;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(HttpClient $http)
    {
        $this->licenses = new IntegratorLicenses($http);
    }
}

/**
 * `/integrator/licenses/*` — billing aggregate views.
 *
 * Tier rates are applied to the AGGREGATE document count across all the
 * integrator's `integrator-managed` firms — a 100-firm × 50-doc integrator
 * lands in tier 2–3, not tier 1 like a standalone firm would. Volumes above
 * `contactThreshold` (5 000 / month) flip `exceedsAutoTier` to `true`;
 * auto-billing pauses there and sales handles invoicing manually.
 */
class IntegratorLicenses
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Aggregate plan + current-period usage across managed firms.
     *
     * Wraps `GET /api/v1/integrator/licenses/info`. Requires the
     * `account:read` scope on a `sk_int_*` integrator key. No `X-Firm-Id`
     * header — the endpoint is integrator-scoped.
     *
     * @param array{offset?: int, limit?: int} $params Optional pagination:
     *   - `offset` Pagination offset for the per-firm list (default 0).
     *   - `limit`  Page size for the per-firm list, max 100 (default 50).
     * @return array Response with `integrator`, `period`, `nextResetAt`,
     *               `billable` (managed-plan aggregate + tier-applied charges),
     *               `nonManaged`, `exceedsAutoTier`, `contactThreshold`,
     *               `pricing.{outboundTiers,inboundApiTiers}`, paginated
     *               `firms` rows, and `pagination`.
     * @throws EPostakError On API error.
     *
     * @example
     *   $usage = $client->integrator->licenses->info(['limit' => 100]);
     *   if ($usage['exceedsAutoTier']) {
     *       // sales review required, auto-billing has paused
     *   }
     */
    public function info(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'offset' => $params['offset'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/integrator/licenses/info' . $qs);
    }
}
