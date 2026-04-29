# Changelog

All notable changes to the official ePošťák Java SDK
(`sk.epostak:epostak-sdk`) are documented in this file. The project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.0.0 — 2026-04-29

This is a clean break-release that aligns the Java SDK with the ePošťák
public API after the Wave-5 namespace migration. **All endpoints now live
under `/api/v1`**; the old `/api/enterprise` namespace no longer exists.

ePošťák is pre-launch — there are no production integrators yet — so the
SDK is rebuilt without back-compat shims or deprecation warnings. Update
your dependency pin and base URL override in one go.

### Breaking

- `EPostak.builder().baseUrl(...)` default is now
  `https://epostak.sk/api/v1` (previously `https://epostak.sk/api/enterprise`).
  Anyone overriding the base URL in env vars or properties must update them.
- `Statistics` shape replaced. New fields: `period`, `sent` (total +
  `byType`), `received` (total + `byType`), `deliveryRate`, `topRecipients`,
  `topSenders`. Old `outbound` / `inbound` shape is gone.
- `Statistics` is now requested via
  `client.reporting().statistics("month")` (or `"quarter"` / `"year"`),
  with `statisticsRange(from, to)` and `statistics(period, from, to)`
  overloads for explicit windows.
- `AuthStatusResponse` re-shaped to match the live route — `key`
  (id/name/prefix/permissions/active/createdAt/lastUsedAt), `firm`
  (id/peppolStatus), `plan` (name/expiresAt/active), `rateLimit`
  (perMinute/window), `integrator` (id) — null when the key is not an
  integrator subkey.
- `RotateSecretResponse` is now `{ key, prefix, message }`.
- Auth/key methods previously hung off `client.account().*` are gone. They
  now live on the new `client.auth().*` resource.
- `client.auth().status()` is `GET` (used to be `POST`).
- `WebhookSignature.verify(...)` now returns a structured
  `WebhookSignature.VerifyResult` (`valid` / `reason` / `timestamp`)
  instead of a raw `boolean`. The signature algorithm is **HMAC-SHA256**
  (was SHA-512) and the timestamp is unix **seconds** (was milliseconds).
  The header still parses as `t=...,v1=...`; multiple `v1=` candidates
  are accepted to support secret rotation. Timing-safe compare uses
  `MessageDigest.isEqual`. Default tolerance: 300 s.
- `EPostakException` constructor signature gained RFC 7807 (`type`,
  `title`, `detail`, `instance`), `requestId`, and `requiredScope` fields.
  Callers that constructed exceptions manually for tests must update.
- Network errors are still `status == 0` but now carry the failure
  message in the exception message itself; the bare `(int, String)`
  constructor still works.

### Added

- **`client.auth()`** — OAuth `client_credentials` flow + key management:
  - `auth().token(apiKey, firmId?, scope?)` — mint short-lived JWT +
    rotating refresh token. Convenience overload `auth().token(apiKey)`.
  - `auth().renew(refreshToken)` — exchange refresh for a new pair.
  - `auth().revoke(token, tokenTypeHint?)` — idempotent revocation.
    Convenience overload `auth().revoke(token)`.
  - `auth().status()` — key introspection.
  - `auth().rotateSecret()` — rotate the calling `sk_live_*` key.
  - `auth().ipAllowlist().get()` / `auth().ipAllowlist().update(cidrs)` —
    per-key IP allowlist (Wave 3.1).
- **`client.audit()`** — per-firm audit feed (`audit().list(params)`),
  cursor-paginated over `(occurred_at DESC, id DESC)`. Returns the new
  generic `CursorPage<T>`. `AuditListParams` exposes a fluent builder.
- **`Idempotency-Key` support** on mutating endpoints. Pass an
  `idempotencyKey` string as the second argument to
  `documents().send(...)`, `documents().sendBatch(...)`, and
  `webhooks().create(...)`. The server returns
  `409 idempotency_conflict` (surfaced as `EPostakException` with
  `code == "idempotency_conflict"`) when the same key is replayed before
  the original request finishes.
- **`WebhookSignature.VerifyResult`** — top-level helper that validates
  HMAC-SHA256 webhook deliveries. Parses the `t=...,v1=...` header,
  computes `HMAC(secret, "${t}.${rawBody}")`, and compares with
  `MessageDigest.isEqual`. Multiple `v1=` values are accepted (rotation
  window). Default 300-second timestamp tolerance, configurable via the
  `verify(payload, header, secret, toleranceSeconds)` overload. A
  `verify(byte[] payload, ...)` overload is provided for frameworks that
  hand the raw request bytes directly.
- **`EPostakException`** now exposes:
  - RFC 7807 fields: `getType()`, `getTitle()`, `getDetail()`,
    `getInstance()`.
  - `getRequestId()` — captured from the body or the `X-Request-Id`
    response header.
  - `getRequiredScope()` — parsed from
    `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`.
- **`SendDocumentResponse.payloadSha256()`** — hex SHA-256 over the
  canonical UBL XML wire payload, returned on `201` send responses.
- **`CursorPage<T>`** generic — used today by `audit().list()` and by any
  future cursor-paginated route.
- **`AuditEvent`**, **`AuditActorType`**, **`AuditListParams`**,
  **`TokenResponse`**, **`RevokeResponse`**, **`IpAllowlistResponse`**,
  **`Statistics.PartyCount`** types.

### Changed

- Client docstring refers to "ePošťák API" (no longer "Enterprise API").
  The product is one tier now.
- `HttpClient` forwards response headers into `EPostakException` so
  `requestId` and `requiredScope` are populated automatically.
- README rewritten for the v2 surface (auth flow, audit, idempotency,
  webhook verify).

### Removed

- `client.account().status()` and `client.account().rotateSecret()` —
  moved to `client.auth().*`.
- The implicit assumption that "Enterprise" is a separate tier — the SDK
  is now the single official client.

### Migration

```java
// Before
EPostak client = EPostak.builder()
    .apiKey(apiKey)
    .baseUrl("https://epostak.sk/api/enterprise")
    .build();
AuthStatusResponse status = client.account().status();

// After
EPostak client = EPostak.builder().apiKey(apiKey).build();
AuthStatusResponse status = client.auth().status();
```

```java
// Before
Statistics stats = client.reporting().statistics(from, to);
System.out.println(stats.outbound().total() + " sent, "
        + stats.outbound().delivered() + " delivered");

// After
Statistics stats = client.reporting().statistics("month");
System.out.println(stats.sent().total() + " sent, "
        + stats.deliveryRate() + " delivered ratio");
stats.topRecipients().forEach(p ->
        System.out.println(p.name() + " " + p.count()));
```

```java
// Before — boolean result, SHA-512, ms timestamps
boolean ok = WebhookSignature.verify(rawBody,
        request.getHeader("X-Epostak-Signature"),
        System.getenv("EPOSTAK_WEBHOOK_SECRET"));

// After — structured result, SHA-256, unix seconds, multi-v1
WebhookSignature.VerifyResult r = WebhookSignature.verify(rawBody,
        request.getHeader("X-Epostak-Signature"),
        System.getenv("EPOSTAK_WEBHOOK_SECRET"));
if (!r.valid()) {
    response.setStatus(400);
    response.getWriter().write("bad signature: " + r.reason());
    return;
}
```
