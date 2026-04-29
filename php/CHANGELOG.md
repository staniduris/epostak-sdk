# Changelog

All notable changes to `epostak/sdk` are documented in this file. The
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.1.0 — 2026-04-29

### Added

- **`EPostak\Resources\OAuth`** — helpers for the integrator-initiated
  onboarding flow (`authorization_code` grant + PKCE S256). Use these from
  your backend to let an end-user firm consent to your integrator app from
  inside your own UI. All methods are `static` — no client instance
  required:
  - `OAuth::generatePkce()` — fresh `['codeVerifier', 'codeChallenge']`
    pair.
  - `OAuth::buildAuthorizeUrl(['clientId', 'redirectUri', 'codeChallenge',
'state', 'scope'?, 'origin'?])` — authorize URL the user is
    redirected to.
  - `OAuth::exchangeCode(['code', 'codeVerifier', 'clientId',
'clientSecret', 'redirectUri', 'origin'?])` — exchanges the returned
    `code` for an access + refresh token pair against
    `${origin}/api/oauth/token`. Hits the OAuth namespace directly,
    bypassing the configured `EPostak` base URL.

  Use this when the firm has no API key with you yet. After
  `exchangeCode()` succeeds, you have a 15-minute access JWT and a 30-day
  rotating refresh token bound to the firm — store both server-side. The
  existing `$client->auth->token($apiKey)` (`client_credentials`)
  continues to be the right choice once the firm is linked through other
  means (dashboard confirm, integrator-managed plan, manual link).

- Required `redirect_uris` must be registered with ePošťák
  (`info@epostak.sk`) before first use — exact-match enforced, no
  wildcards.

## 2.0.0 — 2026-04-29

This is a clean break-release that aligns the SDK with the ePošťák public
API after the Wave-5 namespace migration. **All endpoints now live under
`/api/v1`**; the old `/api/enterprise` namespace no longer exists.

ePošťák is pre-launch — there are no production integrators yet — so the
SDK is rebuilt without back-compat shims or deprecation warnings. Update
your `composer.json` constraint and the base URL in one go.

### Breaking

- Default base URL is now `https://epostak.sk/api/v1` (previously
  `https://epostak.sk/api/enterprise`). Anyone overriding the base URL in
  env vars must update them.
- `Statistics` shape replaced. New keys: `period`, `sent.{total,by_type}`,
  `received.{total,by_type}`, `delivery_rate`, `top_recipients`,
  `top_senders`. Old keys (`outbound`/`inbound`) are gone.
- `Reporting::statistics()` accepts a new `period` parameter
  (`month` | `quarter` | `year`).
- `auth.status` JSON re-shaped to match the live route — `key.{id, name,
prefix, permissions, active, createdAt, lastUsedAt}`, `firm.{id,
peppolStatus}`, `plan.{name, expiresAt, active}`, `rateLimit.{perMinute,
window}`, `integrator.{id} | null`.
- Rotate-secret response is now `{ key, prefix, message }` (was a richer
  envelope on the old route).
- `$client->account->status()` and `$client->account->rotateSecret()` are
  **removed**. They moved to `$client->auth->status()` and
  `$client->auth->rotateSecret()`.
- `$client->auth->status()` is `GET` (used to be `POST`).
- `EPostakError` constructor signature is
  `(int $status, array $body, array $headers = [])`. Callers that
  constructed errors manually for tests must update.
- Network errors are still `status: 0` but now carry the failure message
  in `body.error`.

### Added

- **`$client->auth`** — OAuth `client_credentials` flow + key management:
  - `auth->token($apiKey, $firmId = null, $scope = null)` — mint
    short-lived JWT + rotating refresh token.
  - `auth->renew($refreshToken)` — exchange refresh for a new pair.
  - `auth->revoke($token, $tokenTypeHint = null)` — idempotent revocation.
  - `auth->status()` — key introspection (now `GET`).
  - `auth->rotateSecret()` — rotate the calling `sk_live_*` key.
  - `auth->ipAllowlist->get()` / `auth->ipAllowlist->update($cidrs)` —
    per-key IP allowlist (Wave 3.1).
- **`$client->audit`** — per-firm audit feed with cursor pagination over
  `(occurred_at DESC, id DESC)`. `audit->list(['event'?, 'actorType'?,
'since'?, 'until'?, 'cursor'?, 'limit'?])` returns a `{items,
next_cursor}` page.
- **`Idempotency-Key` support** on mutating endpoints. Pass the key as the
  second argument to `documents->send()`, the second argument to
  `documents->sendBatch()`, and the third argument to
  `webhooks->create()`. The server returns 409 `idempotency_conflict`
  (surfaced as `EPostakError`) when the same key is replayed before the
  original request finishes.
- **`\EPostak\WebhookSignature::verify(...)`** — top-level helper that
  validates HMAC-SHA256 webhook deliveries. Parses the `t=...,v1=...`
  header, computes `HMAC(secret, "${t}.${rawBody}")`, and compares with
  `hash_equals`. Multiple `v1=` values are accepted (rotation window).
  Default 300-second timestamp tolerance, configurable. Returns
  `['valid' => bool, 'reason' => ?string, 'timestamp' => ?int]`.
- **`EPostakError`** now exposes:
  - RFC 7807 fields: `getType()`, `getTitle()`, `getDetail()`,
    `getInstance()`.
  - `getRequestId()` — captured from the body or the `X-Request-Id` header.
  - `getRequiredScope()` — parsed from
    `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`.
- **`SendDocumentResponse.payloadSha256`** — hex SHA-256 over the
  canonical UBL XML wire payload, returned on `201` send responses.

### Changed

- `EPostak` client docstring refers to "ePošťák API" (no longer
  "Enterprise API"). The product is one tier now.
- Internal `HttpClient` forwards Guzzle response headers into
  `EPostakError`, so `getRequestId()` and `getRequiredScope()` are
  populated automatically.
- `WebhookSignature::verify()` is the canonical webhook-verification
  helper; the existing `$client->webhooks->verifyWebhookSignature(...)`
  method is unchanged for legacy callers but new code should use the
  top-level helper.

### Removed

- `$client->account->status()` and `$client->account->rotateSecret()` —
  moved to `$client->auth->*`.
- The implicit assumption that "Enterprise" is a separate tier — the SDK
  is now the single official client.

### Migration

```diff
- $client = new \EPostak\EPostak([
-     'apiKey' => $apiKey,
-     'baseUrl' => 'https://epostak.sk/api/enterprise',
- ]);
- $status = $client->account->status();
+ $client = new \EPostak\EPostak(['apiKey' => $apiKey]);
+ $status = $client->auth->status();
```

```diff
- $stats = $client->reporting->statistics(['from' => $from, 'to' => $to]);
- echo $stats['outbound']['total'];
+ $stats = $client->reporting->statistics(['period' => 'month']);
+ echo $stats['sent']['total'], ' / ', $stats['delivery_rate'];
+ print_r($stats['top_recipients']);
```

```diff
- $valid = $client->webhooks->verifyWebhookSignature(
-     file_get_contents('php://input'),
-     $_SERVER['HTTP_X_EPOSTAK_SIGNATURE'],
-     $secret,
- );
+ $result = \EPostak\WebhookSignature::verify(
+     file_get_contents('php://input'),
+     $_SERVER['HTTP_X_EPOSTAK_SIGNATURE'] ?? '',
+     $secret,
+ );
+ if (!$result['valid']) { http_response_code(400); exit; }
```
