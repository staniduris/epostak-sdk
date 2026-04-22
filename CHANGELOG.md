# Changelog

## TypeScript 1.5.0 — 2026-04-22

### New method: `documents.envelope(id)`

- **`client.documents.envelope(id): Promise<Buffer>`** — mirror of `pdf()` for the `GET /api/enterprise/documents/{id}/envelope` endpoint (shipped in backend 1.4.1). Returns the raw signed AS4 envelope bytes straight from the 10-year WORM archive (S3 Object Lock COMPLIANCE). Every document that ever flowed through our Access Point is retrievable for 10 years during an active `api-enterprise` contract. Brand-new documents may briefly 404 until the archival cron persists the envelope.

  ```typescript
  import { writeFileSync } from "fs";

  const envelope = await client.documents.envelope("doc-uuid");
  writeFileSync("doc-uuid.as4", envelope);
  ```

  Server also returns `X-Envelope-Archived-At` + `X-Envelope-Direction` response headers plus `Content-Disposition: attachment; filename="{id}.as4"` (accessible via a low-level `rawResponse` request if you need them — the `envelope()` helper discards them).

### Type corrections (no runtime change — tsc strictness)

Full parity pass against the live backend surfaced long-standing drift between the SDK's types and the real JSON on the wire. JavaScript callers that destructured by name keep working against the runtime shape; strict-TypeScript callers will need small edits where fields were renamed or re-typed. Highlights:

- **`InvoiceResponseCode`** widened from 3 (`AP/RE/UQ`) to the full BIS 3.0 set of 7: `AB, IP, UQ, CA, RE, AP, PD`. `documents.respond()` docs now describe the 500-char `note` cap and the state rule (only `UQ` allows one follow-up; `AP/RE/PD` are terminal).
- **`InvoiceRespondResponse`** gained `peppolMessageId`, `dispatchStatus` (`"sent" | "failed_queued"`), optional `dispatchError`. The endpoint returns 202 (not 200) when AS4 dispatch fails and the response is queued for retry.
- **`documents.validate()`** request is now `{format, document}` and the response carries `{valid, errorCount, warningCount, errors, warnings}` with per-finding `ruleId`/`severity`/`location` (old `{valid, warnings, ubl}` shape never existed).
- **`documents.preflight()`** request adds `invoice?` / `xml?` for inline validation; response replaced with `{canSend, recipientFound, recipientAcceptsDocumentType, validationPassed, validationErrors, warnings}` — the two middle booleans are tri-state (`true | false | null`).
- **`documents.parse()`** response shape `{document, warnings}` → `{invoice, extras, allowances}`.
- **`BatchSendResult.status`** retyped as `number` (HTTP status code 201 / 4xx / 5xx) — the server always wrote the integer, the old `"success" | "error"` string typing was a lie.
- **`documents.send()`** accepts optional `{idempotencyKey}` argument that is forwarded as the `X-Idempotency-Key` header.
- **`DocumentEvidenceResponse`** — `invoiceResponse.status` is a non-null `InvoiceResponseCode` on hit (or the whole object is `null`); new optional `tdd: {reportedAt, reported}` for SK Tax Data Document reporting state.
- **`DocumentStatusResponse.validationResult`** typed as `{errors: string[]} | null` (was `Record<string, unknown> | null`).
- **Inbox `status` filter** accepts lowercase `"received"` / `"acknowledged"`; `AcknowledgeResponse.status` stays uppercase (`"ACKNOWLEDGED"`).

Webhooks:

- **`WebhookEvent`** expanded from 4 to the canonical 7: added `document.delivered`, `document.rejected`, `document.response_received`.
- **`Webhook`** gained `failedAttempts`. New exported `WebhookDeliveryStatus` alias (`"PENDING" | "SUCCESS" | "FAILED" | "RETRYING"`) — the old lowercase-string typing of `WebhookDelivery.status` was wrong.
- **`webhooks.create()`** accepts optional `isActive` in the body.
- **`webhooks.test()`** docs confirm `responseTime` (round-trip ms), not `latencyMs`.

Peppol:

- **`peppol.capabilities()`** request reshaped to `{participant: {scheme, identifier}, documentType?, processId?}`; response adds `participant`, `accessPoint`, `internal`, `source`, `reason`, keeps `matchedDocumentType` as nullable string.
- **`peppol.lookupBatch()`** per-result: `{index, participant: {scheme, identifier, id}, found, accessPoint?, internal?, supportedDocumentTypes?, source?, error?}`. `PeppolParticipant` itself now matches the SMP-lookup shape (`found, internal?, accessPoint?, supportedDocumentTypes?, source?`) — the old `{peppolId, name, country, capabilities[]}` shape was invented SDK-side.
- **`peppol.lookup()`** returns `PeppolParticipant | null` — a 404 from the backend now resolves to `null` instead of throwing, matching ergonomic expectations.
- **`peppol.directory.search()`** — `q` is required (min 2 chars). Response `{items, page, page_size, has_next}` (was `{results, total, page, page_size}` — backend never returned a `total`, the directory has ~3.6M rows).

Firms:

- **`FirmDetail`** flattened — no longer extends `FirmSummary`. Now carries `name: string` (not `name?: string`), `plan`, `address: {street, city, zip}`, and `createdAt` from a single backend `select`.
- **`firms.documents(id, params)`** — pagination migrated from `offset`/`limit` to `page`/`page_size`; adds `status`, `from`, `to` filters.
- **`firms.registerPeppolId()`** response `{peppolId, scheme, identifier, registeredAt}` → `{peppolId, registrationStatus, message}` (async SMP registration — no server `registeredAt` until the cron completes).

Account & auth:

- **`account.status()`** now hits `GET /auth/status` (backend has always served GET — the previous SDK issued POST and this would have 405'd on first call). Full response reshaped: `key.{id, name, prefix, permissions, active, createdAt, lastUsedAt}`, `firm.{id, peppolStatus}`, `plan.{name, expiresAt, active}`, `rateLimit.{perMinute: 200, window: "60s"}`, `integrator: {id} | null`.
- **`account.rotateSecret()`** response `{keyId, key, prefix, rotatedAt}` → `{key, prefix, message}`. Integrator-key rejection is 403 (was documented as 409).
- **`Account`** gained `usage.ocr_extractions` and `limits.{documents_per_month, ocr_per_month}` (`-1` = unlimited).

Reporting:

- **`reporting.statistics()`** query params gain `period` (`"month" | "quarter" | "year"`). Response reshaped to `{period, sent: {total, by_type}, received: {total, by_type}, delivery_rate, top_recipients[], top_senders[]}`. New exported `StatisticsParty` / `ReportingPeriod` types.

Extract:

- **`ExtractResult`** now surfaces `confidence_scores` (per-field, 0–1) and `needs_review` (`true` for low/medium confidence). `confidence` typed as the coarse bucket `"high" | "medium" | "low"` (was `number`).
- **`BatchExtractResult`** reduced to `{results[]}` — the `batch_id` / `total` / `successful` / `failed` fields never existed on the wire.

### SDK versions

| Language                              | Version |
|-|-|
| TypeScript (`@epostak/sdk`)           | 1.5.0   |

No other SDKs changed in this release.

---

## Python 0.3.0 — 2026-04-22

### New method: `documents.envelope(id)`

- **`client.documents.envelope(id) -> bytes`** — follow-up bump for the 1.4.1 `GET /documents/{id}/envelope` endpoint. Mirrors `pdf()` and returns the raw signed AS4 envelope bytes straight from the 10-year WORM archive (S3 Object Lock COMPLIANCE). Every document that ever flowed through our Access Point is retrievable for 10 years during an active `api-enterprise` contract. Brand-new documents may briefly 404 until the archival cron persists the envelope.

  ```python
  envelope = client.documents.envelope("doc-uuid")
  with open("doc-uuid.as4", "wb") as f:
      f.write(envelope)
  ```

  Server also returns `X-Envelope-Archived-At` + `X-Envelope-Direction` response headers plus `Content-Disposition: attachment; filename="{id}.as4"`.

### TypedDict corrections (no runtime change)

Audit against the live backend surfaced a handful of drifted `TypedDict` fields. Callers who read dict keys by string keep working; only static type hints improve.

- **`InvoiceResponseCode`** expanded from 3 values (`AP/RE/UQ`) to the full BIS 3.0 Invoice Response set: `AB, IP, UQ, CA, RE, AP, PD`. `documents.respond()` docstring now lists all seven and documents the 500-char `note` limit.
- **`InvoiceRespondResponse`** gained `peppolMessageId`, `dispatchStatus` (`"sent"` | `"failed_queued"`), and optional `dispatchError` — the endpoint returns 202 on AS4 transport failure and the response persists for retry.
- **`DocumentEvidenceResponse`** gained optional `tdd: {reportedAt, reported}` for SK Tax Data Document reporting evidence.
- **`DirectorySearchResult`** rewritten to match `/peppol/directory/search` on the wire: `items` (not `results`), `has_next` (not `total`). `DirectoryEntry` fields renamed to `participantId`, `countryCode`, `registrationDate`. The backend never returned a `total` — the underlying directory has ~3.6M rows and counting hits would blow the budget.
- **`AccountStatus`** restructured to match `POST /auth/status`: `key.{id,name,prefix,permissions,active,createdAt,lastUsedAt}`, `firm.{id,peppolStatus}`, `plan.{name,expiresAt,active}`, `rateLimit.{perMinute,window}`, `integrator: {id} | None`. Previous shape described fields that never existed on the wire (`keyId`, `firm.name`, `firm.ico`, `getPerMin`/`postPerMin`).
- **`RotateSecretResponse`** corrected to `{key, prefix, message}`. Integrator-key rejection is `403 FORBIDDEN`, not `409`. Dropped the stray empty `json={}` body in `account.rotate_secret()` — the endpoint reads no body.
- **`Account`** gained `limits.{documents_per_month, ocr_per_month}` and `usage.ocr_extractions`, both previously missing from the TypedDict despite being in the wire response.
- **`BatchSendResultItem.status`** retyped as `int` (HTTP status code 201 / 4xx / 5xx). It was annotated as `"success"`/`"error"` strings but the server always wrote the HTTP integer.
- **`WebhookEvent`** expanded from 4 to the canonical 7: added `document.delivered`, `document.rejected`, `document.response_received`.
- **`documents.send_batch()`** docstring: max 50 items per request (was "up to 100"), max body 20 MB.

### SDK versions

| Language                              | Version |
|-|-|
| Python (`epostak`)                    | 0.3.0   |

No other SDKs changed in this release.

---

## .NET 1.5.0 — 2026-04-22

### New method: `Documents.EnvelopeAsync(id)`

- **`client.Documents.EnvelopeAsync(id)`** — streams the signed AS4 envelope for a document from the 10-year WORM archive (S3 Object Lock COMPLIANCE mode). Returns `Task<byte[]>`, mirroring `PdfAsync()`. Available on api-enterprise plan during an active contract; every document that ever flowed through the AP is retrievable for 10 years. Returns `404` while the archive cron catches up on brand-new documents.

  ```csharp
  byte[] envelope = await client.Documents.EnvelopeAsync("doc_abc123");
  await File.WriteAllBytesAsync($"{id}.as4", envelope);
  ```

### Ground-truth corrections

- **`InvoiceResponseCode` enum expanded from 3 → 7 values.** Spec: `AB` (accepted billing), `IP` (in process), `UQ` (under query), `CA` (conditionally accepted), `RE` (rejected), `AP` (accepted), `PD` (paid). Previous versions only exposed `AP`/`RE`/`UQ` — any of the other four codes returned by `GET /documents/{id}/status` would have deserialized as an enum parse error.
- **`WebhookEvents` constants extended** with the three previously-missing events: `document.delivered`, `document.rejected`, `document.response_received`. The server has always accepted all seven — the constants class just lagged.
- **New `WebhookDeliveryStatus` constants class** with `PENDING`/`SUCCESS`/`FAILED`/`RETRYING` so callers don't hand-write string literals. All delivery-status JSON docstrings now reflect the UPPERCASE wire values.
- **`AuthStatusResponse` shape fixed** to match `/auth/status` exactly: `key:{id,name,prefix,permissions,active,createdAt,lastUsedAt}`, `firm:{id,peppolStatus}` (no name/ico/peppolId), `plan:{name,expiresAt,active}` (was a bare string), `rateLimit:{perMinute,window}` (was `limit/remaining/resetAt`), `integrator:null|{id}` (was `{firmsManaged}`).
- **`Account.StatusAsync()` switched from `POST` to `GET`** — backend implements `GET /auth/status`; the SDK was issuing a POST with an empty body.
- **`RotateSecretResponse` shape fixed** to `{key, prefix, message}` — removed non-existent `keyId`/`rotatedAt`. 403 on integrator keys (was incorrectly documented as 409).
- **`Account` model extended** with `Usage.OcrExtractions` and the top-level `Limits:{documents_per_month, ocr_per_month}` block returned by `GET /account`.
- **`ExtractResult.Confidence` retyped `double → string`** (wire value is `"high"|"medium"|"low"`, not a 0–1 float). Added `ConfidenceScores` (per-field 0–1 map) and `NeedsReview` (bool) — both are always present in the response.
- **`InvoiceRespondResponse` extended** with `PeppolMessageId`, `DispatchStatus`, and `DispatchError` — the server returns these on 200/202 so integrators can distinguish `sent` from `failed_queued` without a second status call.

### Version

- **.NET (`EPostak`) 1.1.0 → 1.5.0.** Catches up with the TypeScript 1.4.0 line and aligns with the PHP 1.2.0 / envelope release cycle.

---

## Java 1.2.0 — 2026-04-22

### New method: `documents().envelope(id)`

- **`client.documents().envelope(id)`** — follow-up bump for the 1.4.1 `GET /documents/{id}/envelope` endpoint. Mirrors `pdf()` and returns raw signed AS4 envelope bytes (`byte[]`) from the 10-year WORM archive (S3 Object Lock COMPLIANCE). Use `Files.write(Path.of("doc.as4"), envelope)` to persist. Same `404 NOT_FOUND` semantic as the other SDKs while the archive cron catches up, same `403 FORBIDDEN` for non-enterprise plans.

  ```java
  byte[] envelope = client.documents().envelope("doc-uuid");
  Files.write(Path.of("doc-uuid.as4"), envelope);
  ```

### Model and method fixes against real backend

A full parity pass against the live Next.js route handlers surfaced long-standing drift in the Java SDK. JSON field names, HTTP methods, and response shapes now match what the server actually emits. Callers that consumed models by getter/accessor should recheck field renames; snake_case `@SerializedName` annotations were removed everywhere the server returns camelCase.

- **`account().status()`** is now `GET /auth/status` (was `POST`). `AuthStatusResponse` fully rewritten: `key.{id,name,prefix,permissions,active,createdAt,lastUsedAt}`, `firm.{id,peppolStatus}`, `plan.{name,expiresAt,active}`, `rateLimit.{perMinute,window}`, `integrator?.{id}`. Previous record described fields that never existed on the wire (`keyId`, `firm.name/ico/peppolId`, `rateLimit.limit/remaining/resetAt`).
- **`account().rotateSecret()`** — `RotateSecretResponse` rewritten to `{key, prefix, message}` (was `{keyId, key, prefix, rotatedAt}`). Integrator-key rejection corrected to `403 FORBIDDEN` (was documented as `409`).
- **`account().get()`** — `Account` gained the `limits` block (`{documentsPerMonth, ocrPerMonth}`) and the `usage.ocrExtractions` counter; `plan.status` replaced the old free-form plan record.
- **`documents().respond()`** — `status` widened from 3 (`AP/RE/UQ`) to the full BIS 3.0 set of 7: `AB, IP, UQ, CA, RE, AP, PD`. `InvoiceRespondResponse` switched to camelCase (`documentId`, `responseStatus`, `respondedAt`) and gained `peppolMessageId`, `dispatchStatus` (`"sent" | "failed_queued"`), and optional `dispatchError`. `note` documented as max 500 chars.
- **`documents().send()`** — `SendDocumentResponse` switched from snake_case (`document_id`, `message_id`) to camelCase (`documentId`, `messageId`) to match the 201 body.
- **`documents().sendBatch()`** — `BatchSendRequest.BatchItem.idempotencyKey` no longer serialized as `idempotency_key`; javadoc corrected from "max 100" to "max 50, 20 MB total".
- **`documents().preflight()`** — body now sends `receiverPeppolId` / `documentType` (was `receiver_peppol_id` / `document_type_id`). `PreflightResult` rewritten to the real shape `{canSend, recipientFound, recipientAcceptsDocumentType, validationPassed, validationErrors, warnings}` with tri-state booleans (`Boolean` nullable, not `boolean`).
- **`documents().validate()`** — overload split into `validate(String ublXml)` and `validateJson(Object jsonDoc)`, both sending the correct `{format, document}` body. `ValidationResult` rewritten to `{valid, errorCount, warningCount, errors, warnings}` with per-finding `{rule, message, location, severity}`.
- **`documents().inbox().acknowledge()`** — `AcknowledgeResponse` switched to camelCase (`documentId`, `acknowledgedAt`).
- **`documents().mark()`** — `MarkResponse` switched to camelCase (`deliveredAt`, `acknowledgedAt`, `readAt`).
- **`documents().status(id)`** — `DocumentStatusResponse` switched to camelCase throughout (`documentType`, `senderPeppolId`, `receiverPeppolId`, `statusHistory`, `validationResult`, `deliveredAt`, `acknowledgedAt`, `invoiceResponseStatus`, `as4MessageId`, `createdAt`, `updatedAt`).
- **`documents().evidence(id)`** — `DocumentEvidenceResponse` switched to camelCase; new `tdd` block (`{reportedAt, reported}`) for Slovak Tax Data Document reporting state.
- **`documents().get(id)` / inbox / firm-documents** — `Document`, `Party`, `LineItemResponse`, and `DocumentTotals` switched to camelCase wire names (`docType`, `issueDate`, `dueDate`, `icDph`, `peppolId`, `unitPrice`, `vatRate`, `vatCategory`, `lineTotal`, `withoutVat`, `withVat`, `peppolMessageId`, `createdAt`, `updatedAt`) to match `formatInvoice`.
- **`extract().single()`** — `ExtractResult.confidence` retyped from `double` to `String` (`"high" | "medium" | "low"`); added `confidenceScores` (`Map<String, Double>`) and `needsReview` (`boolean`).
- **`peppol().lookup()`** — `PeppolParticipant` rewritten to the real SMP lookup shape: `{found, participantId, scheme, accessPoint:{url, transportProfile}, supportedDocumentTypes, source, internal}`. The old `name/country/capabilities` record was a fabrication.
- **`peppol().capabilities()`** — `CapabilitiesRequest.documentType` now serialized camelCase. `CapabilitiesResponse` rewritten to `{found, accepts(boolean), participant, accessPoint, internal, supportedDocumentTypes, matchedDocumentType, source, reason}`.
- **`peppol().lookupBatch()`** — `BatchLookupResponse.LookupResult` rewritten to the real per-item shape (`index, participant:{scheme,identifier,id}, found, accessPoint, internal, supportedDocumentTypes, source, error`).
- **`peppol().directory().search()`** — `DirectorySearchResult` rewritten to `{items, page, page_size, has_next}`; entries to `{participantId, name, countryCode, registrationDate}`.
- **`peppol().companyLookup()`** — `CompanyLookup` now matches the real payload (`legalForm`, `peppolRegistered`, camelCase `icDph`/`peppolId`).
- **`firms().list()` / `get()`** — `FirmSummary` / `FirmDetail` switched to camelCase (`peppolId`, `peppolStatus`, `icDph`). `FirmDetail` gained the `plan` field.
- **`firms().registerPeppolId()`** — `PeppolIdentifierResponse` rewritten to the real 201 shape `{peppolId, registrationStatus, message}`.
- **`firms().assign()`** — docstrings corrected: assignment status is `"active"` / `"already_assigned"`, not `"assigned"`. `AssignFirmResponse.AssignedFirm` keeps `@SerializedName("peppol_id")` / `("peppol_status")` because this specific endpoint does emit snake_case (verified against the route handler).
- **`reporting().statistics()`** — `Statistics` rewritten to `{period, sent:{total, by_type}, received:{total, by_type}, deliveryRate, topRecipients, topSenders}`. The previous `OutboundStats`/`InboundStats` records never matched the server.
- **`webhooks().update()`** — PATCH body sends `isActive` (was `is_active`) to match the handler.
- **`webhooks().list()` / `get()`** — `Webhook` / `WebhookDetail` switched to camelCase (`isActive`, `createdAt`, `failedAttempts`). Delivery status documented as UPPERCASE (`PENDING | SUCCESS | FAILED | RETRYING`).
- **`webhooks().deliveries()`** — `WebhookDeliveriesResponse.DeliveryDetail` switched to camelCase; status documented as UPPERCASE.
- **`webhooks().test()`** — `WebhookTestResponse.responseTime` typed as `long` ms (was `double`) to match the integer the server returns.

### SDK versions

| Language                              | Version |
| ------------------------------------- | ------- |
| Java (Maven `sk.epostak:epostak-sdk`) | 1.2.0   |

No other SDKs changed in this release.

---

## PHP 1.2.0 — 2026-04-22

### New method: `documents->envelope($id)`

- **`$client->documents->envelope($id)`** — follow-up bump for the 1.4.1 `GET /documents/{id}/envelope` endpoint. Mirrors `pdf()` / `ubl()` and returns the raw signed AS4 envelope bytes as a PHP string. Pipe straight to `file_put_contents('/tmp/doc.as4', $envelope)`. Same `404 NOT_FOUND` semantic as the other SDKs while the archive cron catches up, same `403 FORBIDDEN` for non-enterprise plans.

### Docblock corrections (no behaviour change)

- **`documents->respond()`** — status parameter docblock now lists the full BIS 3.0 Invoice Response set (`AB`, `IP`, `UQ`, `CA`, `RE`, `AP`, `PD`). The previous docblock advertised only `AP` / `RE` / `UQ`, which matched the TypeScript 1.0 surface but not the current server contract. Note max length documented as 500 characters.
- **`account->rotateSecret()`** — integrator-key rejection is `403 FORBIDDEN`, not `409` (the server throws `ForbiddenError`). Response shape corrected to `{key, prefix, message}` to match `/auth/rotate-secret`.
- **`account->status()`** — `@return` shape rewritten to match the real `/auth/status` payload: `key.{id,name,prefix,permissions,active,createdAt,lastUsedAt}`, `firm.{id,peppolStatus}`, `plan.{name,expiresAt,active}`, `rateLimit.{perMinute,window}`, `integrator?.{id}`. Previous block described fields that never existed on the wire (`keyId`, `firm.name`, `firm.ico`, `getPerMin`/`postPerMin`).
- **`webhooks->queue->batchAck()` / `batchAckAll()`** — documented the server-side 1000-UUID per-call limit.
- **`webhooks->queue->pullAll()`** — return shape documented as `{events, count}` with default `limit=100` / max `500`.

### SDK versions

| Language                              | Version |
| ------------------------------------- | ------- |
| PHP (`epostak/sdk`)                   | 1.2.0   |

No other SDKs changed in this release.

---

## 1.4.1 — 2026-04-22

### New endpoint: signed AS4 envelope retrieval

- **`documents.envelope(id)`** — downloads the signed, timestamped AS4 envelope for a document from the 10-year WORM archive (S3 Object Lock COMPLIANCE). Returns the raw multipart AS4 bytes exactly as they were transmitted on the Peppol network — tamper-evident, usable as dispute evidence or for regulatory retention. Server responds with `Content-Disposition: attachment; filename="{id}.as4"` and custom headers `X-Envelope-Archived-At` + `X-Envelope-Direction`.
- **Availability:** `api-enterprise` plan only. Returns `403 FORBIDDEN` on other plans.
- **Edge case:** returns `404` briefly for very recently sent documents until the archival cron picks them up; clients should retry after a short delay rather than treat the first 404 as permanent.

### Fixes

- **Ruby:** `webhooks.deliveries(id, **params)` passed its filters under the wrong keyword (`params:` instead of `query:`) so `status`, `event`, `limit`, `offset` were silently dropped on the wire. Now forwarded as real query-string params.
- **Ruby:** `webhooks.create()` docstring listed an outdated event set (referenced the legacy `document.status_changed` and was missing `document.created`, `document.delivered`, `document.rejected`). Synced to the canonical 7-event list the server enforces.

### SDK versions

| Language                              | Version |
| ------------------------------------- | ------- |
| Ruby (`epostak` gem)                  | 1.2.0   |

No other SDKs changed in this release — TypeScript, Python, Java, PHP and .NET will add `documents.envelope()` in parallel follow-up bumps.

---

## 1.4.0 — 2026-04-22

### New feature: invoice attachments (BG-24)

- **`documents.send()` JSON mode now accepts an `attachments[]` array.** Files are embedded into the generated UBL XML as base64 via `AdditionalDocumentReference` / `EmbeddedDocumentBinaryObject` (BG-24 / BT-125), so the receiving accounting system sees them inline with the invoice — no extra API call, no separate download.

  ```typescript
  import fs from "node:fs/promises";

  const pdf = await fs.readFile("./invoice-detail.pdf");

  await client.documents.send({
    receiverPeppolId: "0245:12345678",
    items: [
      { description: "Consulting", quantity: 10, unitPrice: 50, vatRate: 23 },
    ],
    attachments: [
      {
        fileName: "invoice-detail.pdf",
        mimeType: "application/pdf",
        content: pdf.toString("base64"),
        description: "Timesheet breakdown",
      },
    ],
  });
  ```

- **Allowed MIME types (BR-CL-22):** `application/pdf`, `image/png`, `image/jpeg`, `text/csv`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (.xlsx), `application/vnd.oasis.opendocument.spreadsheet` (.ods). Server-side magic-byte sniffing — the client-sent `mimeType` must match the actual file content, or the request rejects with `VALIDATION_ERROR`.
- **Limits:** max 20 attachments per invoice, 10 MB per file, 15 MB aggregated per invoice. The JSON body size cap on `/documents/send` was raised from 6 MB to 25 MB to accommodate attachment payloads.
- **Archive:** attachments are also persisted to the firm's object storage and appear in the dashboard detail view (`/d` and `/d2`) with direct download / inline preview links. No new SDK method — the inbox endpoints already include attachment metadata in `attachments[]` of `ReceivedDocumentDetail`.

### Type additions

- **TypeScript:** new `DocumentAttachment` interface, new optional `attachments?: DocumentAttachment[]` on `SendDocumentJsonRequest`.
- **Python:** new `DocumentAttachment` TypedDict, `attachments` key available in the `documents.send()` JSON-mode body dict.
- **Java:** new `SendDocumentRequest.Attachment` record and `.attachments(List<Attachment>)` builder method.
- **.NET:** new `DocumentAttachment` class and `Attachments` property on `SendDocumentRequest`.
- **Ruby / PHP:** no signature change (both SDKs accept raw `Hash` / `array` bodies); `attachments` can be passed directly. YARD / PHPDoc examples added.

### SDK versions

| Language                              | Version |
| ------------------------------------- | ------- |
| TypeScript (`@epostak/sdk`)           | 1.4.0   |
| Python (`epostak`)                    | 0.2.0   |
| Ruby (`epostak` gem)                  | 1.1.0   |
| Java (Maven `sk.epostak:epostak-sdk`) | 1.1.0   |
| PHP (`epostak/sdk`)                   | 1.1.0   |
| .NET (`EPostak.Sdk`)                  | 1.1.0   |

### OpenAPI

- `public/api-docs/enterprise/openapi.json` bumped `1.0.1 → 1.1.0`. New `DocumentAttachment` schema, new `attachments` property on `SendDocumentJsonRequest`, new `json_mode_with_attachments` example on `/documents/send`.

---

## 1.3.1 — 2026-04-19

### Breaking API changes (all SDKs updated)

- **`documents.convert()` request/response rewritten.** Old `{direction, data?, xml?}` / `{direction, result}` replaced with `{input_format, output_format, document}` / `{output_format, document, warnings}`. The API now supports arbitrary `input→output` pairs (currently `json↔ubl`) and returns a warnings array for non-fatal conversion issues. `ConvertDirection` enum replaced with separate `ConvertInputFormat` and `ConvertOutputFormat` types. Migration:
  ```typescript
  // before
  const { result: xml } = await client.documents.convert({ direction: 'json_to_ubl', data: {...} });
  // after
  const { document: xml, warnings } = await client.documents.convert({
    input_format: 'json', output_format: 'ubl', document: {...}
  });
  ```
- **`webhooks.delete(id)` now returns `void` (HTTP 204 No Content).** Previously returned `{deleted: true}` with HTTP 200. No application-level impact — awaiting the promise still signals success, and any non-2xx still throws `EPostakError`.

### API behavior changes (server-side hardening, reflected in SDK)

- **Idempotency key column widened** from VARCHAR(64) to VARCHAR(255). Long `firmId:method:path:clientKey` tuples no longer 500 on the first request.
- **Malformed UUIDs now return `400 BAD_REQUEST`** (Prisma P2023/P2007) instead of unhandled 500s on all `/documents/{id}/…` and `/webhooks/{id}` routes.
- **`documents/convert` error shape normalized** to `{error:{code,message}}` via the shared `errorResponse()` wrapper. Previously some code paths returned 422 with inline bodies.
- **OCR retry loop fixed** — Gemini `generateContent` is now wrapped in the retry `try/catch`, so transient 5xx/parse errors retry with backoff instead of failing the whole batch.
- **Webhook update/delete use single-query tenant-isolated find-or-404** — old pre-check `findUnique()` removed in favor of `updateMany/deleteMany` scoped by `{id, firmId}`. Correctness unchanged, one less DB round-trip per call.

### SDK versions

| Language                              | Version |
| ------------------------------------- | ------- |
| TypeScript (`@epostak/sdk`)           | 1.3.1   |
| Python (`epostak`)                    | 0.1.1   |
| Ruby (`epostak` gem)                  | 1.0.1   |
| Java (Maven `sk.epostak:epostak-sdk`) | 1.0.1   |
| PHP (`epostak/sdk`)                   | 1.0.1   |
| .NET (`EPostak.Sdk`)                  | 1.0.1   |

### OpenAPI

- `public/api-docs/enterprise/openapi.json` bumped `1.0.0 → 1.0.1`. `ConvertDocumentRequest`, `ConvertDocumentResponse`, and `DELETE /webhooks/{id}` reworked to match new shapes. Examples updated.

---

## Unreleased — 2026-04-18

### Backend pricing change (no SDK code change required)

- **ePošťák Enterprise API switched to pay-per-success billing.** You're charged only when Peppol confirms delivery of an outbound document or when a real inbound document arrives. Validation failures, SMP miss, AS4 transport errors and sandbox firms generate no charge.
- **Tiered rates** (per firm, per month). Outbound: €0.10 (1–1 000) / €0.08 (1 001–2 000) / €0.06 (2 001+). Inbound: €0.08 / €0.07 / €0.06 in the same tiers. Canonical pricing table lives on the `/api` landing page.
- **No base fee, no minimum.** Web UI subscriptions (Zadarmo / Štandard / Firma) are free until **2027-01-01**; only API integrators generate revenue in this window.
- **Sandbox firms** (`is_sandbox = true`) are excluded from metering — safe default for new integrator accounts.
- **Metering is live but invoicing is gated** by the backend `BILLING_ENABLED` flag until ePošťák receives its Peppol Authority production certificate. Pre-launch API traffic will not be billed retroactively.

No SDK calls, method signatures or error codes change. See https://epostak.sk/api/docs/enterprise for the full billing reference.

---

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
