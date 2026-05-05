# Changelog

All notable changes to the `epostak` Ruby gem are documented in this file.
The project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Going forward, the gem version (`VERSION` constant) is the source of truth;
earlier CHANGELOG headings used a different numbering scheme.

## 0.8.1 — 2026-05-06

Three P0 bug fixes that affect every integrator using webhooks.
Pre-launch break-clean — no back-compat shims.

### Fixed

- **Webhook signature verification rewritten** — the server signs HMAC-SHA256
  over `"#{timestamp}.#{body}"` and delivers two separate headers:
  `X-Webhook-Signature: sha256=<hex>` and `X-Webhook-Timestamp: <unix>`.
  The previous implementation parsed a single Stripe-style
  `X-Epostak-Signature: t=,v1=` header with HMAC-SHA512 — every prior call
  rejected valid server-signed webhooks.
  - `EPostak.verify_webhook_signature` now takes `signature:` and `timestamp:`
    keyword arguments instead of `signature_header:`.
  - Reason `:no_v1_signature` removed; `:unsupported_algorithm` and
    `:missing_timestamp` added.
  - Hash algorithm changed from SHA-512 to SHA-256.

- **`register_receive_callback` removed** — the server endpoint
  `POST /document/receive-callback` was deleted on 2026-05-05.
  Calling it would have returned 404. Migration: use
  `client.webhooks.create(url:, events:)` instead.

- **Webhook queue response shape corrected** — server returns
  `{ items: [...], has_more: bool }`, not `{ events: [...], count: N }`.
  YARD docs and README examples updated throughout.

### Migration

```diff
- result = EPostak.verify_webhook_signature(
-   payload:          raw,
-   signature_header: request.env["HTTP_X_EPOSTAK_SIGNATURE"].to_s,
-   secret:           ENV["EPOSTAK_WEBHOOK_SECRET"]
- )
+ result = EPostak.verify_webhook_signature(
+   payload:   raw,
+   signature: request.env["HTTP_X_WEBHOOK_SIGNATURE"].to_s,
+   timestamp: request.env["HTTP_X_WEBHOOK_TIMESTAMP"].to_s,
+   secret:    ENV["EPOSTAK_WEBHOOK_SECRET"]
+ )
```

```diff
- client.webhooks.register_receive_callback(url: "https://...", events: [...])
+ client.webhooks.create(url: "https://...", events: [...])
```

```diff
- response["events"].each { |item| process(item) }
- break if response["count"] == 0
+ response["items"].each { |item| process(item) }
+ break unless response["has_more"]
```

## 3.1.0 — 2026-04-30

### Added

- **`client.webhooks.register_receive_callback(url:, events:)`** — wraps
  `POST /document/receive-callback` (also at SAPI alias
  `/sapi/v1/document/receive-callback`). Registers a webhook URL for
  inbound document notifications. Requires `webhooks:write` scope.
  Returns `{ "id", "url", "events", "secret", "is_active",
"created_at" }` — `secret` is shown only once.

### Changed

- **`client.auth.status`** response now includes `firm_id`, `key_type`,
  and `scope` fields alongside the existing `key`, `firm`, `plan`,
  `rateLimit`, and `integrator` envelope.
- **`/sapi/v1/auth/status`** is now documented as an alias for
  `client.auth.status`.
- **Scope requirement documented** for 4 endpoints that now require
  `documents:read`: `documents.validate`, `documents.parse`,
  `documents.convert`, and `peppol.directory.search`.

## 2.2.0 — 2026-04-29

### Added

- **`client.integrator.licenses.info(offset:, limit:)`** — wraps
  `GET /api/v1/integrator/licenses/info`. Returns aggregate plan +
  current-period usage across every firm an integrator manages. Tier rates
  apply to the AGGREGATE counts (not per-firm summed), so a 100-firm ×
  50-doc integrator lands in tier 2–3 instead of tier 1.
- Response surfaces `billable` (firms on the `integrator-managed` plan that
  the integrator pays for), `nonManaged` (linked firms paying their own
  plan), `exceedsAutoTier` (`true` above 5 000 / month — auto-billing
  pauses, sales handles manually), `contactThreshold`, `pricing`, and a
  paginated per-firm breakdown.
- Requires `account:read` scope on a `sk_int_*` integrator key. No
  `X-Firm-Id` header — the endpoint is integrator-scoped, not firm-scoped.
- New resources: `EPostak::Resources::Integrator`,
  `EPostak::Resources::IntegratorLicenses`.

## 2.1.0 — 2026-04-29

### Added

- **`EPostak::OAuth`** — module-function helpers for the
  integrator-initiated onboarding flow (`authorization_code` grant + PKCE
  S256). Use these from your backend to let an end-user firm consent to
  your integrator app from inside your own UI:
  - `EPostak::OAuth.generate_pkce` — fresh
    `{ code_verifier:, code_challenge: }` pair.
  - `EPostak::OAuth.build_authorize_url(client_id:, redirect_uri:,
code_challenge:, state:, scope: nil, origin: nil)` — authorize URL the
    user is redirected to.
  - `EPostak::OAuth.exchange_code(code:, code_verifier:, client_id:,
client_secret:, redirect_uri:, origin: nil)` — exchanges the returned
    `code` for an access + refresh token pair against
    `${origin}/api/oauth/token`. Hits the OAuth namespace directly,
    bypassing the configured client base URL.

  Use this when the firm has no API key with you yet. After
  `exchange_code` succeeds, you have a 15-minute access JWT and a 30-day
  rotating refresh token bound to the firm — store both server-side. The
  existing `client.auth.token(api_key:)` (`client_credentials`) continues
  to be the right choice once the firm is linked through other means
  (dashboard confirm, integrator-managed plan, manual link).

- Required `redirect_uris` must be registered with ePošťák
  (`info@epostak.sk`) before first use — exact-match enforced, no
  wildcards.

## 2.0.0 — 2026-04-29

This is a clean break-release that aligns the Ruby SDK with the ePošťák
public API after the Wave-5 namespace migration. **All endpoints now live
under `/api/v1`**; the old `/api/enterprise` namespace no longer exists.

ePošťák is pre-launch — there are no production integrators yet — so the
SDK is rebuilt without back-compat shims or deprecation warnings. Update
your dependency pin and base URL in one go.

### Breaking

- `EPostak::DEFAULT_BASE_URL` is now `https://epostak.sk/api/v1`
  (previously `https://epostak.sk/api/enterprise`). Anyone overriding the
  base URL in env vars must update them.
- `client.reporting.statistics` now returns `period`, `sent`, `received`,
  `delivery_rate`, `top_recipients`, `top_senders`. Old `outbound`/`inbound`
  fields are gone. Accepts a new `period:` kwarg (`"month"`, `"quarter"`,
  `"year"`).
- `client.auth.status` is **GET** (was POST in v1) and now returns the
  re-shaped envelope: `key.{id, name, prefix, permissions, active,
createdAt, lastUsedAt}`, `firm.{id, peppolStatus}`, `plan.{name,
expiresAt, active}`, `rateLimit.{perMinute, window}`, `integrator.{id}`
  or `nil`.
- `client.account.status` and `client.account.rotate_secret` were removed.
  Use `client.auth.status` and `client.auth.rotate_secret` instead.
- `client.auth.rotate_secret` returns `{ "key", "prefix", "message" }`.

### Added

- **`client.auth`** — OAuth `client_credentials` flow + key management:
  - `auth.token(api_key:, firm_id: nil, scope: nil)` — mint short-lived JWT
    - rotating refresh token.
  - `auth.renew(refresh_token:)` — exchange refresh for a new pair.
  - `auth.revoke(token:, token_type_hint: nil)` — idempotent revocation.
  - `auth.status` — key introspection (GET).
  - `auth.rotate_secret` — rotate the calling `sk_live_*` key.
  - `auth.ip_allowlist.get` / `auth.ip_allowlist.update(cidrs:)` — per-key
    IP allowlist (Wave 3.1).
- **`client.audit`** — per-firm audit feed:
  `audit.list(event:, actor_type:, since:, until_ts:, cursor:, limit:)`,
  cursor-paginated over `(occurred_at DESC, id DESC)`. Returns the new
  generic `{ "items" => [...], "next_cursor" => String|nil }` page shape.
- **`Idempotency-Key` support** on mutating endpoints. Pass
  `idempotency_key:` to `documents.send_document`, `documents.send_batch`,
  and `webhooks.create`. The server returns `409 idempotency_conflict`
  (surfaced as `EPostak::Error` with `code == "idempotency_conflict"`)
  when the same key is replayed before the original request finishes.
- **`EPostak.verify_webhook_signature(payload:, signature_header:, secret:,
tolerance_seconds: 300)`** — top-level helper that validates HMAC-SHA256
  webhook deliveries. Parses the `t=...,v1=...` header, computes
  `HMAC(secret, "#{t}.#{raw_body}")`, and compares with
  `OpenSSL.fixed_length_secure_compare` when available (falls back to a
  manual constant-time compare). Multiple `v1=` values are accepted
  (rotation window). Default 300-second timestamp tolerance, configurable.
  Returns `{ valid:, reason:, timestamp: }`.
- **`EPostak::Error`** now exposes:
  - RFC 7807 fields: `type`, `title`, `detail`, `instance`.
  - `request_id` — captured from the body or the `X-Request-Id` header.
  - `required_scope` — parsed from
    `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`.
- **`SendDocumentResponse#payload_sha256`** — hex SHA-256 over the
  canonical UBL XML wire payload, returned on `201` send responses.

### Changed

- `EPostak::Client` docstring refers to "ePošťák API" (no longer
  "Enterprise API"). The product is one tier now.
- `HttpClient#request` forwards response headers into `EPostak::Error` so
  `request_id` and `required_scope` are populated automatically.

### Removed

- `client.account.status` and `client.account.rotate_secret` — moved to
  `client.auth.*`.
- The implicit assumption that "Enterprise" is a separate tier — the SDK
  is now the single official client.

### Migration

```diff
- client = EPostak::Client.new(
-   api_key: ENV["EPOSTAK_API_KEY"],
-   base_url: "https://epostak.sk/api/enterprise"
- )
- status = client.account.status
+ client = EPostak::Client.new(api_key: ENV["EPOSTAK_API_KEY"])
+ status = client.auth.status
```

```diff
- stats = client.reporting.statistics(from: from, to: to)
- puts stats["outbound"]["total"], stats["outbound"]["delivered"]
+ stats = client.reporting.statistics(period: "month")
+ puts stats["sent"]["total"], stats["delivery_rate"]
+ puts stats["top_recipients"]
```

```diff
- valid = EPostak::Webhook.verify_signature(
-   payload: raw, signature: header, secret: secret
- )
- halt 400 unless valid
+ result = EPostak.verify_webhook_signature(
+   payload: raw,
+   signature_header: header,
+   secret: secret
+ )
+ halt 400, "bad signature: #{result[:reason]}" unless result[:valid]
```
