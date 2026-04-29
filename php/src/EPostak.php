<?php

declare(strict_types=1);

namespace EPostak;

use EPostak\Resources\Auth;
use EPostak\Resources\Audit;
use EPostak\Resources\Documents;
use EPostak\Resources\Firms;
use EPostak\Resources\OAuth;
use EPostak\Resources\Peppol;
use EPostak\Resources\Webhooks;
use EPostak\Resources\Reporting;
use EPostak\Resources\Extract;
use EPostak\Resources\Account;
use GuzzleHttp\Client as GuzzleClient;
use GuzzleHttp\Exception\GuzzleException;

/**
 * ePošťák API client.
 *
 * @property-read Auth $auth OAuth token mint/renew/revoke + key introspection, rotation, IP allowlist
 * @property-read Audit $audit Per-firm audit feed (cursor-paginated)
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
    private const DEFAULT_BASE_URL = 'https://epostak.sk/api/v1';
    private const DEFAULT_PUBLIC_BASE_URL = 'https://epostak.sk/api';

    private string $apiKey;
    private string $baseUrl;
    private ?string $firmId;
    private int $maxRetries;

    public Auth $auth;
    public Audit $audit;
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
     *   - `apiKey`     (required) Your API key (`sk_live_*` or `sk_int_*`).
     *   - `baseUrl`    (optional) Override the API base URL (default: https://epostak.sk/api/v1).
     *   - `firmId`     (optional) Scope all requests to this firm ID (required for `sk_int_*`).
     *   - `maxRetries` (optional) Maximum retries on 429/5xx responses (default: 3).
     *
     * @throws \InvalidArgumentException If apiKey is missing or not a string.
     *
     * @example
     *   $client = new EPostak(['apiKey' => 'sk_live_...']);
     *   $client->documents->send([...]);
     *
     * @example Scoped to a specific firm:
     *   $client = new EPostak(['apiKey' => 'sk_int_...', 'firmId' => 'firm_abc']);
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

        $this->auth = new Auth($http);
        $this->audit = new Audit($http);
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
            'maxRetries' => $this->maxRetries,
        ]);
    }

    /**
     * Validate a UBL XML document against the Peppol BIS 3.0 3-layer rules.
     *
     * This endpoint is PUBLIC -- no API key is required. Rate-limited to
     * 20 requests per minute per IP. Can be called statically without
     * constructing an EPostak client, which makes it suitable for CLI
     * tools or test harnesses.
     *
     * @param string      $xml     UBL 2.1 XML invoice or credit note.
     * @param string|null $baseUrl Optional override for the public API base URL
     *                             (defaults to https://epostak.sk/api).
     * @return array Full 3-layer Peppol BIS 3.0 validation report.
     * @throws EPostakError On non-2xx responses or network errors.
     *
     * @example
     *   $report = \EPostak\EPostak::validate(file_get_contents('invoice.xml'));
     *   var_dump($report['valid']);
     */
    public static function validate(string $xml, ?string $baseUrl = null): array
    {
        $base = rtrim($baseUrl ?? self::DEFAULT_PUBLIC_BASE_URL, '/');
        $client = new GuzzleClient([
            'base_uri' => $base . '/',
            'http_errors' => false,
        ]);

        try {
            $response = $client->request('POST', 'validate', [
                'headers' => [
                    'Content-Type' => 'application/xml',
                    'Accept' => 'application/json',
                ],
                'body' => $xml,
            ]);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => $e->getMessage()]);
        }

        $statusCode = $response->getStatusCode();
        $body = $response->getBody()->getContents();

        if ($statusCode >= 400) {
            $decoded = [];
            try {
                $decoded = json_decode($body, true) ?? [];
            } catch (\Throwable) {
                $decoded = ['error' => $response->getReasonPhrase()];
            }
            throw new EPostakError($statusCode, $decoded, $response->getHeaders());
        }

        return $body === '' ? [] : (json_decode($body, true) ?? []);
    }

}
