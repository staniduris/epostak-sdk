<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Account and firm information for the authenticated API key.
 *
 * Access via `$client->account`.
 */
class Account
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Get account and firm information for the authenticated API key.
     *
     * @return array Account object with plan, firm details, and usage.
     * @throws EPostakError On API error.
     */
    public function get(): array
    {
        return $this->http->request('GET', '/account');
    }

    /**
     * Inspect the authenticated API key, firm, plan, and rate limits.
     *
     * Useful for debugging credentials, verifying which firm an integrator
     * key is currently scoped to, and discovering per-minute rate limits.
     *
     * @return array{
     *   key: array{keyId: string, prefix: string, createdAt: string, lastUsedAt: ?string},
     *   firm: array{id: string, name: string, ico: ?string},
     *   plan: string,
     *   rateLimit: array{getPerMin: int, postPerMin: int},
     *   integrator?: ?array{id: string, name: string}
     * } Status object with key metadata, firm, plan, rate limits, and optional integrator.
     * @throws EPostakError On API error.
     *
     * @example
     *   $info = $client->account->status();
     *   echo $info['firm']['name'], " on plan ", $info['plan'];
     */
    public function status(): array
    {
        return $this->http->request('POST', '/auth/status');
    }

    /**
     * Rotate the plaintext secret for the current API key.
     *
     * The returned `key` is shown exactly once -- store it immediately.
     * The previous secret is invalidated on success.
     *
     * Integrator keys (`sk_int_*`) cannot be rotated through this endpoint;
     * the server returns HTTP 409, which throws EPostakError.
     *
     * @return array{keyId: string, key: string, prefix: string, rotatedAt: string} New key material.
     * @throws EPostakError On API error (409 for integrator keys).
     *
     * @example
     *   $new = $client->account->rotateSecret();
     *   save_somewhere_secure($new['key']); // shown only once
     */
    public function rotateSecret(): array
    {
        return $this->http->request('POST', '/auth/rotate-secret', [
            'json' => new \stdClass(),
        ]);
    }
}
