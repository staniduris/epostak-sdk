<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Sub-resource for managing the per-key IP allowlist (Wave 3.1).
 *
 * An empty list means "no IP restriction" — any caller IP is accepted. When
 * the list is non-empty, requests authenticated with this key are rejected
 * (HTTP 403) unless the source IP matches at least one entry. Each entry is
 * either a bare IPv4/IPv6 address or a CIDR block (`addr/prefix`).
 *
 * Access via `$client->auth->ipAllowlist`.
 */
class IpAllowlist
{
    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
    }

    /**
     * Read the current IP allowlist for the calling API key.
     *
     * @return array{ip_allowlist: string[]} Current allowlist (CIDR/IP entries).
     * @throws EPostakError On API error.
     *
     * @example
     *   $info = $client->auth->ipAllowlist->get();
     *   var_dump($info['ip_allowlist']);
     */
    public function get(): array
    {
        return $this->http->request('GET', '/auth/ip-allowlist');
    }

    /**
     * Replace the IP allowlist for the calling API key.
     *
     * Pass an empty array to clear the restriction. Maximum 50 entries; each
     * must be either a bare IP or a valid CIDR.
     *
     * @param string[] $cidrs Array of IPs or CIDR blocks.
     * @return array{ip_allowlist: string[]} The persisted allowlist.
     * @throws EPostakError On API error.
     *
     * @example
     *   $client->auth->ipAllowlist->update(['192.168.1.0/24', '203.0.113.42']);
     */
    public function update(array $cidrs): array
    {
        return $this->http->request('PUT', '/auth/ip-allowlist', [
            'json' => ['ip_allowlist' => $cidrs],
        ]);
    }
}
