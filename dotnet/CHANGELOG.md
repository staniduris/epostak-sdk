# Changelog

All notable changes to the `EPostak` .NET SDK are documented in this file. The
project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

- Added `client.Connector.MapperAsync(...)` and customer-scoped
  `client.Enterprise.Connector.Customers.For(customerRef).MapperAsync(...)`
  for the live `/connector/mapper` endpoint.

## 1.0.0 — 2026-06-14

### Added

- Added `client.Enterprise` as the documented namespace for Enterprise
  `/api/v1/*` workflows.
- Added `client.Sapi.Participants.For(id).Documents` for participant-scoped
  SAPI-SK document flows.
- Added `client.Enterprise.Connector.Customers.For(customerRef)` for
  managed-customer Connector calls that inject `CustomerRef` and omit
  `X-Firm-Id`.
- Added `client.Connector` for Connector preflight, send, outbox
  stage/list/detail/send/batch/cancel, status, inbox list/detail, ACK, and
  event polling.
- Added Connector v2 helpers: `ZenInputAsync(...)`, `AutopilotAsync(...)`,
  `GetAutopilotRunAsync(...)`, `SendAutopilotRunAsync(...)`,
  `ReconcileAsync(...)`, `MailboxesAsync()`, `RepairMailboxAsync(...)`,
  `UpdateMailboxSendPolicyAsync(...)`, `SyncAsync(...)`, Connector
  document/evidence helpers, and `RunActionAsync(...)`.
- Added `client.Documents.StatusBatchAsync(ids)` for `POST /documents/status/batch`.
- Added `client.Reporting.SubmissionsAsync(...)` for `GET /reporting/submissions`.
- Added `client.Integrator.Keys.ListAsync()` and `DeactivateAsync(...)` for
  the production `GET`/`DELETE /integrator/keys` surface.
- Extended SDK README endpoint coverage and environment data for production
  `https://epostak.sk` and test `https://dev.epostak.sk`.

### Changed

- Documented `https://dev.epostak.sk/api/v1` as the test environment
  `BaseUrl` override. Production remains the default.
- Clarified that OAuth test flows pass `origin: "https://dev.epostak.sk"`
  because OAuth bypasses `BaseUrl`.
- Updated README and migration guide for the workflow-first public API.

## 0.10.1 — 2026-05-22

### Fixed

- `OAuth.ExchangeCodeAsync(...)` now returns `OAuthTokenResponse` instead of
  `TokenResponse`. This matches the live `/api/oauth/token` contract, which
  issues a new `sk_int_*` `client_id` / `client_secret` plus firm metadata.
  The returned secret is not a bearer token; exchange it with
  `client.Auth.TokenAsync(...)` to mint a JWT for Enterprise API calls.

## 0.10.0 — 2026-05-18

### Added

- **`client.Sapi`** — SAPI-SK 1.0 document send, receive list/detail, and acknowledge.
- **Webhook queued tests** — `Webhooks.TestAsync(id, WebhookTestParams)` supports `Count` and `Mode`.
- **Webhook delivery filters** — `WebhookDeliveriesParams` adds `Cursor`, `TestRunId`, and `IncludeResponseBody`.
- **Webhook dead-letter queue** — `DeadLettersAsync()`, `ReplayDeadLetterAsync(id)`, and `ResolveDeadLetterAsync(id, reason)`.
- **Peppol participant resolve** — `Peppol.ResolveAsync(...)` for `/peppol/participants/resolve`.
- **Enterprise route gaps** — `Documents.EvidenceBundleAsync`, `Outbound.GetMdnAsync`, `Peppol.CompanySearchAsync`, `Documents.PeppolDocumentsAsync`, and `Account.LicenseInfoAsync`.

## [.NET 0.9.0] — 2026-05-12

### Added

- **Pull API — `client.Inbound`** (`InboundResource`): four methods covering
  the full Pull API receive flow for inbound documents:
  - `ListAsync(InboundListParams?)` — cursor-paginated list (`GET /inbound/documents`)
  - `GetAsync(string id)` — single document retrieval (`GET /inbound/documents/{id}`)
  - `GetUblAsync(string id)` — raw UBL 2.1 XML download (`GET /inbound/documents/{id}/ubl`)
  - `AckAsync(string id, InboundAckParams?)` — acknowledge receipt with optional
    `ClientReference` (`POST /inbound/documents/{id}/ack`, requires `documents:write` scope)
  - New POCOs: `InboundDocument`, `InboundListResponse`, `InboundListParams`, `InboundAckParams`.

- **Pull API — `client.Outbound`** (`OutboundResource`): three methods for outbound document inspection:
  - `ListAsync(OutboundListParams?)` — cursor-paginated list (`GET /outbound/documents`)
  - `GetAsync(string id)` — single document with `AttemptHistory` (`GET /outbound/documents/{id}`)
  - `GetUblAsync(string id)` — raw UBL 2.1 XML download (`GET /outbound/documents/{id}/ubl`)
  - `EventsAsync(OutboundEventsParams?)` — cursor stream of delivery events (`GET /outbound/events`)
  - New POCOs: `OutboundDocument`, `OutboundListResponse`, `OutboundListParams`,
    `OutboundEventsParams`, `OutboundEvent`, `OutboundEventsResponse`.

- **`UblValidationException`** — thrown automatically when the API returns HTTP 422
  with a code matching `UBL_*` (e.g. `UBL_VALIDATION_ERROR`, `UBL_EN16931_VIOLATION`).
  Exposes `Rule` (the code string) in addition to all base `EPostakException` fields.
  - **`UblRule`** static class with seven `const string` codes:
    `UblValidationError`, `SchemaInvalid`, `UnsupportedDocumentType`,
    `SupplierMismatch`, `En16931Violation`, `PeppolBisViolation`, `SkNationalViolation`.

- **`WebhookTestParams`** — typed parameter object for `Webhooks.TestAsync`:
  ```csharp
  await client.Webhooks.TestAsync(id, new WebhookTestParams { Event = WebhookEvent.DocumentDelivered });
  ```
  The event is sent as a `?event=` query parameter (server precedence over body).
  The existing `TestAsync(id, string? webhookEvent)` overload still works.

- **`WebhookEvent` enum** — strongly-typed webhook event values used by
  `WebhookTestParams.Event` and convertible to wire strings via the new
  overload of `TestAsync`.

- **`WebhookDelivery.IdempotencyKey`** (`string?`) — nullable property added
  to the `WebhookDelivery` POCO. Populated when the triggering API call
  supplied an `Idempotency-Key` header.

- **`client.LastRateLimit`** (`RateLimitInfo?`) — exposes the most recent
  `X-RateLimit-Limit` / `X-RateLimit-Remaining` / `X-RateLimit-Reset` headers
  captured from any API response. Returns `null` until the first rate-limited
  response is observed.
  - New type: `RateLimitInfo { int Limit, int Remaining, DateTimeOffset ResetAt }`.

## 0.8.1 — 2026-05-06

Bug-fix release. Four breaking-on-paper but pre-launch-clean fixes for webhook
handling and documentation.

### Fixed

- **`WebhookSignature.Verify()` rewritten.** Was parsing a single Stripe-style
  `X-Epostak-Signature: t=…,v1=…` header. The server has always sent two
  separate headers (`X-Webhook-Signature: sha256=<hex>` and
  `X-Webhook-Timestamp: <unix_seconds>`) signed with HMAC-SHA256 over
  `${timestamp}.${body}`. Every previous integration was rejecting valid
  webhooks. New signature:
  ```csharp
  WebhookSignature.Verify(
      payload: requestBodyBytes,
      signature: request.Headers["X-Webhook-Signature"],  // "sha256=<hex>"
      timestamp: request.Headers["X-Webhook-Timestamp"],  // unix seconds
      secret: Environment.GetEnvironmentVariable("EPOSTAK_WEBHOOK_SECRET")!);
  ```
  The method still returns `WebhookSignatureResult` with a `Reason` enum and
  never throws on bad signatures.
- **`WebhookQueueResponse` / `WebhookQueueAllResponse` shape.** Properties
  were declared as `Events` (`[JsonPropertyName("events")]`) and `Count`
  (`[JsonPropertyName("count")]`). Server has always returned `items` and
  `has_more`. Renamed to `Items` and `HasMore` (bool) with correct
  `[JsonPropertyName]` attributes.
- **README examples** used `new EPostakConfig { ApiKey = ... }`. `ApiKey` is
  not a property — `EPostakConfig` requires `ClientId` + `ClientSecret`. All
  four Quick Start / Configuration examples corrected.

### Removed

- **`Documents.ReceiveCallbackAsync()`** deleted. The underlying endpoint
  `POST /sapi/v1/document/receive-callback` was removed server-side on
  2026-05-05 — it duplicated `POST /api/v1/webhooks` under a misleading name.
  Types `ReceiveCallbackRequest` and `ReceiveCallbackResponse` are also gone.
  Use `client.Webhooks.CreateAsync(new CreateWebhookRequest { Url, Events })`
  instead, which is the canonical webhook subscription path.

### Migration

```diff
- await client.Documents.ReceiveCallbackAsync(new ReceiveCallbackRequest { Url = url });
+ await client.Webhooks.CreateAsync(new CreateWebhookRequest { Url = url });

  WebhookSignature.Verify(
      payload: bodyBytes,
-     signatureHeader: request.Headers["X-Epostak-Signature"],
+     signature: request.Headers["X-Webhook-Signature"],
+     timestamp: request.Headers["X-Webhook-Timestamp"],
      secret: secret);

- foreach (var item in queue.Events)
+ foreach (var item in queue.Items)

- new EPostakConfig { ApiKey = "sk_live_..." }
+ new EPostakConfig { ClientId = "sk_live_...", ClientSecret = "sk_live_..." }
```

## 3.1.0 — 2026-04-30

### Added

- **`client.Documents.ReceiveCallbackAsync(request)`** — wraps
  `POST /sapi/v1/document/receive-callback`. Registers a webhook endpoint
  for inbound document delivery notifications. The response includes a
  one-time HMAC-SHA256 signing secret. Requires `webhooks:write` scope.
  Convenience overload `ReceiveCallbackAsync(url)` subscribes to the
  default `document.received` event.
- New types: `ReceiveCallbackRequest`, `ReceiveCallbackResponse`.
- **`client.Auth.TokenStatusAsync()`** — wraps
  `GET /auth/token/status`. Introspects the calling JWT access token and
  returns `FirmId`, `KeyType`, `Scope`, expiry timing, and refresh
  recommendation. Also available at the SAPI alias `/sapi/v1/auth/status`.
- New type: `TokenStatusResponse` with properties `Valid`, `TokenType`,
  `ClientId`, `FirmId`, `KeyType`, `Scope`, `IssuedAt`, `ExpiresAt`,
  `ExpiresInSeconds`, `ShouldRefresh`, `RefreshRecommendedAt`.

### Changed

- Four document endpoints now require the `documents:read` scope:
  `Documents.ValidateAsync(...)`, `Documents.ParseAsync(...)`,
  `Documents.ConvertAsync(...)`, and `Peppol.Directory.SearchAsync(...)`.
  This is a server-side enforcement change — no SDK code changes are
  needed, but callers must ensure their API key or OAuth token includes
  the `documents:read` scope. Requests without it receive HTTP 403 with
  `RequiredScope = "documents:read"`.

## 2.2.0 — 2026-04-29

### Added

- **`client.Integrator.Licenses.InfoAsync(offset?, limit?)`** — wraps
  `GET /api/v1/integrator/licenses/info`. Returns aggregate plan +
  current-period usage across every firm an integrator manages. Tier rates
  are applied to the AGGREGATE counts (not per-firm summed), so a 100-firm ×
  50-doc integrator lands in tier 2–3 instead of tier 1.
- Response surfaces `Billable` (firms on the `integrator-managed` plan that
  the integrator pays for), `NonManaged` (linked firms paying their own
  plan), `ExceedsAutoTier` (`true` above 5 000 / month — auto-billing pauses,
  sales handles manually), `ContactThreshold`, `Pricing.OutboundTiers` /
  `Pricing.InboundApiTiers`, and a paginated per-firm breakdown.
- Requires the `account:read` scope on a `sk_int_*` integrator key. No
  `X-Firm-Id` header — the endpoint is integrator-scoped, not firm-scoped.
- New types: `IntegratorLicenseInfo`, `IntegratorLicenseInfoIntegrator`,
  `IntegratorBillableUsage`, `IntegratorNonManagedUsage`,
  `IntegratorPricing`, `IntegratorPricingTier`, `IntegratorFirmUsage`,
  `IntegratorLicensePagination`. New resources:
  `EPostak.Resources.IntegratorResource`,
  `EPostak.Resources.IntegratorLicensesResource`. New top-level accessor
  `EPostakClient.Integrator`.

## 2.1.0 — 2026-04-29

### Added

- **`EPostak.Resources.OAuth`** — static helpers for the integrator-initiated
  onboarding flow (`authorization_code` grant + PKCE S256). Use these from
  your backend to let an end-user firm consent to your integrator app from
  inside your own UI. All members are `static` — no client instance required:
  - `OAuth.GeneratePkce()` — fresh `(CodeVerifier, CodeChallenge)` value
    tuple.
  - `OAuth.BuildAuthorizeUrl(clientId, redirectUri, codeChallenge, state, scope?, origin?)`
    — authorize URL the user is redirected to.
  - `OAuth.ExchangeCodeAsync(code, codeVerifier, clientId, clientSecret, redirectUri, origin?, httpClient?, ct?)`
    — exchanges the returned `code` for an `OAuthTokenResponse` against
    `${origin}/api/oauth/token`. Hits the OAuth namespace directly,
    bypassing `EPostakConfig.BaseUrl`.

  Use this when the firm has no API key with you yet. After
  `ExchangeCodeAsync` succeeds, store the returned `sk_int_*` client secret
  and firm metadata server-side. Use the returned `client_id` /
  `client_secret` with `client.Auth.TokenAsync(...)` to mint short-lived JWTs
  for Enterprise API calls.

- Required `redirect_uris` must be registered with ePošťák
  (`info@epostak.sk`) before first use — exact-match enforced, no wildcards.

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
