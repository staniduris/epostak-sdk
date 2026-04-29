<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;
use GuzzleHttp\Client as GuzzleClient;
use GuzzleHttp\Exception\GuzzleException;

/**
 * OAuth `client_credentials` flow + key management.
 *
 * The token mint endpoint accepts the API key as the `client_secret` of an
 * OAuth `client_credentials` exchange. ePošťák returns a short-lived JWT
 * access token (`expires_in: 900` seconds) and a 30-day rotating refresh
 * token. Use `auth->token()` once at startup, cache the access token, and
 * call `auth->renew()` before it expires.
 *
 * Access via `$client->auth`.
 *
 * @example
 *   $client = new \EPostak\EPostak(['clientId' => 'sk_live_xxx', 'clientSecret' => '...']);
 *
 *   // Mint a JWT manually
 *   $tokens = $client->auth->token('sk_live_xxx', 'secret');
 *   echo $tokens['access_token'];
 *
 *   // Later, before the access token expires:
 *   $renewed = $client->auth->renew($tokens['refresh_token']);
 *
 *   // On logout / key rotation:
 *   $client->auth->revoke($tokens['refresh_token'], 'refresh_token');
 */
class Auth
{
    /** @var IpAllowlist Per-key IP allowlist sub-resource. */
    public IpAllowlist $ipAllowlist;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
        $this->ipAllowlist = new IpAllowlist($http);
    }

    /**
     * Mint an OAuth access token via the `client_credentials` grant.
     *
     * Posts to the SAPI token endpoint (`/sapi/v1/auth/token`) with the
     * given client credentials. For integrator keys (`sk_int_*`) you must
     * also pass `firmId`, which is forwarded as `X-Firm-Id` so the issued
     * JWT is bound to the right tenant.
     *
     * @param string      $clientId     OAuth client ID (`sk_live_*` or `sk_int_*`).
     * @param string      $clientSecret OAuth client secret.
     * @param string|null $firmId       Required when using an integrator key.
     * @param string|null $scope        Optional space-separated scope subset.
     * @return array{access_token: string, refresh_token: string, token_type: string, expires_in: int, scope?: string}
     * @throws EPostakError On API error.
     *
     * @example
     *   // sk_live_* — direct firm access
     *   $tokens = $client->auth->token('sk_live_xxx', 'secret');
     *
     *   // sk_int_* — integrator acting on behalf of a managed firm
     *   $tokens = $client->auth->token('sk_int_xxx', 'secret', 'client-firm-uuid');
     */
    public function token(string $clientId, string $clientSecret, ?string $firmId = null, ?string $scope = null): array
    {
        $body = [
            'grant_type' => 'client_credentials',
            'client_id' => $clientId,
            'client_secret' => $clientSecret,
        ];
        if ($scope !== null) {
            $body['scope'] = $scope;
        }

        $headers = [
            'Accept' => 'application/json',
            'Content-Type' => 'application/json',
        ];
        if ($firmId !== null) {
            $headers['X-Firm-Id'] = $firmId;
        }

        // Token endpoint is on the SAPI path, not the main API path
        $sapiBase = preg_replace('#/api/v1/?$#', '', $this->http->getBaseUrl());
        $url = $sapiBase . '/sapi/v1/auth/token';

        $client = new GuzzleClient(['http_errors' => false]);

        try {
            $response = $client->request('POST', $url, [
                'headers' => $headers,
                'json' => $body,
            ]);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => $e->getMessage()]);
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

        return $payload === '' ? [] : (json_decode($payload, true) ?? []);
    }

    /**
     * Exchange a refresh token for a new access + refresh pair.
     *
     * The old refresh token is invalidated server-side, so always replace
     * your stored refresh token with the value returned by this call.
     *
     * @param string $refreshToken Refresh token previously issued by `token()`.
     * @return array{access_token: string, refresh_token: string, token_type: string, expires_in: int, scope?: string}
     * @throws EPostakError On API error.
     */
    public function renew(string $refreshToken): array
    {
        return $this->http->request('POST', '/auth/renew', [
            'json' => [
                'grant_type' => 'refresh_token',
                'refresh_token' => $refreshToken,
            ],
        ]);
    }

    /**
     * Revoke an access or refresh token.
     *
     * Idempotent — a 200 is returned even if the token is unknown or already
     * revoked, so this is safe to call unconditionally on logout. Pass
     * `$tokenTypeHint` when you know which variant the token is; the server
     * will skip the auto-detect path.
     *
     * @param string      $token         Access or refresh token to revoke.
     * @param string|null $tokenTypeHint One of `access_token`, `refresh_token`, or null.
     * @return array Server confirmation.
     * @throws EPostakError On API error.
     */
    public function revoke(string $token, ?string $tokenTypeHint = null): array
    {
        $body = ['token' => $token];
        if ($tokenTypeHint !== null) {
            $body['token_type_hint'] = $tokenTypeHint;
        }
        return $this->http->request('POST', '/auth/revoke', [
            'json' => $body,
        ]);
    }

    /**
     * Inspect the calling API key, firm, plan, and rate limits.
     *
     * @return array{
     *   key: array{
     *     id: string,
     *     name: string,
     *     prefix: string,
     *     permissions: array,
     *     active: bool,
     *     createdAt: string,
     *     lastUsedAt: ?string
     *   },
     *   firm: array{id: string, peppolStatus: string},
     *   plan: array{name: string, expiresAt: ?string, active: bool},
     *   rateLimit: array{perMinute: int, window: string},
     *   integrator: ?array{id: string}
     * }
     * @throws EPostakError On API error.
     *
     * @example
     *   $info = $client->auth->status();
     *   echo $info['key']['prefix'], " on plan ", $info['plan']['name'];
     */
    public function status(): array
    {
        return $this->http->request('GET', '/auth/status');
    }

    /**
     * Rotate the calling API key.
     *
     * The previous key is deactivated immediately and the new plaintext key
     * is returned ONCE — store it in your secret manager before continuing.
     * Integrator keys (`sk_int_*`) are rejected with 403; rotate those
     * through the integrator dashboard instead.
     *
     * @return array{key: string, prefix: string, message: string} New key material.
     * @throws EPostakError On API error (403 for integrator keys).
     *
     * @example
     *   $rotated = $client->auth->rotateSecret();
     *   save_somewhere_secure($rotated['key']); // shown only once
     */
    public function rotateSecret(): array
    {
        return $this->http->request('POST', '/auth/rotate-secret', [
            'json' => new \stdClass(),
        ]);
    }
}
