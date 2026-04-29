# Changelog

All notable changes to the `EPostak` .NET SDK are documented in this file. The
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.0.0 — 2026-04-29

This is a clean break-release that aligns the .NET SDK with the ePošťák public
API after the Wave-5 namespace migration. **All endpoints now live under
`/api/v1`**; the old `/api/enterprise` namespace no longer exists.

ePošťák is pre-launch — there are no production integrators yet — so the SDK
is rebuilt without back-compat shims. Update your package version and base
URL in one go.

### Breaking

- `EPostakConfig.BaseUrl` default is now `https://epostak.sk/api/v1`
  (previously `https://epostak.sk/api/enterprise`). Anyone overriding the
  base URL in env vars must update them.
- `Statistics` shape replaced. New fields: `Period`, `Sent` / `Received`
  (each `{Total, ByType}`), `DeliveryRate`, `TopRecipients`, `TopSenders`.
  Old fields (`Outbound`/`Inbound`) are gone.
- `StatisticsParams` gains `ReportingPeriod? Period { get; set; }`.
- `AuthStatusResponse.Key.Permissions` is now a `string` (space-separated)
  instead of `List<string>`.
- `RotateSecretResponse` is `{ Key, Prefix, Message }` (the old richer
  envelope on the legacy `/account` route is gone).
- `client.Account.StatusAsync()` and `client.Account.RotateSecretAsync()`
  are gone — moved to `client.Auth.StatusAsync()` /
  `client.Auth.RotateSecretAsync()`.
- `EPostakException` constructor signature gained optional RFC 7807 +
  request-id + required-scope parameters. Callers that constructed errors
  manually for tests must update.

### Added

- **`client.Auth`** — OAuth `client_credentials` flow + key management:
  - `Auth.TokenAsync(apiKey, firmId?, scope?)` — mint short-lived JWT +
    rotating refresh token.
  - `Auth.RenewAsync(refreshToken)` — exchange refresh for a new pair.
  - `Auth.RevokeAsync(token, tokenTypeHint?)` — idempotent revocation.
  - `Auth.StatusAsync()` — key introspection (now `GET`).
  - `Auth.RotateSecretAsync()` — rotate the calling `sk_live_*` key.
  - `Auth.IpAllowlist.GetAsync()` / `Auth.IpAllowlist.UpdateAsync(cidrs)` —
    per-key IP allowlist (Wave 3.1).
- **`client.Audit`** — per-firm audit feed
  (`Audit.ListAsync(AuditListParams?)`), cursor-paginated over
  `(occurred_at DESC, id DESC)`. Returns the new generic `CursorPage<T>`.
- **`Idempotency-Key` support** on mutating endpoints. Pass
  `idempotencyKey` to `Documents.SendAsync`, `Documents.SendBatchAsync`,
  and `Webhooks.CreateAsync`. The server returns `409 idempotency_conflict`
  (surfaced as `EPostakException` with `Code = "idempotency_conflict"`)
  when the same key is replayed before the original request finishes.
- **`EPostak.WebhookSignature.Verify(...)`** — top-level helper that
  validates HMAC-SHA256 webhook deliveries. Parses the `t=...,v1=...`
  header, computes `HMAC(secret, "{t}.{rawBody}")`, and compares with
  `CryptographicOperations.FixedTimeEquals`. Multiple `v1=` values are
  accepted (rotation window). Default 300-second tolerance, configurable.
  Returns a `WebhookSignatureResult` with a `Reason` enum on failure.
- **`EPostakException`** now exposes:
  - RFC 7807 fields: `Type`, `Title`, `Detail`, `Instance`.
  - `RequestId` — captured from the body or the `X-Request-Id` header.
  - `RequiredScope` — parsed from
    `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`.
- **`SendDocumentResponse.PayloadSha256`** — hex SHA-256 over the canonical
  UBL XML wire payload, returned on `201` send responses.
- **`CursorPage<T>`** generic — used today by `Audit.ListAsync()` and by any
  future cursor-paginated route.
- **`AuditEvent`**, **`AuditActorType`**, **`AuditListParams`**,
  **`StatisticsTopParty`**, **`TokenResponse`**, **`RevokeResponse`**,
  **`IpAllowlistResponse`**, **`ReportingPeriod`** types.

### Changed

- Client docstring refers to "ePošťák API" (no longer "Enterprise API").
- `HttpRequestor` parses RFC 7807 problem-details bodies, forwards
  `X-Request-Id`, and reads `WWW-Authenticate` for `insufficient_scope`
  rejections — `EPostakException.RequestId` and `RequiredScope` are
  populated automatically.
- `DeriveValidateUrl` recognises both `/api/v1` and `/api/enterprise` so
  staging/custom deployments keep working when callers override the base.

### Removed

- `client.Account.StatusAsync()` and `client.Account.RotateSecretAsync()` —
  moved to `client.Auth.*`.
- `WebhooksResource.VerifyWebhookSignature(...)` — replaced by the
  top-level `EPostak.WebhookSignature.Verify(...)` helper, which uses
  HMAC-SHA256 (matching the server) and unix-second timestamps. The old
  helper was HMAC-SHA512 with millisecond timestamps and would have
  rejected real ePošťák deliveries.
- The implicit assumption that "Enterprise" is a separate tier — the SDK is
  now the single official .NET client.
