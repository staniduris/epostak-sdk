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

    public function companySearch(string $q, ?int $limit = null): array
    {
        $qs = HttpClient::buildQuery(['q' => $q, 'limit' => $limit]);
        return $this->http->request('GET', '/company/search' . $qs);
    }

    /**
     * Check a participant's advertised Peppol capabilities.
     *
     * Verifies that a participant exists on SMP and (optionally) that it
     * accepts a specific document type. Prefer this over {@see lookup()}
     * when you only need a yes/no answer for a given doc type.
     *
     * @param string      $scheme       Peppol scheme (e.g. '0245').
     * @param string      $identifier   Identifier value.
     * @param string|null $documentType Optional UBL document type ID to check for acceptance.
     * @return array{
     *   found: bool,
     *   accepts: bool,
     *   supportedDocumentTypes: list<string>,
     *   matchedDocumentType?: ?string
     * } Capabilities result.
     * @throws EPostakError On API error.
     *
     * @example
     *   $caps = $client->peppol->capabilities(
     *       '0245',
     *       '12345678',
     *       'urn:peppol:pint:billing-1@aunz-1'
     *   );
     *   if ($caps['found'] && $caps['accepts']) {
     *       echo "Receiver supports that document type";
     *   }
     */
    public function capabilities(string $scheme, string $identifier, ?string $documentType = null): array
    {
        $body = ['scheme' => $scheme, 'identifier' => $identifier];
        if ($documentType !== null) {
            $body['documentType'] = $documentType;
        }
        return $this->http->request('POST', '/peppol/capabilities', [
            'json' => $body,
        ]);
    }

    /**
     * Look up many Peppol participants in a single request (max 100).
     *
     * Each result matches the order of the input list and indicates whether
     * the participant was found on SMP.
     *
     * @param list<array{scheme: string, identifier: string}> $participants Participant references.
     * @return array{
     *   total: int,
     *   found: int,
     *   notFound: int,
     *   results: list<array{
     *     scheme: string,
     *     identifier: string,
     *     found: bool,
     *     participant?: ?array,
     *     error?: ?string
     *   }>
     * } Batch lookup result.
     * @throws EPostakError On API error.
     *
     * @example
     *   $batch = $client->peppol->lookupBatch([
     *       ['scheme' => '0245', 'identifier' => '12345678'],
     *       ['scheme' => '0245', 'identifier' => '87654321'],
     *   ]);
     *   foreach ($batch['results'] as $r) {
     *       echo $r['identifier'], ' -> ', $r['found'] ? 'found' : 'not found', "\n";
     *   }
     */
    public function lookupBatch(array $participants): array
    {
        return $this->http->request('POST', '/peppol/participants/batch', [
            'json' => ['participants' => $participants],
        ]);
    }
}
