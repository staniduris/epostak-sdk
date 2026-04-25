<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Manage client firms and their Peppol identifiers.
 *
 * Integrator API keys can list, assign, and manage multiple firms.
 * Access via `$client->firms`.
 */
class Firms
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * List all accessible firms.
     *
     * @return array Array of firm objects with id, name, ICO, and Peppol IDs.
     * @throws EPostakError On API error.
     */
    public function list(): array
    {
        return $this->http->request('GET', '/firms');
    }

    /**
     * Get firm detail by ID.
     *
     * @param string $id Firm UUID.
     * @return array Firm object with full details including Peppol identifiers.
     * @throws EPostakError On API error.
     */
    public function get(string $id): array
    {
        return $this->http->request('GET', '/firms/' . urlencode($id));
    }

    /**
     * List documents for a firm.
     *
     * @param string $id     Firm UUID.
     * @param array{offset?: int, limit?: int, direction?: string} $params Optional filters:
     *   - `offset`    Pagination offset.
     *   - `limit`     Max items to return.
     *   - `direction` Filter by 'sent' or 'received'.
     * @return array Paginated document list.
     * @throws EPostakError On API error.
     */
    public function documents(string $id, array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'offset' => $params['offset'] ?? null,
            'limit' => $params['limit'] ?? null,
            'direction' => $params['direction'] ?? null,
        ]);
        return $this->http->request('GET', '/firms/' . urlencode($id) . '/documents' . $qs);
    }

    /**
     * Register a Peppol participant ID for a firm.
     *
     * @param string $id         Firm UUID.
     * @param string $scheme     Peppol identifier scheme (e.g. '0192' for Slovak ICO).
     * @param string $identifier Identifier value within the scheme (e.g. the ICO number).
     * @return array Registered Peppol identifier object.
     * @throws EPostakError On API error.
     */
    public function registerPeppolId(string $id, string $scheme, string $identifier): array
    {
        return $this->http->request('POST', '/firms/' . urlencode($id) . '/peppol-identifiers', [
            'json' => [
                'scheme' => $scheme,
                'identifier' => $identifier,
            ],
        ]);
    }

    /**
     * Link this integrator to a Firm that has already completed FS SR signup
     * and granted consent.
     *
     * **Lookup-only** — this endpoint cannot create new Firms. The target
     * Firm must have completed FS SR PFS signup and granted consent to this
     * integrator before the link succeeds.
     *
     * On error, inspect `$err->code`:
     *   - `FIRM_NOT_REGISTERED` (HTTP 404) — no Firm with that ICO exists yet.
     *     Direct the firm to complete FS SR PFS signup before retrying.
     *   - `CONSENT_REQUIRED` (HTTP 403) — Firm exists but has not granted
     *     consent for this integrator to act on its behalf.
     *   - `ALREADY_LINKED` (HTTP 409) — the integrator already has an active
     *     link to this Firm.
     *
     * @param string $ico Slovak company identification number (ICO).
     * @return array Linked firm object.
     * @throws EPostakError On API error (see the codes listed above).
     */
    public function assign(string $ico): array
    {
        return $this->http->request('POST', '/firms/assign', [
            'json' => ['ico' => $ico],
        ]);
    }

    /**
     * Batch assign firms to this integrator.
     *
     * @param string[] $icos Array of Slovak ICO numbers (up to 50).
     * @return array Batch result with assigned firms and any errors.
     * @throws EPostakError On API error.
     */
    public function assignBatch(array $icos): array
    {
        return $this->http->request('POST', '/firms/assign/batch', [
            'json' => ['icos' => $icos],
        ]);
    }
}
