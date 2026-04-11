<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Peppol network lookups and Slovak company search.
 *
 * Access via `$client->peppol`. Directory search is available through
 * the nested `$client->peppol->directory` resource.
 */
class Peppol
{
    /** @var PeppolDirectory Peppol Business Card directory search. */
    public PeppolDirectory $directory;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
        $this->directory = new PeppolDirectory($http);
    }

    /**
     * SMP participant lookup.
     *
     * Queries the Peppol SMP to check if a participant is registered
     * and which document types it supports.
     *
     * @param string $scheme     Peppol identifier scheme (e.g. '0192').
     * @param string $identifier Identifier value (e.g. ICO number).
     * @return array Participant info with supported document types and endpoints.
     * @throws EPostakError On API error.
     */
    public function lookup(string $scheme, string $identifier): array
    {
        return $this->http->request('GET', '/peppol/participants/' . urlencode($scheme) . '/' . urlencode($identifier));
    }

    /**
     * Slovak company lookup by ICO.
     *
     * Returns company name, address, and tax identifiers from Slovak registries.
     *
     * @param string $ico Slovak company identification number (ICO).
     * @return array Company details including name, address, DIC, and IC DPH.
     * @throws EPostakError On API error.
     */
    public function companyLookup(string $ico): array
    {
        return $this->http->request('GET', '/company/lookup/' . urlencode($ico));
    }
}
