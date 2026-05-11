# Changelog

All notable changes to the `epostak` Python SDK are documented in this
file. The project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 0.9.0 — 2026-05-12

Additive release. Backward-compatible with 0.8.x.

### Added

- **`client.inbound`** (`InboundResource`) — Pull API for received documents:
  - `inbound.list(since=None, limit=100, kind=None, sender=None)` — cursor-paginated list of received documents.
  - `inbound.get(id)` — fetch a single received document.
  - `inbound.get_ubl(id)` — fetch raw UBL XML (returns `str`).
  - `inbound.ack(id, client_reference=None, idempotency_key=None)` — acknowledge receipt; scope `documents:write`.
- **`client.outbound`** (`OutboundResource`) — Pull API for sent documents:
  - `outbound.list(since=None, limit=100, kind=None, status=None, business_status=None, recipient=None)`.
  - `outbound.get(id)` — detail view includes `attempt_history`.
  - `outbound.get_ubl(id)` — raw UBL XML.
  - `outbound.events(since=None, limit=100, document_id=None)` — cursor-paginated delivery event stream.
- **`UblValidationError`** — raised automatically by `build_api_error` when the server returns HTTP 422 with `code == 'UBL_VALIDATION_ERROR'`. Exposes `rule` (e.g. `"BR-06"`) and inherits all `EPostakError` fields.
- **`UBL_RULES`** — constant tuple of the 7 known Peppol BIS 3.0 rule codes: `("BR-02", "BR-05", "BR-06", "BR-11", "BR-16", "BT-1", "PEPPOL-R008")`. New rules may be added; treat as hint, not closed enum.
- **`client.last_rate_limit`** — property returning `{"limit": int, "remaining": int, "reset_at": datetime}` parsed from `X-RateLimit-*` headers, or `None` before the first request. Shared across all resource namespaces on the client instance.
- **`WebhookDelivery.idempotency_key`** — optional field on the `WebhookDelivery` TypedDict.
- **`webhooks.test(id, event=None)`** — now sends `?event=` as a query parameter in addition to the request body, matching server behaviour (PR #114 in epostak backend).

## 0.8.1 — 2026-05-06

Bug-fix release. Two breaking-on-paper but pre-launch-clean fixes for
webhook handling.

### Fixed

- **`verify_webhook_signature()` rewritten.** Was parsing a single Stripe-
  style `X-Epostak-Signature: t=…,v1=…` header. Server has always sent two
  separate headers (`X-Webhook-Signature: sha256=…` and
  `X-Webhook-Timestamp: …`) signed with HMAC-SHA256 over
  `{timestamp}.{body}`. Every previous integration that called
  `verify_webhook_signature` was rejecting valid webhooks. New signature:
  ```python
  verify_webhook_signature(
      payload=request.get_data(),                               # bytes | str
      signature=request.headers.get("x-webhook-signature", ""),# "sha256=<hex>"
      timestamp=request.headers.get("x-webhook-timestamp", ""),# unix seconds
      secret=os.environ["EPOSTAK_WEBHOOK_SECRET"],
  )
  ```
  The function still returns `VerifyWebhookSignatureResult(valid, reason,
  timestamp)` and never raises. A new `reason="unsupported_algorithm"` is
  returned when the signature prefix is not `sha256`.

- **`webhooks.queue.pull()` / `pull_all()` response shape.** Types
  declared `{ events, count }`, server has always returned
  `{ items, has_more }`. Updated `WebhookQueueResponse` and
  `WebhookQueueAllResponse` accordingly. Item field is `event` (not
  `event_type`).

### Migration

```diff
  verify_webhook_signature(
      payload=request.get_data(),
-     signature_header=request.headers.get("X-Epostak-Signature", ""),
+     signature=request.headers.get("x-webhook-signature", ""),
+     timestamp=request.headers.get("x-webhook-timestamp", ""),
      secret=WEBHOOK_SECRET,
  )

  queue = client.webhooks.queue.pull(limit=50)
- for item in queue["events"]:
+ for item in queue["items"]:
-     print(item["event_type"])
+     print(item["event"])
      client.webhooks.queue.ack(item["event_id"])
+ if queue["has_more"]:
+     ...  # pull again
```

## 2.2.0 — 2026-04-29

### Added

- **`client.integrator.licenses.info(offset=0, limit=50)`** — wraps
  `GET /api/v1/integrator/licenses/info`. Returns aggregate plan + current-period
  usage across every firm an integrator manages. Tier rates are applied to the
  AGGREGATE counts (not per-firm summed), so a 100-firm × 50-doc integrator
  lands in tier 2-3 instead of tier 1.
- Response surfaces `billable` (firms on the `integrator-managed` plan that the
  integrator pays for), `nonManaged` (linked firms paying their own plan),
  `exceedsAutoTier` (`True` above 5 000 / month — auto-billing pauses, sales
  handles manually), `contactThreshold`, `pricing.outboundTiers` /
  `pricing.inboundApiTiers`, and a paginated per-firm breakdown.
- Requires `account:read` scope on a `sk_int_*` integrator key. No `X-Firm-Id`
  header — the endpoint is integrator-scoped, not firm-scoped.
- New types: `IntegratorLicenseInfo`, `IntegratorPricingTier`,
  `IntegratorBillableUsage`, `IntegratorNonManagedUsage`,
  `IntegratorFirmUsage`. New resources: `IntegratorResource`,
  `IntegratorLicensesResource`.

## 2.1.0 — 2026-04-29

### Added

- **`OAuth`** — helpers for the integrator-initiated onboarding flow
  (`authorization_code` grant + PKCE S256). Use these from your backend to
  let an end-user firm consent to your integrator app from inside your own
  UI:
  - `OAuth.generate_pkce()` — fresh `{"code_verifier", "code_challenge"}`
    pair.
  - `OAuth.build_authorize_url(client_id, redirect_uri, code_challenge,
state, scope=None, origin=None)` — authorize URL the user is
    redirected to.
  - `OAuth.exchange_code(code, code_verifier, client_id, client_secret,
redirect_uri, origin=None)` — exchanges the returned `code` for a
    `TokenResponse` against `${origin}/api/oauth/token`. Hits the OAuth
    namespace directly, bypassing `EPostak(base_url=...)`.

  Use this when the firm has no API key with you yet. After
  `exchange_code` succeeds, you have a 15-minute access JWT and a 30-day
  rotating refresh token bound to the firm — store both server-side. The
  existing `client.auth.token(api_key=...)` (`client_credentials`)
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
your imports, dependency pin, and base URL in one go.

### Breaking

- `EPostak(base_url=...)` default is now `https://epostak.sk/api/v1`
  (previously `https://epostak.sk/api/enterprise`). Anyone overriding the
  base URL in env vars must update them.
- `Statistics` shape replaced. New fields: `period`, `sent.{total,by_type}`,
  `received.{total,by_type}`, `delivery_rate`, `top_recipients`,
  `top_senders`. Old `outbound`/`inbound` blocks are gone.
- `client.reporting.statistics(period="month" | "quarter" | "year")`
  added as a convenience selector — the explicit
  `from_date`/`to_date` window still works.
- `AuthStatusResponse` re-shaped to match the live route — `key.{id, name,
prefix, permissions, active, createdAt, lastUsedAt}`, `firm.{id,
peppolStatus}`, `plan.{name, expiresAt, active}`, `rateLimit.{perMinute,
window}`, `integrator.{id} | None`. The `AccountStatus` alias is kept
  pointing at the new shape so existing type annotations compile.
- `RotateSecretResponse` is now `{key, prefix, message}` (was a richer
  envelope on the old route).
- Auth/key methods previously hung off `client.account.*` are gone. They
  now live on the new `client.auth.*` resource.
- `client.auth.status()` is `GET` (used to be `POST`).
- `EPostakError.__init__` now accepts an optional `headers` argument so
  the SDK can populate `request_id` and `required_scope`. Callers that
  constructed errors manually for tests must update.
- `verify_webhook_signature(...)` rewritten with a dataclass return type
  (`VerifyWebhookSignatureResult`) and switched from HMAC-SHA512 to
  HMAC-SHA256 to match the server. Old call sites (`payload=`,
  `signature=`, `secret=`) must update — see the **Migration** block at
  the bottom of this entry.

### Added

- **`client.auth`** — OAuth `client_credentials` flow + key management:
  - `auth.token(api_key, firm_id=None, scope=None)` — mint short-lived
    JWT + rotating refresh token.
  - `auth.renew(refresh_token)` — exchange refresh for a new pair.
  - `auth.revoke(token, token_type_hint=None)` — idempotent revocation.
  - `auth.status()` — key introspection (`GET /auth/status`).
  - `auth.rotate_secret()` — rotate the calling `sk_live_*` key.
  - `auth.ip_allowlist.get()` / `auth.ip_allowlist.update(cidrs)` —
    per-key IP allowlist (Wave 3.1).
- **`client.audit`** — per-firm audit feed
  (`audit.list(event=None, actor_type=None, since=None, until=None,
cursor=None, limit=None)`), cursor-paginated over `(occurred_at DESC,
id DESC)`. Returns the new generic `CursorPage[AuditEvent]`.
- **`Idempotency-Key` support** on mutating endpoints. Pass
  `idempotency_key="..."` as a keyword argument to `documents.send()`,
  `documents.send_batch()`, and `webhooks.create()`. The server returns
  `409 idempotency_conflict` (surfaced as `EPostakError` with
  `code == "idempotency_conflict"`) when the same key is replayed before
  the original request finishes.
- **Top-level `verify_webhook_signature(...)`** — exported from the
  `epostak` package root. Validates HMAC-SHA256 webhook deliveries,
  parses the `t=...,v1=...` header, computes
  `HMAC(secret, f"{t}.{raw_body}")` and uses `hmac.compare_digest` for a
  timing-safe compare. Multiple `v1=` values are accepted (rotation
  window). Default 300-second timestamp tolerance, configurable. Returns
  a `VerifyWebhookSignatureResult` dataclass with `valid: bool` and
  `reason: str | None`.
- **`EPostakError`** now exposes:
  - RFC 7807 fields: `type`, `title`, `detail`, `instance`.
  - `request_id` — captured from the body or the `X-Request-Id` header.
  - `required_scope: str | None` — parsed from
    `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`.
- **`SendDocumentResponse.payload_sha256`** — hex SHA-256 over the
  canonical UBL XML wire payload, returned on `201` send responses.
- **`CursorPage[T]`** generic — used today by `audit.list()` and by any
  future cursor-paginated route.
- **`AuditEvent`**, **`AuditActorType`**, **`AuditListParams`**,
  **`StatisticsTopParty`**, **`TokenResponse`**, **`RevokeResponse`**,
  **`IpAllowlistResponse`**, **`AuthStatusKey`**, **`AuthStatusFirm`**,
  **`AuthStatusPlan`**, **`AuthStatusRateLimit`**,
  **`AuthStatusIntegrator`**, **`AuthStatusResponse`** TypedDicts.
- **`ReportingPeriod`** Literal alias (`"month" | "quarter" | "year"`).

### Changed

- `EPostak` client docstring refers to "ePošťák API" (no longer
  "ePostak Enterprise API"). The product is one tier now.
- `_BaseResource._request` forwards response headers into `EPostakError`
  so `request_id` and `required_scope` are populated automatically.
- README rewritten for the v2 surface (auth flow, audit, idempotency,
  webhook verify).

### Removed

- `client.account.status()` and `client.account.rotate_secret()` — moved
  to `client.auth.*`.
- The implicit assumption that "Enterprise" is a separate tier — the SDK
  is now the single official client.
- The legacy HMAC-SHA512 webhook verifier (`verify_webhook_signature`
  returning `bool`). The new SHA-256 helper returns
  `VerifyWebhookSignatureResult` with structured rejection reasons.

### Migration

```diff
- from epostak import EPostak
- client = EPostak(api_key="sk_live_xxx", base_url="https://epostak.sk/api/enterprise")
- status = client.account.status()
+ from epostak import EPostak
+ client = EPostak(api_key="sk_live_xxx")
+ status = client.auth.status()
```

```diff
- stats = client.reporting.statistics(from_date="2026-01-01", to_date="2026-03-31")
- print(stats["outbound"]["total"], stats["outbound"]["delivered"])
+ stats = client.reporting.statistics(period="month")
+ print(stats["sent"]["total"], stats["delivery_rate"])
+ print(stats["top_recipients"])
```

```diff
- from epostak.resources.webhooks import verify_webhook_signature
- ok = verify_webhook_signature(payload=body, signature=hdr, secret=secret)
- if not ok: ...
+ from epostak import verify_webhook_signature
+ result = verify_webhook_signature(
+     payload=body,
+     signature_header=hdr,
+     secret=secret,
+ )
+ if not result.valid:
+     log.warning("rejected webhook: %s", result.reason)
```

```diff
- result = client.documents.send({...})
+ result = client.documents.send({...}, idempotency_key="fv-2026-001-send")
+ print(result["payload_sha256"])  # new field
```
