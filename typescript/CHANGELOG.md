# Changelog

All notable changes to `@epostak/sdk` are documented in this file. The
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.0.0 — 2026-04-29

This is a clean break-release that aligns the SDK with the ePošťák public
API after the Wave-5 namespace migration. **All endpoints now live under
`/api/v1`**; the old `/api/enterprise` namespace no longer exists.

ePošťák is pre-launch — there are no production integrators yet — so the
SDK is rebuilt without back-compat shims or deprecation warnings. Update
your imports, dependency pin, and base URL in one go.

### Breaking

- `EPostakConfig.baseUrl` default is now `https://epostak.sk/api/v1`
  (previously `https://epostak.sk/api/enterprise`). Anyone overriding the
  base URL in env vars must update them.
- `Statistics` shape replaced. New fields: `period`, `sent.{total,by_type}`,
  `received.{total,by_type}`, `delivery_rate`, `top_recipients`,
  `top_senders`. Old fields (`outbound`/`inbound`) are gone.
- `StatisticsParams` gains `period?: "month" | "quarter" | "year"`.
- `AuthStatusResponse` re-shaped to match the live route — `key.{id, name,
prefix, permissions, active, createdAt, lastUsedAt}`, `firm.{id,
peppolStatus}`, `plan.{name, expiresAt, active}`, `rateLimit.{perMinute,
window}`, `integrator.{id} | null`.
- `RotateSecretResponse` is now `{ key, prefix, message }` (was a richer
  envelope on the old route).
- Auth/key methods previously hung off `client.account.*` are gone. They
  now live on the new `client.auth.*` resource.
- `client.auth.status()` is `GET` (used to be `POST`).
- `EPostakError` constructor signature is
  `(status, body, headers?: Headers)`. Callers that constructed errors
  manually for tests must update.
- Network errors are still `status: 0` but now carry the failure message
  in `body.error` instead of a separate field.

### Added

- **`client.auth`** — OAuth `client_credentials` flow + key management:
  - `auth.token({ apiKey, firmId?, scope? })` — mint short-lived JWT +
    rotating refresh token.
  - `auth.renew({ refreshToken })` — exchange refresh for a new pair.
  - `auth.revoke({ token, tokenTypeHint? })` — idempotent revocation.
  - `auth.status()` — key introspection.
  - `auth.rotateSecret()` — rotate the calling `sk_live_*` key.
  - `auth.ipAllowlist.get()` / `auth.ipAllowlist.update({ cidrs })` —
    per-key IP allowlist (Wave 3.1).
- **`client.audit`** — per-firm audit feed (`auth.list({ event?,
actorType?, since?, until?, cursor?, limit? })`), cursor-paginated over
  `(occurred_at DESC, id DESC)`. Returns the new generic `CursorPage<T>`.
- **`Idempotency-Key` support** on mutating endpoints. Pass
  `{ idempotencyKey: "..." }` as the second argument to
  `documents.send()`, `documents.sendBatch()`, and `webhooks.create()`.
  The server returns `409 idempotency_conflict` (surfaced as
  `EPostakError` with `code: "idempotency_conflict"`) when the same key
  is replayed before the original request finishes.
- **`verifyWebhookSignature(...)`** — top-level helper that validates
  HMAC-SHA256 webhook deliveries. Parses the `t=...,v1=...` header,
  computes `HMAC(secret, "${t}.${rawBody}")`, and compares with
  `crypto.timingSafeEqual`. Multiple `v1=` values are accepted (rotation
  window). Default 300-second timestamp tolerance, configurable.
- **`EPostakError`** now exposes:
  - RFC 7807 fields: `type`, `title`, `detail`, `instance`.
  - `requestId` — captured from the body or the `X-Request-Id` header.
  - `requiredScope: string | null` — parsed from
    `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`.
- **`SendDocumentResponse.payloadSha256`** — hex SHA-256 over the
  canonical UBL XML wire payload, returned on `201` send responses.
- **`CursorPage<T>`** generic — used today by `audit.list()` and by any
  future cursor-paginated route.
- **`AuditEvent`**, **`AuditActorType`**, **`AuditListParams`**,
  **`StatisticsTopParty`**, **`TokenResponse`**, **`RevokeResponse`**,
  **`IpAllowlistResponse`** types.

### Changed

- `EPostak` client docstring refers to "ePošťák API" (no longer
  "Enterprise API"). The product is one tier now.
- `request()` forwards response headers into `EPostakError` so
  `requestId` and `requiredScope` are populated automatically.
- README rewritten for the v2 surface (auth flow, audit, idempotency,
  webhook verify).

### Removed

- `client.account.status()` and `client.account.rotateSecret()` — moved
  to `client.auth.*`.
- The implicit assumption that "Enterprise" is a separate tier — the SDK
  is now the single official client.

### Migration

```diff
- import { EPostak } from "@epostak/sdk";
- const client = new EPostak({ apiKey, baseUrl: "https://epostak.sk/api/enterprise" });
- const status = await client.account.status();
+ import { EPostak } from "@epostak/sdk";
+ const client = new EPostak({ apiKey });
+ const status = await client.auth.status();
```

```diff
- const stats = await client.reporting.statistics({ from, to });
- console.log(stats.outbound.total, stats.outbound.delivered);
+ const stats = await client.reporting.statistics({ period: "month" });
+ console.log(stats.sent.total, stats.delivery_rate);
+ console.log(stats.top_recipients);
```

```diff
- import { createHmac } from "node:crypto";
- // hand-roll HMAC verification...
+ import { verifyWebhookSignature } from "@epostak/sdk";
+ const result = verifyWebhookSignature({
+   payload: req.rawBody,
+   signatureHeader: req.header("x-epostak-signature") ?? "",
+   secret: process.env.EPOSTAK_WEBHOOK_SECRET!,
+ });
+ if (!result.valid) return res.status(400).end();
```
