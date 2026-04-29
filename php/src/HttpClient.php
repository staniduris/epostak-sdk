<?php

declare(strict_types=1);

namespace EPostak;

use GuzzleHttp\Client;
use GuzzleHttp\Exception\GuzzleException;

/**
 * Internal HTTP transport layer for the ePošťák SDK.
 *
 * Wraps Guzzle to handle authentication, firm-scoping headers, and
 * error mapping. Not intended for direct use by consumers.
 *
 * @internal
 */
class HttpClient
{
    private Client $client;
    private string $apiKey;
    private string $baseUrl;
    private ?string $firmId;
    private int $maxRetries;

    /** @var string[] HTTP methods that are safe to retry by default. */
    private const RETRYABLE_METHODS = ['GET', 'DELETE'];

    /**
     * @param string      $baseUrl    API base URL (e.g. 'https://epostak.sk/api/v1').
     * @param string      $apiKey     Bearer token for authentication.
     * @param string|null $firmId     Optional firm ID sent via X-Firm-Id header.
     * @param int         $maxRetries Maximum number of retries on 429/5xx (default 3).
     */
    public function __construct(string $baseUrl, string $apiKey, ?string $firmId = null, int $maxRetries = 3)
    {
        $this->apiKey = $apiKey;
        $this->baseUrl = $baseUrl;
        $this->firmId = $firmId;
        $this->maxRetries = $maxRetries;

        $this->client = new Client([
            'base_uri' => rtrim($baseUrl, '/') . '/',
            'http_errors' => false,
        ]);
    }

    public function getApiKey(): string
    {
        return $this->apiKey;
    }

    public function getBaseUrl(): string
    {
        return $this->baseUrl;
    }

    public function getFirmId(): ?string
    {
        return $this->firmId;
    }

    public function getMaxRetries(): int
    {
        return $this->maxRetries;
    }

    /**
     * Send a JSON request and return the decoded response.
     *
     * @param string $method  HTTP method (GET, POST, PATCH, DELETE).
     * @param string $path    Request path relative to the base URL.
     * @param array  $options Guzzle request options (json, query, headers, etc.).
     * @return array|null Decoded JSON body, or null for 204 (No Content) responses.
     * @throws EPostakError On HTTP 4xx/5xx responses or network errors.
     */
    public function request(string $method, string $path, array $options = []): ?array
    {
        $headers = [
            'Authorization' => 'Bearer ' . $this->apiKey,
            'Accept' => 'application/json',
        ];

        if ($this->firmId !== null) {
            $headers['X-Firm-Id'] = $this->firmId;
        }

        if (isset($options['headers'])) {
            $headers = array_merge($headers, $options['headers']);
            unset($options['headers']);
        }

        $options['headers'] = $headers;

        // Strip leading slash — Guzzle base_uri resolution needs relative paths
        $path = ltrim($path, '/');

        $retryable = in_array(strtoupper($method), self::RETRYABLE_METHODS, true);

        for ($attempt = 0; $attempt <= $this->maxRetries; $attempt++) {
            try {
                $response = $this->client->request($method, $path, $options);
            } catch (GuzzleException $e) {
                throw new EPostakError(0, ['error' => $e->getMessage()]);
            }

            $statusCode = $response->getStatusCode();

            // Retry on 429 or 5xx for safe methods
            if ($retryable && $attempt < $this->maxRetries && ($statusCode === 429 || $statusCode >= 500)) {
                $delay = $this->calculateDelay($attempt, $response);
                usleep((int) ($delay * 1_000_000));
                continue;
            }

            if ($statusCode === 204) {
                return null;
            }

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

            if ($body === '' || $body === '{}') {
                return [];
            }

            return json_decode($body, true);
        }

        // Should not be reached, but satisfy static analysis
        throw new EPostakError(0, ['error' => 'Max retries exceeded']);
    }

    /**
     * Calculate the backoff delay for a retry attempt.
     *
     * Uses exponential backoff with jitter: min(base_delay * 2^attempt + jitter, 30s).
     * Respects the Retry-After header on 429 responses.
     *
     * @param int $attempt Current attempt number (0-based).
     * @param \Psr\Http\Message\ResponseInterface $response The HTTP response.
     * @return float Delay in seconds.
     */
    private function calculateDelay(int $attempt, $response): float
    {
        $baseDelay = 0.5;
        $maxDelay = 30.0;

        // Respect Retry-After header on 429
        if ($response->getStatusCode() === 429 && $response->hasHeader('Retry-After')) {
            $retryAfter = $response->getHeaderLine('Retry-After');
            if (is_numeric($retryAfter)) {
                return min((float) $retryAfter, $maxDelay);
            }
            // Try parsing as HTTP date
            $timestamp = strtotime($retryAfter);
            if ($timestamp !== false) {
                return min(max(0, $timestamp - time()), $maxDelay);
            }
        }

        $jitter = mt_rand(0, 1000) / 1000.0; // 0–1s of jitter
        return min($baseDelay * (2 ** $attempt) + $jitter, $maxDelay);
    }

    /**
     * Send a request and return the raw response body as a string.
     *
     * Used for binary downloads (PDF) and XML content (UBL).
     *
     * @param string $method HTTP method (typically GET).
     * @param string $path   Request path relative to the base URL.
     * @return string Raw response body bytes.
     * @throws EPostakError On HTTP 4xx/5xx responses or network errors.
     */
    public function requestRaw(string $method, string $path): string
    {
        $headers = [
            'Authorization' => 'Bearer ' . $this->apiKey,
        ];

        if ($this->firmId !== null) {
            $headers['X-Firm-Id'] = $this->firmId;
        }

        $path = ltrim($path, '/');

        try {
            $response = $this->client->request($method, $path, [
                'headers' => $headers,
            ]);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => $e->getMessage()]);
        }

        $statusCode = $response->getStatusCode();

        if ($statusCode >= 400) {
            $body = $response->getBody()->getContents();
            $decoded = [];
            try {
                $decoded = json_decode($body, true) ?? [];
            } catch (\Throwable) {
                $decoded = ['error' => $response->getReasonPhrase()];
            }
            throw new EPostakError($statusCode, $decoded, $response->getHeaders());
        }

        return $response->getBody()->getContents();
    }

    /**
     * Send a multipart form request.
     *
     * @param string $method    HTTP method (typically POST).
     * @param string $path      Request path relative to the base URL.
     * @param array  $multipart Guzzle multipart form data array.
     * @return array|null Decoded JSON body, or null for 204 responses.
     * @throws EPostakError On HTTP 4xx/5xx responses or network errors.
     */
    public function requestMultipart(string $method, string $path, array $multipart): ?array
    {
        return $this->request($method, $path, [
            'multipart' => $multipart,
        ]);
    }

    /**
     * Build a query string from params, skipping null values.
     *
     * @param array<string, scalar|null> $params Key-value pairs; null values are omitted.
     * @return string Query string prefixed with '?', or empty string if no params remain.
     */
    public static function buildQuery(array $params): string
    {
        $filtered = array_filter($params, fn($v) => $v !== null);

        if (empty($filtered)) {
            return '';
        }

        return '?' . http_build_query($filtered);
    }
}
