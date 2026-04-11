<?php

declare(strict_types=1);

namespace EPostak;

use GuzzleHttp\Client;
use GuzzleHttp\Exception\GuzzleException;
use GuzzleHttp\Exception\RequestException;

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
    private ?string $firmId;

    /**
     * @param string      $baseUrl API base URL (e.g. 'https://epostak.sk/api/enterprise').
     * @param string      $apiKey  Bearer token for authentication.
     * @param string|null $firmId  Optional firm ID sent via X-Firm-Id header.
     */
    public function __construct(string $baseUrl, string $apiKey, ?string $firmId = null)
    {
        $this->apiKey = $apiKey;
        $this->firmId = $firmId;

        $this->client = new Client([
            'base_uri' => rtrim($baseUrl, '/') . '/',
            'http_errors' => false,
        ]);
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

        try {
            $response = $this->client->request($method, $path, $options);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => $e->getMessage()]);
        }

        $statusCode = $response->getStatusCode();

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
            throw new EPostakError($statusCode, $decoded);
        }

        if ($body === '' || $body === '{}') {
            return [];
        }

        return json_decode($body, true);
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
            throw new EPostakError($statusCode, $decoded);
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
