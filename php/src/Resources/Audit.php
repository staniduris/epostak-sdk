<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Per-firm security/auth audit feed (Wave 3.4).
 *
 * Tenant-isolated: every row is filtered by the firm the calling key is
 * bound to. Integrators with multiple managed firms see only the firm
 * specified by `X-Firm-Id` (set automatically when you pass `firmId` to
 * the EPostak constructor or use `withFirm()`).
 *
 * Cursor pagination over `(occurred_at DESC, id DESC)` — pass the
 * `next_cursor` from one page back into the next call to walk the feed
 * deterministically, even across rows with identical timestamps.
 *
 * Access via `$client->audit`.
 *
 * @example
 *   $cursor = null;
 *   do {
 *       $page = $client->audit->list([
 *           'event' => 'jwt.issued',
 *           'since' => '2026-04-01T00:00:00Z',
 *           'cursor' => $cursor,
 *           'limit' => 50,
 *       ]);
 *       foreach ($page['items'] as $event) {
 *           echo $event['occurred_at'], ' ', $event['event'], "\n";
 *       }
 *       $cursor = $page['next_cursor'] ?? null;
 *   } while ($cursor !== null);
 */
class Audit
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * List audit events for the current firm. Cursor-paginated.
     *
     * @param array{
     *   event?: string,
     *   actorType?: string,
     *   since?: string,
     *   until?: string,
     *   cursor?: string,
     *   limit?: int
     * } $params Optional filters:
     *   - `event`     Exact-match event name (e.g. `jwt.issued`).
     *   - `actorType` One of `user`, `apiKey`, `integratorKey`, `system`.
     *   - `since`     ISO 8601 timestamp lower bound (inclusive).
     *   - `until`     ISO 8601 timestamp upper bound (exclusive).
     *   - `cursor`    Opaque cursor from a previous page.
     *   - `limit`     1–100, default 20.
     * @return array{items: array, next_cursor: ?string} Cursor page.
     * @throws EPostakError On API error.
     */
    public function list(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'event' => $params['event'] ?? null,
            'actor_type' => $params['actorType'] ?? null,
            'since' => $params['since'] ?? null,
            'until' => $params['until'] ?? null,
            'cursor' => $params['cursor'] ?? null,
            'limit' => $params['limit'] ?? null,
        ]);
        return $this->http->request('GET', '/audit' . $qs);
    }
}
