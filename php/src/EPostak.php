<?php

declare(strict_types=1);

namespace EPostak;

use EPostak\RateLimit;
use EPostak\Resources\Auth;
use EPostak\Resources\Audit;
use EPostak\Resources\Documents;
use EPostak\Resources\Firms;
use EPostak\Resources\Inbound;
use EPostak\Resources\Integrator;
use EPostak\Resources\OAuth;
use EPostak\Resources\Outbound;
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
 * @property-read Inbound $inbound Pull API — receive and acknowledge inbound Peppol documents
 * @property-read Outbound $outbound Pull API — list sent documents and stream delivery events
 * @property-read Peppol $peppol SMP lookup and Peppol directory search
 * @property-read Webhooks $webhooks Manage webhook subscriptions and pull queue
 * @property-read Reporting $reporting Document statistics and reports
 * @property-read Extract $extract AI-powered OCR extraction from PDFs and images
 * @property-read Account $account Account and firm information
 * @property-read Integrator $integrator Integrator-aggregate endpoints (sk_int_* keys)
 */
class EPostak
{
    private const DEFAULT_BASE_URL = 'https://epostak.sk/api/v1';
    private const DEFAULT_PUBLIC_BASE_URL = 'https://epostak.sk/api';

    private string $clientId;
    private string $clientSecret;
    private string $baseUrl;
    private ?string $firmId;
    private int $maxRetries;

    public Auth $auth;
    public Audit $audit;
    public Documents $documents;
    public Firms $firms;
    public Inbound $inbound;
    public Outbound $outbound;
    public Peppol $peppol;
    public Webhooks $webhooks;
    public Reporting $reporting;
    public Extract $extract;
    public Account $account;
    public Integrator $integrator;

    private HttpClient $http;

    /**
     * Create a new ePošťák API client.
     *
     * @param array{clientId: string, clientSecret: string, baseUrl?: string, firmId?: string, maxRetries?: int} $config Configuration array.
     *   - `clientId`     (required) OAuth client ID (`sk_live_*` or `sk_int_*`).
     *   - `clientSecret` (required) OAuth client secret.
     *   - `baseUrl`      (optional) Override the API base URL (default: https://epostak.sk/api/v1).
     *   - `firmId`       (optional) Scope all requests to this firm ID (required for `sk_int_*`).
     *   - `maxRetries`   (optional) Maximum retries on 429/5xx responses (default: 3).
     *
     * @throws \InvalidArgumentException If clientId or clientSecret is missing.
     *
     * @example
     *   $client = new EPostak(['clientId' => 'sk_live_...', 'clientSecret' => '...']);
     *   $client->documents->send([...]);
     *
     * @example Scoped to a specific firm:
     *   $client = new EPostak(['clientId' => 'sk_int_...', 'clientSecret' => '...', 'firmId' => 'firm_abc']);
     */
    public function __construct(array $config)
    {
        if (empty($config['clientId']) || !is_string($config['clientId'])) {
            throw new \InvalidArgumentException('EPostak: clientId is required');
        }
        if (empty($config['clientSecret']) || !is_string($config['clientSecret'])) {
            throw new \InvalidArgumentException('EPostak: clientSecret is required');
        }

        $this->clientId = $config['clientId'];
        $this->clientSecret = $config['clientSecret'];
        $this->baseUrl = $config['baseUrl'] ?? self::DEFAULT_BASE_URL;
        $this->firmId = $config['firmId'] ?? null;
        $this->maxRetries = $config['maxRetries'] ?? 3;

        $tokenManager = new TokenManager(
            $this->clientId,
            $this->clientSecret,
            $this->baseUrl,
            $this->firmId
        );

        $this->http = new HttpClient($this->baseUrl, $tokenManager, $this->firmId, $this->maxRetries);

        $this->auth = new Auth($this->http);
        $this->audit = new Audit($this->http);
        $this->documents = new Documents($this->http);
        $this->firms = new Firms($this->http);
        $this->inbound = new Inbound($this->http);
        $this->outbound = new Outbound($this->http);
        $this->peppol = new Peppol($this->http);
        $this->webhooks = new Webhooks($this->http);
        $this->reporting = new Reporting($this->http);
        $this->extract = new Extract($this->http);
        $this->account = new Account($this->http);
        $this->integrator = new Integrator($this->http);
    }

    /**
     * Return the rate-limit snapshot from the most recent API response.
     *
     * Populated after every successful (or failed) call that reaches the server.
     * Returns `null` before the first request or when the last response did not
     * include `X-RateLimit-*` headers.
     *
     * @return RateLimit|null
     *
     * @example
     *   $client->documents->list();
     *   $rl = $client->getLastRateLimit();
     *   if ($rl !== null) {
     *       echo "Remaining: {$rl->remaining}/{$rl->limit}";
     *   }
     */
    public function getLastRateLimit(): ?RateLimit
    {
        return $this->http->getLastRateLimit();
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
            'clientId' => $this->clientId,
            'clientSecret' => $this->clientSecret,
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
