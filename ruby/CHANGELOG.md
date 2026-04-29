# Changelog

All notable changes to the `epostak` Ruby gem are documented in this file.
The project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
