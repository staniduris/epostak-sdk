<?php

declare(strict_types=1);

namespace EPostak;

use GuzzleHttp\Client as GuzzleClient;
use GuzzleHttp\Exception\GuzzleException;

/**
 * Manages OAuth JWT tokens for the ePošťák SDK.
 *
 * Mints tokens via POST /sapi/v1/auth/token (client_credentials grant),
 * caches the JWT, and auto-refreshes 60 seconds before expiry via
 * /sapi/v1/auth/renew.
 *
 * @internal
 */
class TokenManager
{
    private const SAPI_TOKEN_PATH = '/sapi/v1/auth/token';
    private const SAPI_RENEW_PATH = '/sapi/v1/auth/renew';
    private const REFRESH_BUFFER_SECONDS = 60;

    private string $clientId;
    private string $clientSecret;
    private string $baseUrl;
    private ?string $firmId;
    private ?string $scope;

    private ?string $accessToken = null;
    private ?string $refreshToken = null;
    private float $expiresAt = 0;

    /**
     * @param string      $clientId     OAuth client ID (sk_live_* or sk_int_*).
     * @param string      $clientSecret OAuth client secret.
     * @param string      $baseUrl      API base URL (e.g. 'https://epostak.sk/api/v1').
     * @param string|null $firmId       Optional firm ID for integrator keys.
     * @param string|null $scope        Optional space-separated scope subset.
     */
    public function __construct(
        string $clientId,
        string $clientSecret,
        string $baseUrl,
        ?string $firmId = null,
        ?string $scope = null
    ) {
        $this->clientId = $clientId;
        $this->clientSecret = $clientSecret;
        $this->baseUrl = $baseUrl;
        $this->firmId = $firmId;
        $this->scope = $scope;
    }

    /**
     * Get a valid access token, minting or refreshing as needed.
     *
     * @return string JWT access token.
     * @throws EPostakError On token mint/renew failure.
     */
    public function getAccessToken(): string
    {
        if ($this->accessToken !== null && microtime(true) < $this->expiresAt - self::REFRESH_BUFFER_SECONDS) {
            return $this->accessToken;
        }

        // Try renew first if we have a refresh token
        if ($this->refreshToken !== null && $this->accessToken !== null) {
            try {
                $this->doRenew();
                return $this->accessToken;
            } catch (\Throwable) {
                // Renew failed, fall through to full mint
            }
        }

        $this->doMint();
        return $this->accessToken;
    }

    public function getClientId(): string
    {
        return $this->clientId;
    }

    public function getClientSecret(): string
    {
        return $this->clientSecret;
    }

    /**
     * Compute the SAPI base URL by stripping /api/v1 from the configured baseUrl.
     */
    private function sapiBaseUrl(): string
    {
        return preg_replace('#/api/v1/?$#', '', $this->baseUrl);
    }

    /**
     * Mint a fresh JWT via client_credentials grant.
     */
    private function doMint(): void
    {
        $url = $this->sapiBaseUrl() . self::SAPI_TOKEN_PATH;

        $headers = [
            'Content-Type' => 'application/json',
            'Accept' => 'application/json',
        ];
        if ($this->firmId !== null) {
            $headers['X-Firm-Id'] = $this->firmId;
        }

        $body = [
            'grant_type' => 'client_credentials',
            'client_id' => $this->clientId,
            'client_secret' => $this->clientSecret,
        ];
        if ($this->scope !== null) {
            $body['scope'] = $this->scope;
        }

        $client = new GuzzleClient(['http_errors' => false]);

        try {
            $response = $client->request('POST', $url, [
                'headers' => $headers,
                'json' => $body,
            ]);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => 'Token mint failed: ' . $e->getMessage()]);
        }

        $statusCode = $response->getStatusCode();
        $payload = $response->getBody()->getContents();

        if ($statusCode >= 400) {
            $decoded = [];
            try {
                $decoded = json_decode($payload, true) ?? [];
            } catch (\Throwable) {
                $decoded = ['error' => $response->getReasonPhrase()];
            }
            throw new EPostakError($statusCode, $decoded, $response->getHeaders());
        }

        $data = json_decode($payload, true) ?? [];
        $this->applyTokenResponse($data);
    }

    /**
     * Refresh the JWT using the stored refresh token.
     */
    private function doRenew(): void
    {
        $url = $this->sapiBaseUrl() . self::SAPI_RENEW_PATH;

        $client = new GuzzleClient(['http_errors' => false]);

        try {
            $response = $client->request('POST', $url, [
                'headers' => [
                    'Content-Type' => 'application/json',
                    'Accept' => 'application/json',
                    'Authorization' => 'Bearer ' . $this->accessToken,
                ],
                'json' => [
                    'grant_type' => 'refresh_token',
                    'refresh_token' => $this->refreshToken,
                ],
            ]);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => 'Token renew failed: ' . $e->getMessage()]);
        }

        $statusCode = $response->getStatusCode();
        if ($statusCode >= 400) {
            $payload = $response->getBody()->getContents();
            $decoded = [];
            try {
                $decoded = json_decode($payload, true) ?? [];
            } catch (\Throwable) {
                $decoded = ['error' => $response->getReasonPhrase()];
            }
            throw new EPostakError($statusCode, $decoded, $response->getHeaders());
        }

        $data = json_decode($response->getBody()->getContents(), true) ?? [];
        $this->applyTokenResponse($data);
    }

    private function applyTokenResponse(array $data): void
    {
        $this->accessToken = $data['access_token'] ?? '';
        $this->refreshToken = $data['refresh_token'] ?? null;
        $this->expiresAt = microtime(true) + ($data['expires_in'] ?? 900);
    }
}
