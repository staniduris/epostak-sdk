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
}
