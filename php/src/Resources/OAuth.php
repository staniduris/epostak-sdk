<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\EPostakError;
use GuzzleHttp\Client as GuzzleClient;
use GuzzleHttp\Exception\GuzzleException;

/**
 * Stateless helpers for the **integrator-initiated** OAuth `authorization_code`
 * + PKCE flow. Use these from your own backend when you want to onboard an
 * end-user firm into ePošťák from inside your own application — the user
 * clicks a "Connect ePošťák" button in your UI, lands on the ePošťák
 * `/oauth/authorize` consent page, and ePošťák redirects back to your
 * `redirect_uri` with a `code`.
 *
 * This is independent of the regular `Auth::token()` flow (which uses
 * `client_credentials`). Pick one or the other depending on how the firm is
 * linked to you.
 *
 * The OAuth token endpoint lives at `https://epostak.sk/api/oauth/token` —
 * **not** under `/api/v1` — so this resource bypasses the configured
 * `EPostak` base URL.
 *
 * @example
 *   use EPostak\Resources\OAuth;
 *
 *   // 1. On every onboarding attempt, generate a fresh PKCE pair.
 *   $pair = OAuth::generatePkce();
 *   $_SESSION['epostak_verifier'] = $pair['codeVerifier'];
 *
 *   // 2. Build the authorize URL and redirect the user.
 *   $url = OAuth::buildAuthorizeUrl([
 *       'clientId'      => getenv('EPOSTAK_OAUTH_CLIENT_ID'),
 *       'redirectUri'   => 'https://your-app.com/oauth/epostak/callback',
 *       'codeChallenge' => $pair['codeChallenge'],
 *       'state'         => session_id(),
 *       'scope'         => 'firm:read firm:manage document:send',
 *   ]);
 *   header('Location: ' . $url);
 *
 *   // 3. On callback, exchange the code for a token pair.
 *   $tokens = OAuth::exchangeCode([
 *       'code'         => $_GET['code'],
 *       'codeVerifier' => $_SESSION['epostak_verifier'],
 *       'clientId'     => getenv('EPOSTAK_OAUTH_CLIENT_ID'),
 *       'clientSecret' => getenv('EPOSTAK_OAUTH_CLIENT_SECRET'),
 *       'redirectUri'  => 'https://your-app.com/oauth/epostak/callback',
 *   ]);
 */
final class OAuth
{
    /** Default origin for ePošťák OAuth endpoints. Override for staging. */
    public const DEFAULT_ORIGIN = 'https://epostak.sk';

    /**
     * Generate a fresh PKCE code-verifier + S256 code-challenge pair.
     *
     * The `codeVerifier` is 43 base64url characters (≈256 bits of entropy).
     * Store it server-side keyed by `state` — you must NOT round-trip it
     * through the user's browser, that defeats PKCE.
     *
     * @return array{codeVerifier: string, codeChallenge: string}
     */
    public static function generatePkce(): array
    {
        $codeVerifier = self::base64Url(random_bytes(32));
        $codeChallenge = self::base64Url(hash('sha256', $codeVerifier, true));
        return [
            'codeVerifier' => $codeVerifier,
            'codeChallenge' => $codeChallenge,
        ];
    }

    /**
     * Build a `/oauth/authorize` URL the integrator can redirect the user to.
     * Always sets `response_type=code` and `code_challenge_method=S256`.
     *
     * @param array{
     *   clientId: string,
     *   redirectUri: string,
     *   codeChallenge: string,
     *   state: string,
     *   scope?: string,
     *   origin?: string
     * } $params
     */
    public static function buildAuthorizeUrl(array $params): string
    {
        $query = [
            'client_id' => $params['clientId'],
            'redirect_uri' => $params['redirectUri'],
            'response_type' => 'code',
            'code_challenge' => $params['codeChallenge'],
            'code_challenge_method' => 'S256',
            'state' => $params['state'],
        ];
        if (!empty($params['scope'])) {
            $query['scope'] = $params['scope'];
        }
        $origin = rtrim($params['origin'] ?? self::DEFAULT_ORIGIN, '/');
        return $origin . '/oauth/authorize?' . http_build_query($query, '', '&', PHP_QUERY_RFC3986);
    }

    /**
     * Exchange an authorization `code` for an access + refresh token pair on
     * the OAuth token endpoint. Hits `${origin}/api/oauth/token` directly —
     * does not route through the `EPostak` base URL.
     *
     * The returned access token is a 15-minute JWT; the refresh token is
     * 30-day rotating. Persist both server-side keyed by your firm record.
     *
     * @param array{
     *   code: string,
     *   codeVerifier: string,
     *   clientId: string,
     *   clientSecret: string,
     *   redirectUri: string,
     *   origin?: string
     * } $params
     * @return array{access_token: string, refresh_token: string, token_type: string, expires_in: int, scope?: string}
     * @throws EPostakError On non-2xx responses.
     */
    public static function exchangeCode(array $params): array
    {
        $origin = rtrim($params['origin'] ?? self::DEFAULT_ORIGIN, '/');
        $body = http_build_query([
            'grant_type' => 'authorization_code',
            'code' => $params['code'],
            'code_verifier' => $params['codeVerifier'],
            'client_id' => $params['clientId'],
            'client_secret' => $params['clientSecret'],
            'redirect_uri' => $params['redirectUri'],
        ], '', '&', PHP_QUERY_RFC3986);

        $client = new GuzzleClient(['http_errors' => false]);

        try {
            $response = $client->request('POST', $origin . '/api/oauth/token', [
                'headers' => [
                    'Content-Type' => 'application/x-www-form-urlencoded',
                    'Accept' => 'application/json',
                ],
                'body' => $body,
            ]);
        } catch (GuzzleException $e) {
            throw new EPostakError(0, ['error' => $e->getMessage()]);
        }

        $statusCode = $response->getStatusCode();
        $payload = $response->getBody()->getContents();
        $decoded = [];
        if ($payload !== '') {
            try {
                $decoded = json_decode($payload, true) ?? ['error' => ['message' => $payload]];
            } catch (\Throwable) {
                $decoded = ['error' => ['message' => $payload]];
            }
        }

        if ($statusCode >= 400) {
            throw new EPostakError($statusCode, is_array($decoded) ? $decoded : [], $response->getHeaders());
        }
        return is_array($decoded) ? $decoded : [];
    }

    private static function base64Url(string $bytes): string
    {
        return rtrim(strtr(base64_encode($bytes), '+/', '-_'), '=');
    }
}
