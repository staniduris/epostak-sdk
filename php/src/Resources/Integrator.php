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
    public IntegratorKeys $keys;
    public IntegratorLicenses $licenses;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(HttpClient $http)
    {
        $this->keys = new IntegratorKeys($http);
        $this->licenses = new IntegratorLicenses($http);
    }
}

/**
 * `/integrator/keys` — integrator API key management.
 */
class IntegratorKeys
{
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * List all API keys for the current integrator.
     *
     * @return array{keys: array}
     * @throws EPostakError On API error.
     */
    public function list(): array
    {
        return $this->http->request('GET', '/integrator/keys');
    }

    /**
     * Deactivate an integrator API key by UUID (`keyId`) or `sk_int_*` prefix (`client_id`).
     *
     * @param array{keyId?: string, client_id?: string} $params
     * @return array{success: bool, message: string}
     * @throws EPostakError On API error.
     */
    public function deactivate(array $params): array
    {
        return $this->http->request('DELETE', '/integrator/keys', [
            'json' => $params,
        ]);
    }
}

/**
 * `/integrator/licenses/*` — billing aggregate views.
 *
 * Progressive rates are applied separately to aggregate outbound and inbound
 * usage. Managed sandbox firms are reported separately and excluded from the
 * billed aggregate.
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
     *               `billable`, `sandbox`, `productionEstimate`, `nonManaged`,
     *               `exceedsAutoTier`, `contactThreshold`,
     *               `pricing.{scheduleVersion,thresholdScope,
     *               marginalBandStartsAt,outboundTiers,inboundApiTiers}`, paginated
     *               `firms` rows, and `pagination`.
     * @throws EPostakError On API error.
     *
     * @example
     *   $usage = $client->integrator->licenses->info(['limit' => 100]);
     *   echo $usage['pricing']['scheduleVersion'];
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
