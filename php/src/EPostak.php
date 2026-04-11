<?php

declare(strict_types=1);

namespace EPostak;

use EPostak\Resources\Documents;
use EPostak\Resources\Firms;
use EPostak\Resources\Peppol;
use EPostak\Resources\Webhooks;
use EPostak\Resources\Reporting;
use EPostak\Resources\Extract;
use EPostak\Resources\Account;

/**
 * ePošťák Enterprise API client.
 *
 * @property-read Documents $documents Send and receive documents via Peppol
 * @property-read Firms $firms Manage client firms (integrator keys)
 * @property-read Peppol $peppol SMP lookup and Peppol directory search
 * @property-read Webhooks $webhooks Manage webhook subscriptions and pull queue
 * @property-read Reporting $reporting Document statistics and reports
 * @property-read Extract $extract AI-powered OCR extraction from PDFs and images
 * @property-read Account $account Account and firm information
 */
class EPostak
{
    private const DEFAULT_BASE_URL = 'https://epostak.sk/api/enterprise';

    private string $apiKey;
    private string $baseUrl;
    private ?string $firmId;
    private int $maxRetries;

    public Documents $documents;
    public Firms $firms;
    public Peppol $peppol;
    public Webhooks $webhooks;
    public Reporting $reporting;
    public Extract $extract;
    public Account $account;

    /**
     * Create a new ePošťák API client.
     *
     * @param array{apiKey: string, baseUrl?: string, firmId?: string, maxRetries?: int} $config Configuration array.
     *   - `apiKey`     (required) Your Enterprise API key.
     *   - `baseUrl`    (optional) Override the API base URL (default: https://epostak.sk/api/enterprise).
     *   - `firmId`     (optional) Scope all requests to this firm ID.
     *   - `maxRetries` (optional) Maximum retries on 429/5xx responses (default: 3).
     *
     * @throws \InvalidArgumentException If apiKey is missing or not a string.
     *
     * @example
     *   $client = new EPostak(['apiKey' => 'ek_live_...']);
     *   $client->documents->send([...]);
     *
     * @example Scoped to a specific firm:
     *   $client = new EPostak(['apiKey' => 'ek_live_...', 'firmId' => 'firm_abc']);
     */
    public function __construct(array $config)
    {
        if (empty($config['apiKey']) || !is_string($config['apiKey'])) {
            throw new \InvalidArgumentException('EPostak: apiKey is required');
        }

        $this->apiKey = $config['apiKey'];
        $this->baseUrl = $config['baseUrl'] ?? self::DEFAULT_BASE_URL;
        $this->firmId = $config['firmId'] ?? null;
        $this->maxRetries = $config['maxRetries'] ?? 3;

        $http = new HttpClient($this->baseUrl, $this->apiKey, $this->firmId, $this->maxRetries);

        $this->documents = new Documents($http);
        $this->firms = new Firms($http);
        $this->peppol = new Peppol($http);
        $this->webhooks = new Webhooks($http);
        $this->reporting = new Reporting($http);
        $this->extract = new Extract($http);
        $this->account = new Account($http);
    }

    /**
     * Create a new client instance scoped to a specific firm.
     *
     * Useful when an integrator key needs to switch between client firms.
     * Returns a fresh EPostak instance; the original is not modified.
     *
     * @param string $firmId The firm ID to scope requests to.
     * @return self A new client instance scoped to the given firm.
     *
     * @example
     *   $firmClient = $client->withFirm('firm_abc');
     *   $firmClient->documents->send([...]);
     */
    public function withFirm(string $firmId): self
    {
        return new self([
            'apiKey' => $this->apiKey,
            'baseUrl' => $this->baseUrl,
            'firmId' => $firmId,
            'maxRetries' => $this->maxRetries ?? 3,
        ]);
    }
}
