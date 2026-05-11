# Changelog

All notable changes to `epostak/sdk` are documented in this file. The
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **Note:** Previous entries in this file incorrectly listed versions as
> `3.x.x`. The actual published version on Packagist has always been `0.8.x`.
> The version history is corrected going forward.

## 0.9.0 — 2026-05-12

### Added

- **`$client->inbound`** — Pull API resource for inbound Peppol documents:
  - `inbound->list($params)` — cursor-paginated list (`since`, `limit`, `kind`, `sender`, `next_cursor`).
  - `inbound->get($id)` — single document detail.
  - `inbound->getUbl($id)` — raw UBL XML string (`application/xml`).
  - `inbound->ack($id, ['client_reference' => '...'])` — acknowledge with optional ERP reference (idempotent, latest-ack-wins).

- **`$client->outbound`** — Pull API resource for outbound Peppol documents:
  - `outbound->list($params)` — cursor-paginated list (`since`, `limit`, `kind`, `status`, `business_status`, `recipient`, `next_cursor`).
  - `outbound->get($id)` — single document detail (includes `attempt_history`).
  - `outbound->getUbl($id)` — raw UBL XML string.
  - `outbound->events($params)` — cursor-paginated document event stream (`document_id`, `next_cursor`, `limit`).

- **`UblValidationException`** — thrown when the API returns HTTP 422 with
  `code: "UBL_VALIDATION_ERROR"`. Exposes `$rule` (machine-readable rule code)
  and `$requestId`. Extends `EPostakError` — existing catch blocks still work.

- **`UblRule`** — class constants for the 7 known UBL rule codes:
  `SCHEMA_VIOLATION`, `MISSING_MANDATORY_ELEMENT`, `INVALID_CODE_LIST_VALUE`,
  `CALCULATION_ERROR`, `BUSINESS_RULE_VIOLATION`, `ENDPOINT_MISMATCH`,
  `UNKNOWN_RECEIVER`.

- **`$client->getLastRateLimit()`** — returns a `RateLimit` value object
  (`limit`, `remaining`, `resetAt`) populated from `X-RateLimit-*` response
  headers after every request. Returns `null` before first request.

- **`RateLimit`** DTO — `readonly` value object with `limit: ?int`,
  `remaining: ?int`, `resetAt: ?\DateTimeImmutable`.

- **`webhooks->test($id, $params)`** now accepts `['event' => '...']` array
  and forwards the event as `?event=` query parameter (server-side priority
  over body per PR #114). String shorthand `test($id, 'document.delivered')`
  still works.

- **`webhooks->deliveries()`** — PHPDoc updated to document
  `idempotency_key?: string|null` on each delivery object, and `nextCursor`
  on the paginated response. Params now include `cursor`, `includeResponseBody`,
  and `include` pass-through fields.

## 0.8.1 — 2026-05-06

Bug-fix release. Four P0 fixes for webhook handling identified by audit.
Pre-launch clean break — no compat shims.

### Fixed

- **`WebhookSignature::verify()` rewritten.** Was parsing a single
  Stripe-style `X-Epostak-Signature: t=…,v1=…` header with HMAC-SHA512.
  Server has always sent two separate headers (`X-Webhook-Signature: sha256=…`
  and `X-Webhook-Timestamp: …`) signed with HMAC-SHA256 over
  `${timestamp}.${body}`. Every previous call to `WebhookSignature::verify()`
  was rejecting valid webhooks. New signature:
  ```php
  WebhookSignature::verify(
      signature:  $_SERVER['HTTP_X_WEBHOOK_SIGNATURE'] ?? '',
      timestamp:  $_SERVER['HTTP_X_WEBHOOK_TIMESTAMP'] ?? '',
      body:       $raw,
      secret:     getenv('EPOSTAK_WEBHOOK_SECRET'),
  );
  ```
  Returns `['valid' => bool, 'reason' => ?string, 'timestamp' => ?int]`.
  Algorithm prefix other than `sha256=` is rejected with `unknown_algorithm`.

- **`webhooks->queue->pull()` response shape.** PHPDoc declared
  `{events, count, firm_id}`; server has always returned `{items, has_more}`.
  PHPDoc and example updated to match.

- **README quick-start fixed.** Constructor examples used `apiKey` key which
  does not exist — constructor requires `clientId` + `clientSecret`. All
  four occurrences (lines 22, 55, 472, 475) corrected. Base URL in
  constructor docs corrected from `api/enterprise` to `api/v1`.

### Removed

- **`$client->webhooks->registerReceiveCallback()` deleted.** The underlying
  endpoint `POST /sapi/v1/document/receive-callback` was removed on the server
  (2026-05-05). Use `$client->webhooks->create($url, $events)` instead.

- **`$client->webhooks->verifyWebhookSignature()` deleted.** Was using wrong
  header name, wrong format, and HMAC-SHA512. Use the canonical
  `\EPostak\WebhookSignature::verify()` top-level helper instead.

### Migration

```diff
- $client->webhooks->registerReceiveCallback($url, $events);
+ $client->webhooks->create($url, $events);

- $isValid = $client->webhooks->verifyWebhookSignature(
-     file_get_contents('php://input'),
-     $_SERVER['HTTP_X_EPOSTAK_SIGNATURE'],
-     $secret
- );
+ $result = \EPostak\WebhookSignature::verify(
+     signature:  $_SERVER['HTTP_X_WEBHOOK_SIGNATURE'] ?? '',
+     timestamp:  $_SERVER['HTTP_X_WEBHOOK_TIMESTAMP'] ?? '',
+     body:       file_get_contents('php://input'),
+     secret:     $secret,
+ );
+ $isValid = $result['valid'];

  foreach ($queue['items'] as $item) {
-     $client->webhooks->queue->ack($item['id']);
+     $client->webhooks->queue->ack($item['event_id']);
  }
// $queue['has_more'] is bool (was $queue['count'] int)

- $client = new EPostak(['apiKey' => 'sk_live_...']);
+ $client = new EPostak(['clientId' => 'sk_live_...', 'clientSecret' => '...']);
```

## 3.1.0 — 2026-04-30

### Added

- **`$client->webhooks->registerReceiveCallback($url, $events)`** — wraps
  `POST /document/receive-callback` (also at SAPI alias
  `/sapi/v1/document/receive-callback`). Registers a webhook URL for
  inbound document notifications. Requires `webhooks:write` scope.
  Returns `{ id, url, events, secret, is_active, created_at }` —
  `secret` is shown only once.

### Changed

- **`$client->auth->status()`** response now includes `firm_id`,
  `key_type`, and `scope` fields alongside the existing `key`, `firm`,
  `plan`, `rateLimit`, and `integrator` envelope.
- **`/sapi/v1/auth/status`** is now documented as an alias for
  `$client->auth->status()`.
- **Scope requirement documented** for 4 endpoints that now require
  `documents:read`: `documents->validate()`, `documents->parse()`,
  `documents->convert()`, and `peppol->directory->search()`.

## 2.2.0 — 2026-04-29

### Added

- **`$client->integrator->licenses->info(['offset' => …, 'limit' => …])`** —
  wraps `GET /api/v1/integrator/licenses/info`. Returns aggregate plan +
  current-period usage across every firm an integrator manages. Tier rates
  are applied to the AGGREGATE counts (not per-firm summed), so a 100-firm ×
  50-doc integrator lands in tier 2–3 instead of tier 1.
- Response surfaces `billable` (firms on the `integrator-managed` plan that
  the integrator pays for), `nonManaged` (linked firms paying their own
  plan), `exceedsAutoTier` (`true` above 5 000 / month — auto-billing
  pauses, sales handles manually), `contactThreshold`, `pricing.outboundTiers`
  / `pricing.inboundApiTiers`, and a paginated per-firm breakdown.
- Requires `account:read` scope on a `sk_int_*` integrator key. No
  `X-Firm-Id` header — the endpoint is integrator-scoped, not firm-scoped.
- New resource classes `EPostak\Resources\Integrator` and
  `EPostak\Resources\IntegratorLicenses`.

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
