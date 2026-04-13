# Changelog

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
