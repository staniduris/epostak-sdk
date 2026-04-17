# Changelog

## 1.3.0 — 2026-04-17

### New endpoint

- **`webhooks.rotateSecret(id)`** — issues a new HMAC-SHA256 signing secret for a webhook and immediately invalidates the previous one. Returns the new `secret` only once. Use when a leaked secret needs rotation without losing delivery history.

### API behavior changes (server-side, now reflected in JSDoc)

- **`documents.send()`**: `vatRate` is now whitelisted against Slovak legal rates `{0, 5, 10, 19, 23}`. Any other value returns `422 VALIDATION_ERROR`. Previous accepting range of 0–100 was incorrect per Slovak VAT law.
- **`documents.inbox.acknowledge(id)`**: requires source state `received`. Calling on any other state returns `400`.
- **`documents.status(id)`**: `acknowledgedAt` field now returns the real timestamp instead of always being `null`.
- **`webhooks.deliveries(id, params)`**: the `status` query param is now whitelisted server-side against `PENDING | SUCCESS | FAILED | RETRYING`; invalid values are ignored rather than returning 500.
- **`/oauth/token` and `/auth/token`**: now rate-limited at 10 req/min per `client_id`, fail-closed on Redis outage.
- **Body size limits**: auth tokens 16 KB, documents 6 MB, webhook create 64 KB. Oversized requests return `413 PAYLOAD_TOO_LARGE`.
- **`firms.assign(ico)`**: integrators can now only claim firms on plans `free` or `integrator-managed`. Firms on any other paid plan require the OAuth `authorization_code` flow for explicit consent. Returns `403 FORBIDDEN` otherwise.

### Documentation

- **Peppol ID format**: consolidated to `0245:DIČ` only per Slovak PASR requirements. Removed `9950:SKXXXXXXXXXX` references from TypeScript README, root README, and Ruby SDK — the `9950` scheme is not supported for Slovak participants.
- `vatRate` JSDoc on `LineItemBase` now lists the allowed values.

### Languages

- TypeScript: new `rotateSecret()` method, updated `LineItemBase.vatRate` JSDoc, new `WebhookRotateSecretResponse` type.
- All SDKs: README updates for Peppol ID format and vatRate behavior.

---

## 1.2.0 — 2026-04-14

### Breaking behavior change

- **`documents.send()` now throws 422 `VALIDATION_FAILED`** when a document fails Peppol BIS 3.0 Schematron validation. Previously, validation failures were silently queued. `err.details` contains the list of validation errors. Use `documents.validate()` to pre-check.
- **`documents.send()` returns 502 `SEND_FAILED`** on Peppol network transport failures (retryable). Queue insertion failures no longer return a false `202 QUEUED`.

### Documentation

- Added `@throws` JSDoc to `documents.send()` for 422 and 502 error codes
- Updated error handling examples with `VALIDATION_FAILED` / `SEND_FAILED` codes
- Added error code table to README

### Languages

All documentation changes applied to: TypeScript

---

## 1.1.0 — 2026-04-11

### New endpoints

- `webhooks.test(id, event?)` — send test event to webhook URL, get delivery result
- `webhooks.deliveries(id, params?)` — paginated delivery history with status/event filters

### Improvements

- **Retry/backoff** added to all 6 SDKs — exponential backoff with jitter on 429/5xx, max 3 retries (configurable via `maxRetries`), respects `Retry-After` header

### Languages

All changes applied to: TypeScript, Python, PHP, C#, Java, Ruby

---

## 1.0.0 — 2026-04-11

### Initial release

Official SDKs for the ePošťák Enterprise API — Peppol e-invoicing for Slovakia and the EU.

**37 API endpoints covered:**

- **Documents** — send, get, update, status, evidence, pdf, ubl, respond, validate, preflight, convert
- **Inbox** — list, get, acknowledge, listAll (integrator cross-firm)
- **Peppol** — SMP lookup, directory search, company lookup by ICO
- **Firms** — list, get, documents, registerPeppolId, assign, assignBatch (integrator)
- **Webhooks** — create, list, get, update, delete, test, deliveries
- **Webhook Queue** — pull, ack, batchAck, pullAll, batchAckAll (integrator)
- **Reporting** — statistics
- **Account** — get
- **Extract** — single (AI OCR), batch

**6 languages:** TypeScript (npm), Python, PHP, C#, Java, Ruby

**Peppol ID formats:** `0245:DIČ`, `9950:SK+VAT`

**TypeScript published on npm:** `@epostak/sdk@1.0.0`
