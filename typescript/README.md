# @epostak/sdk

Official Node.js / TypeScript SDK for the [ePošťák API](https://epostak.sk/api/docs) — Peppol e-invoicing for Slovakia and the EU.

Zero runtime dependencies. Requires Node.js 18+.

## Major release API shape

TypeScript `4.0.0` is a breaking workflow-first release:

- Enterprise direct firm flow: `client.enterprise.documents.send(...)`
- Enterprise ERP/integrator flow: `client.enterprise.connector.customers.for("erp-customer").submitDocument(...)`
- Enterprise API facade flow: `client.enterprise.payloads.validate(...)`, `client.enterprise.events.pull(...)`, `client.enterprise.documents.supportPacket(...)`
- SAPI-SK interoperable flow: `client.sapi.participants.for("0245:1234567890").documents.send(...)`

Enterprise `firmId` applies only to firm-scoped Enterprise calls. Connector
customer-scoped calls inject `customerRef` and omit `X-Firm-Id`. SAPI document
calls always send `X-Peppol-Participant-Id`.

## Enterprise API facade flow

Use this as the default low-friction path for ERP integrations that do not want
to receive webhooks on day one:

```typescript
await client.enterprise.payloads.validate(invoice);

await client.enterprise.documents.preflight({
  receiverPeppolId: invoice.receiverPeppolId,
});

const sent = await client.enterprise.documents.send(invoice, {
  idempotencyKey: "erp-fa-2026-001",
});

const events = await client.enterprise.events.pull({ limit: 50 });
await client.enterprise.events.batchAck(events.items.map((event) => event.event_id));

const supportPacket = await client.enterprise.documents.supportPacket(sent.documentId);
```

The older `client.enterprise.webhooks.queue` resource remains available for
compatibility. New pull-based integrations should prefer
`client.enterprise.events`.

Non-breaking adoption: facade helpers are additive. Existing
`client.enterprise.extract`, `client.enterprise.documents.validate`,
`client.enterprise.webhooks.queue`, and
`client.enterprise.documents.evidenceBundle` integrations can keep running;
migrate to `payloads`, `events`, and `supportPacket` when your release window
allows.

## Recent changes

### Unreleased — 2026-07-01

- **New:** Enterprise API facade helpers for Payload Assistant validation,
  pull/ack event handling, and document support packets:
  `client.enterprise.payloads.validate(...)`,
  `client.enterprise.events.pull(...)`, and
  `client.enterprise.documents.supportPacket(...)`.

### Unreleased — 2026-06-30

- **New:** `client.connector.mapper(...)` and customer-scoped
  `client.enterprise.connector.customers.for(customerRef).mapper(...)` cover
  `/connector/mapper`.
- **New:** `client.box` / `client.enterprise.box` covers ePošťák Box list,
  create with `payloadXml`, detail, schedule, send-now, retry, and cancel over
  `/box/items`.

### v4.0.0 — 2026-06-14

- **Breaking:** documented Enterprise resources now live under
  `client.enterprise`; old top-level resources remain internal compatibility
  adapters.
- **New:** `client.sapi.participants.for(participantId).documents` requires
  participant scoping before SAPI document send/receive/get/acknowledge.
- **New:** `client.enterprise.connector.customers.for(customerRef)` injects
  `customerRef` and keeps `X-Firm-Id` off customer-managed Connector calls.
- **Docs:** README and migration guide now cover Enterprise direct,
  Enterprise Connector, and SAPI-SK flows as first-class paths.

### v3.3.4 — 2026-06-06

- **New:** `client.enterprise.connector` covers Connector preflight, Zen input, Autopilot lifecycle, reconcile, mailbox policy, sync, Connector documents/UBL/evidence, action execution, send, outbox, status, inbox, ACK, and event polling.
- **Coverage:** static endpoint coverage expanded to 317 checks across TypeScript, Python, Ruby, PHP, .NET, and Java.

### v3.3.3 — 2026-06-05

- **Docs:** Added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence.

### v3.3.2 — 2026-05-18

- **New:** `client.sapi.participants.for(id).documents` covers SAPI-SK 1.0 document send, receive list/detail, and acknowledge.
- **New:** `client.enterprise.webhooks.test(id, { count, mode })` supports direct and queued webhook tests; queued tests return `testRunId` for delivery-history polling.
- **New:** `client.enterprise.webhooks.deadLetters()`, `replayDeadLetter(id)`, and `resolveDeadLetter(id, { reason })` cover webhook dead-letter operations.
- **New:** `client.enterprise.peppol.resolve(...)` maps ERP identifiers (`ico`, `dic`, `icDph`, `peppolId`, or `scheme` + `identifier`) to Peppol participant + routing capability.
- **New:** `client.enterprise.documents.evidenceBundle(id)`, `client.enterprise.pull.outbound.getMdn(id)`, `client.enterprise.peppol.companySearch(...)`, `client.enterprise.documents.peppolDocuments(...)`, and `client.enterprise.account.licenseInfo()`.
- **Coverage:** static endpoint coverage expanded to 97 checks across TypeScript, Python, Ruby, PHP, .NET, and Java.

### v3.2.0 — 2026-05-12

- **New:** Pull API — `client.enterprise.pull.inbound` (`list`, `get`, `getUbl`, `ack`) and `client.enterprise.pull.outbound` (`list`, `get`, `getUbl`, `events`) resources with full TypeScript types (`InboundDocument`, `OutboundDocument`, `OutboundEvent`, etc.).
- **New:** `UblValidationError` class — thrown on `422 UBL_VALIDATION_ERROR`; carries `.rule` (e.g. `"BR-06"`) and `UblRule` exported union type for the 7 known rule codes.
- **New:** `client.enterprise.webhooks.test(id, { event? })` — `event` is now passed as `?event=` query parameter (server precedence over body).
- **New:** `client.lastRateLimit: { limit, remaining, resetAt: Date } | null` — updated after every request that includes `X-RateLimit-*` response headers.
- **Improved:** `WebhookDelivery` type adds optional `idempotency_key?: string` — SHA-256 hex stable across retry attempts.
- **Improved:** `WebhookDeliveriesParams` adds `includeResponseBody?: boolean` (opt-in response body in delivery history).
- **Improved:** `WebhookEvent` union covers delivery-failure events via `"document.delivery_failed"`.
- Resolved doc drifts surfaced by 2026-05-12 endpoint consistency audit.

> **v3.0 — OAuth-only auth.** The SDK now auto-mints a JWT on the first
> API call and refreshes it before expiry. Constructor takes `clientId` +
> `clientSecret` instead of `apiKey`. Raw `sk_live_*` bearer is no longer
> accepted by the server. See [CHANGELOG.md](./CHANGELOG.md).

---

## Installation

```bash
npm install @epostak/sdk
```

---

## Quick Start

```typescript
import { EPostak } from "@epostak/sdk";

const client = new EPostak({
  clientId: "sk_live_xxxxx",
  clientSecret: "sk_live_xxxxx",
});

const result = await client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  invoiceNumber: "FV-2026-001",
  issueDate: "2026-04-04",
  dueDate: "2026-04-18",
  items: [
    { description: "Konzultácia", quantity: 10, unitPrice: 50, vatRate: 23 },
  ],
});
console.log(result.documentId, result.messageId, result.payloadSha256);
```

### Connector golden path for ERP developers

Use `client.enterprise.connector` for the ERP workflow instead of building raw HTTP calls.
The SDK handles OAuth token minting and refresh automatically after you create
the client.

```typescript
const invoice = {
  receiverPeppolId: "0245:1234567890",
  document: {
    invoiceNumber: "FA-2026-001",
    issueDate: "2026-06-04",
    dueDate: "2026-06-18",
    items: [{ description: "Služby", quantity: 1, unitPrice: 100, vatRate: 23 }],
  },
};

const preflight = await client.enterprise.connector.preflight(invoice);
if (!preflight.ready) {
  console.error(preflight.repairReport.blocking);
  throw new Error("Invoice is not ready for Peppol delivery");
}

const staged = await client.enterprise.connector.outbox.stage({
  items: [
    {
      externalId: "FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: invoice,
    },
  ],
});

const sent = await client.enterprise.connector.outbox.send(staged.items[0].outboxId);
if (!sent.documentId) {
  throw new Error("Staged invoice was not sent");
}

const status = await client.enterprise.connector.status(sent.documentId);

const inbox = await client.enterprise.connector.inbox({ limit: 20 });
for (const doc of inbox.documents) {
  await client.enterprise.connector.ack(doc.documentId);
}

// Evidence is shared with the Enterprise document API.
const evidence = await client.enterprise.documents.evidence(sent.documentId);
console.log(status.status, evidence);
```

For immediate send without staging:

```typescript
const sent = await client.enterprise.connector.send(invoice, {
  idempotencyKey: "erp-fa-2026-001-send",
});
console.log(sent.documentId, sent.status);
```

Connector v2 Autopilot stores a durable lifecycle run and reconciliation gives
ERP sync jobs one place to read exceptions:

```typescript
const run = await client.enterprise.connector.autopilot({
  customerRef: "erp-customer-1",
  mode: "shadow",
  externalId: "FA-2026-001",
  idempotencyKey: "erp-fa-2026-001",
  payload: invoice,
});
const sentRun = await client.enterprise.connector.sendAutopilotRun(run.autopilotId);
const exceptions = await client.enterprise.connector.reconcile({ status: "exceptions" });
console.log(sentRun.lifecycleStatus, exceptions.total);
```

Customer-scoped Connector calls are the preferred integrator shape when you
already know the managed ERP customer:

```typescript
const customer = client.enterprise.connector.customers.for("erp-customer-1");
const run = await customer.submitDocument({
  externalId: "FA-2026-001",
  idempotencyKey: "erp-fa-2026-001",
  payload: invoice,
});
console.log(run.autopilotId);
```

Common sandbox scenarios to test:

- nonexistent participant or unsupported document type: `preflight.ready === false` with blocking `repairReport` items
- invalid UBL or missing buyer/seller data: `preflight` or `send` returns validation details in the typed `EPostakError`
- duplicate idempotency key: `409 idempotency_conflict`
- expired token: the SDK refreshes automatically; persistent auth failures surface as `EPostakError`
- received invoice processing: poll `client.enterprise.connector.inbox(...)`, store the payload, then call `client.enterprise.connector.ack(documentId)`

Batch workers can send queued items with:

```typescript
await client.enterprise.connector.outbox.sendBatch({ limit: 50 }); // ready, failed, and due scheduled items
```

---

## SAPI-SK participant flow

```typescript
const participant = client.sapi.participants.for("0245:1234567890");

await participant.documents.send(
  {
    metadata: {
      documentId: "FA-2026-001",
      documentTypeId: "invoice",
      processId: "billing",
      senderParticipantId: "0245:1234567890",
      receiverParticipantId: "0245:0987654321",
      creationDateTime: "2026-06-14T10:00:00Z",
    },
    payload: "<Invoice/>",
    payloadFormat: "XML",
  },
  { idempotencyKey: "sapi-fa-2026-001" },
);
```

---

## Peppol ID Format (Slovakia)

| Scheme | Identifier | Format            | Example           |
| ------ | ---------- | ----------------- | ----------------- |
| `0245` | DIČ        | `0245:XXXXXXXXXX` | `0245:1234567890` |

Per Slovak PASR, only `0245:DIČ` is used. The `9950:SK...` VAT form is not supported.

---

## Authentication

| Key prefix  | Use case                                           |
| ----------- | -------------------------------------------------- |
| `sk_live_*` | Direct access — acts on behalf of your own firm    |
| `sk_int_*`  | Integrator access — acts on behalf of client firms |

```typescript
const client = new EPostak({
  clientId: "sk_live_xxxxx",
  clientSecret: "sk_live_xxxxx",
  baseUrl: "https://dev.epostak.sk/api/v1", // optional test env; omit for prod
});
```

Connector V2 integrator calls do not need `firmId`; pass your ERP customer key
as `customerRef` on Connector requests. Use `firmId` or `client.withFirm(...)`
only for legacy Enterprise API calls that still target one firm directly.

Production is the SDK default: Enterprise `https://epostak.sk/api/v1`, SAPI
`https://epostak.sk/sapi/v1`, OAuth origin `https://epostak.sk`. For test
calls, set `baseUrl` to `https://dev.epostak.sk/api/v1`; SAPI derives
`https://dev.epostak.sk/sapi/v1`, and OAuth helpers need
`origin: "https://dev.epostak.sk"` because OAuth is outside `/api/v1`.

### OAuth `client_credentials` (automatic)

The SDK automatically mints a JWT on the first request and refreshes it
before expiry. You never handle tokens directly. For manual token management:

```typescript
const tokens = await client.enterprise.auth.token({
  clientId: "sk_live_xxxxx",
  clientSecret: "sk_live_xxxxx",
});
console.log(tokens.access_token, tokens.expires_in); // 900s

const renewed = await client.enterprise.auth.renew({
  refreshToken: tokens.refresh_token,
});

await client.enterprise.auth.revoke({
  token: tokens.refresh_token,
  tokenTypeHint: "refresh_token",
});
```

### Key introspection, rotation, IP allowlist

```typescript
const status = await client.enterprise.auth.status();
console.log(status.key.prefix, status.plan.name, status.firm.peppolStatus);

const rotated = await client.enterprise.auth.rotateSecret(); // sk_live_* only
console.log(rotated.key); // store immediately — only returned once

await client.enterprise.auth.ipAllowlist.update({
  cidrs: ["192.168.1.0/24", "203.0.113.42"],
});
const { ip_allowlist } = await client.enterprise.auth.ipAllowlist.get();
```

---

## API Reference

### Documents

```typescript
// Send a document (JSON mode — UBL auto-generated)
const result = await client.enterprise.documents.send(
  {
    receiverPeppolId: "0245:1234567890",
    receiverName: "Firma s.r.o.",
    invoiceNumber: "FV-2026-001",
    issueDate: "2026-04-04",
    dueDate: "2026-04-18",
    currency: "EUR",
    items: [{ description: "Konzultácia", quantity: 10, unitPrice: 50, vatRate: 23 }],
  },
  // Optional: replay-safe send. Server returns 409 (idempotency_conflict)
  // if the same key is replayed before the original request finishes.
  { idempotencyKey: "fv-2026-001-send" },
);

// Send pre-built UBL XML
await client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  xml: '<?xml version="1.0"?>...',
});

// Get document by ID
const doc = await client.enterprise.documents.get("doc-uuid");

// Update a draft document
await client.enterprise.documents.update("doc-uuid", { invoiceNumber: "FV-2026-002", dueDate: "2026-05-01" });

// Status with full history
const status = await client.enterprise.documents.status("doc-uuid");

// Delivery evidence (AS4, MLR, invoice response)
const evidence = await client.enterprise.documents.evidence("doc-uuid");

// Download PDF / UBL XML
const pdf = await client.enterprise.documents.pdf("doc-uuid");
const ubl = await client.enterprise.documents.ubl("doc-uuid");

// Respond to received invoice (AP=accept, RE=reject, UQ=query)
await client.enterprise.documents.respond("doc-uuid", { status: "AP", note: "Akceptované" });

// Validate without sending — pass the JSON invoice or raw UBL XML
const validation = await client.enterprise.documents.validate({
  format: "json",
  document: { receiverPeppolId: "0245:1234567890", items: [/* ... */] },
});

// Check receiver capability
const check = await client.enterprise.documents.preflight({ receiverPeppolId: "0245:1234567890" });

// Convert between JSON and UBL
const converted = await client.enterprise.documents.convert({
  input_format: "json",
  output_format: "ubl",
  document: { ... },
});
```

### Inbox

```typescript
// List received documents
const inbox = await client.enterprise.documents.inbox.list({
  limit: 20,
  status: "RECEIVED",
  since: "2026-04-01T00:00:00Z",
});

// Get full detail with UBL XML payload
const detail = await client.enterprise.documents.inbox.get("doc-uuid");
console.log(detail.document, detail.payload);

// Acknowledge (mark as processed)
await client.enterprise.documents.inbox.acknowledge("doc-uuid");

// Cross-firm inbox (integrator only)
const all = await client.enterprise.documents.inbox.listAll({
  limit: 50,
  firm_id: "firm-uuid",
});
```

### Audit (per-firm security feed)

Cursor-paginated walk over `(occurred_at DESC, id DESC)`.

```typescript
let cursor: string | null = null;
do {
  const page = await client.enterprise.audit.list({
    event: "jwt.issued",
    since: "2026-04-01T00:00:00Z",
    cursor,
    limit: 50,
  });
  for (const ev of page.items) {
    console.log(ev.occurred_at, ev.event, ev.actor_id);
  }
  cursor = page.next_cursor;
} while (cursor);
```

### Peppol

```typescript
const participant = await client.enterprise.peppol.lookup("0245", "1234567890");
if (participant?.accepts && participant.routingStatus === "ready") {
  // Receiver is registered and routable for the default BIS Billing invoice.
}

const caps = await client.enterprise.peppol.capabilities({
  participant: { scheme: "0245", identifier: "1234567890" },
  documentType: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##...",
});

const results = await client.enterprise.peppol.directory.search({
  q: "Telekom",
  country: "SK",
});

const company = await client.enterprise.peppol.companyLookup("12345678");

const matches = await client.enterprise.peppol.companySearch({ q: "Demo", limit: 10 });

const resolved = await client.enterprise.peppol.resolve({
  ico: "12345678",
  documentTypeId: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##...",
});
```

### Firms (integrator)

```typescript
const firms = await client.enterprise.firms.list();
const firm = await client.enterprise.firms.get("firm-uuid");
const docs = await client.enterprise.firms.documents("firm-uuid", {
  limit: 20,
  direction: "inbound",
});
await client.enterprise.firms.registerPeppolId("firm-uuid", {
  scheme: "0245",
  identifier: "1234567890",
});

// Assign firm by ICO
await client.enterprise.firms.assign({ ico: "12345678" });
await client.enterprise.firms.assignBatch({ icos: ["12345678", "87654321"] });
```

### Webhooks

```typescript
// Create webhook (store secret for HMAC verification!)
const webhook = await client.enterprise.webhooks.create(
  {
    url: "https://example.com/webhook",
    events: ["document.received", "document.sent"],
  },
  { idempotencyKey: "create-prod-webhook" },
);

const list = await client.enterprise.webhooks.list();
const detail = await client.enterprise.webhooks.get(webhook.id);
await client.enterprise.webhooks.update(webhook.id, { isActive: false });
await client.enterprise.webhooks.delete(webhook.id);

// Rotate the signing secret (issues a fresh one, invalidates the old).
const { secret } = await client.enterprise.webhooks.rotateSecret(webhook.id);

// Production-like test: enqueue delivery rows for the normal webhook worker.
const queued = await client.enterprise.webhooks.test(webhook.id, {
  event: "document.received",
  count: 250,
  mode: "queued",
});

const deliveries = await client.enterprise.webhooks.deliveries(webhook.id, {
  testRunId: queued.testRunId,
  includeResponseBody: true,
});

const dlq = await client.enterprise.webhooks.deadLetters({ includeResponseBody: true });
for (const failed of dlq.items) {
  await client.enterprise.webhooks.replayDeadLetter(failed.id);
  // or: await client.enterprise.webhooks.resolveDeadLetter(failed.id, { reason: "Handled in ERP" });
}
```

#### Verifying a delivery

```typescript
import express from "express";
import { verifyWebhookSignature } from "@epostak/sdk";

const app = express();

app.post(
  "/webhooks/epostak",
  // express.raw is required — we MUST hash the bytes off the wire,
  // not the parsed-and-re-stringified JSON.
  express.raw({ type: "application/json" }),
  (req, res) => {
    const result = verifyWebhookSignature({
      payload: req.body, // Buffer
      signature: req.header("x-webhook-signature") ?? "",
      timestamp: req.header("x-webhook-timestamp") ?? "",
      secret: process.env.EPOSTAK_WEBHOOK_SECRET!,
      // toleranceSeconds: 300, // default — clamps replay attacks
    });
    if (!result.valid) {
      return res.status(400).send(`bad signature: ${result.reason}`);
    }
    const event = JSON.parse(req.body.toString("utf8"));
    // process event...
    res.status(204).end();
  },
);
```

#### Dedup + retry headers (server v1.1 — 2026-05-12)

The server now ships three additional headers on every push delivery:

| Header | Value | Use |
|-|-|-|
| `X-Webhook-Event-Id` | UUID, stable across retries | **Primary dedup key.** Body also carries it as `webhook_event_id`. |
| `X-Webhook-Attempt` | 1-based attempt number | Telemetry / logging. |
| `X-Webhook-Max-Attempts` | Total attempts in the retry window (10) | Telemetry / logging. |

Recommended receiver pattern:

```typescript
// INSERT ON CONFLICT DO NOTHING on the event id is enough — every retry
// of the same logical event carries the SAME X-Webhook-Event-Id.
const eventId = req.header("x-webhook-event-id");
const inserted = await db.query(
  `INSERT INTO processed_webhooks (event_id) VALUES ($1)
   ON CONFLICT (event_id) DO NOTHING RETURNING id`,
  [eventId],
);
if (inserted.rowCount === 0) {
  return res.status(200).end(); // duplicate — ack and skip
}
// process event for the first time...
```

**Retry policy (server-side, as of 2026-05-12):** we retry only on `408`, `425`, `429`, `502`, `503`, `504` and network errors (~44h bounded backoff). Returning any other 4xx/5xx — including `500` — terminates the retry loop immediately. If your handler hits an app-level error and you want us to retry, return `503` (not `500`).

The signature contract is **unchanged** — `verifyWebhookSignature` continues to work without code changes.

### Events pull facade

```typescript
// Pull pending events
const queue = await client.enterprise.events.pull({ limit: 50 });
for (const item of queue.items) {
  console.log(item.event_id, item.event, item.payload);
  await client.enterprise.events.ack(item.event_id);
}
if (queue.has_more) {
  // Drain remaining events on the next iteration
}

// Batch acknowledge
await client.enterprise.events.batchAck(queue.items.map((e) => e.event_id));
```

`client.enterprise.webhooks.queue` remains available for legacy queue and
cross-firm drain jobs.

### Reporting

```typescript
// Convenience period selector
const stats = await client.enterprise.reporting.statistics({ period: "month" });
console.log(stats.sent.total, stats.sent.by_type);
console.log(stats.received.total, stats.received.by_type);
console.log(stats.delivery_rate); // e.g. 0.987
console.log(stats.top_recipients); // up to 5
console.log(stats.top_senders);

// Or an explicit window
await client.enterprise.reporting.statistics({ from: "2026-01-01", to: "2026-03-31" });
```

### Account

```typescript
const account = await client.enterprise.account.get();
```

### Payload Assistant OCR

```typescript
import { readFileSync } from "fs";

// Single file
const result = await client.enterprise.payloads.extract(
  readFileSync("invoice.pdf"),
  "application/pdf",
  "invoice.pdf",
);
// Outbound invoices can include result.send_payload for review + validate + send.
if (result.send_payload) {
  await client.enterprise.payloads.validate(result.send_payload);
}

// Batch (up to 50 files, server-side)
const batch = await client.enterprise.payloads.extractBatch([
  { file: pdfBuffer, mimeType: "application/pdf", fileName: "inv1.pdf" },
  { file: imgBuffer, mimeType: "image/png", fileName: "inv2.png" },
]);
```

`client.enterprise.extract` remains available for compatibility; new
integrations should use `client.enterprise.payloads.extract`.

---

## Integrator Mode

Use this for legacy firm-scoped Enterprise API calls. Connector V2 calls
(`autopilot`, `mapper`, `zenInput`, mailbox, sync, Connector documents, actions) stay
integrator-scoped and resolve the managed firm from `customerRef`, so the SDK
does not send `X-Firm-Id` for those methods even if this client has `firmId`.

```typescript
// Option 1: firmId in constructor
const client = new EPostak({
  clientId: "sk_int_xxxxx",
  clientSecret: "sk_int_xxxxx",
  firmId: "client-firm-uuid",
});

// Option 2: withFirm() for switching (shares JWT)
const base = new EPostak({
  clientId: "sk_int_xxxxx",
  clientSecret: "sk_int_xxxxx",
});
const clientA = base.withFirm("firm-uuid-a");
const clientB = base.withFirm("firm-uuid-b");
```

---

## Error Handling

`EPostakError` normalizes both the legacy `{ error: { code, message } }`
envelope and RFC 7807 `application/problem+json`.

```typescript
import { EPostak, EPostakError } from "@epostak/sdk";

try {
  await client.enterprise.documents.send({ ... });
} catch (err) {
  if (err instanceof EPostakError) {
    console.error(err.status);         // HTTP status (0 for network errors)
    console.error(err.code);           // e.g. 'VALIDATION_FAILED'
    console.error(err.message);        // Human-readable
    console.error(err.details);        // Validation error list (422)
    console.error(err.requestId);      // From X-Request-Id (or body)
    console.error(err.title, err.detail, err.type, err.instance); // RFC 7807

    if (err.code === "idempotency_conflict") {
      // The same Idempotency-Key is still in flight server-side.
      return;
    }
    if (err.requiredScope) {
      // 403 with WWW-Authenticate: insufficient_scope
      console.error(`Mint a token with scope: ${err.requiredScope}`);
    }
  }
}
```

**Common error codes from `documents.send()`:**

| Status | Code                   | Meaning                                                                  |
| ------ | ---------------------- | ------------------------------------------------------------------------ |
| 409    | `idempotency_conflict` | Same `Idempotency-Key` is still in flight server-side. Retry shortly.    |
| 422    | `VALIDATION_FAILED`    | Document failed Peppol BIS 3.0 validation. `details` has the error list. |
| 502    | `SEND_FAILED`          | Peppol network temporarily unavailable. Retryable.                       |

---

## Full Endpoint Map

| Method                                   | HTTP   | Path                                         |
|-|-|-|
| `auth.token({ clientId, clientSecret })` | POST   | `/auth/token`                                |
| `auth.renew({ refreshToken })`           | POST   | `/auth/renew`                                |
| `auth.revoke({ token })`                 | POST   | `/auth/revoke`                               |
| `auth.status()`                          | GET    | `/auth/status` (alias: `/auth/token/status`) |
| `auth.rotateSecret()`                    | POST   | `/auth/rotate-secret`                        |
| `auth.ipAllowlist.get()`                 | GET    | `/auth/ip-allowlist`                         |
| `auth.ipAllowlist.update({ cidrs })`     | PUT    | `/auth/ip-allowlist`                         |
| `audit.list(params?)`                    | GET    | `/audit`                                     |
| `documents.get(id)`                      | GET    | `/documents/{id}`                            |
| `documents.update(id, body)`             | PATCH  | `/documents/{id}`                            |
| `documents.send(body, opts?)`            | POST   | `/documents/send`                            |
| `documents.sendBatch(items, opts?)`      | POST   | `/documents/send/batch`                      |
| `documents.status(id)`                   | GET    | `/documents/{id}/status`                     |
| `documents.statusBatch(ids)`             | POST   | `/documents/status/batch`                    |
| `documents.evidence(id)`                 | GET    | `/documents/{id}/evidence`                   |
| `documents.evidenceBundle(id)`           | GET    | `/documents/{id}/evidence-bundle`            |
| `documents.supportPacket(id)`            | GET    | `/documents/{id}/support-packet`             |
| `documents.envelope(id)`                 | GET    | `/documents/{id}/envelope`                   |
| `documents.pdf(id)`                      | GET    | `/documents/{id}/pdf`                        |
| `documents.ubl(id)`                      | GET    | `/documents/{id}/ubl`                        |
| `documents.respond(id, body)`            | POST   | `/documents/{id}/respond`                    |
| `documents.mark(id, state, note?)`       | POST   | `/documents/{id}/mark`                       |
| `documents.validate(body)`               | POST   | `/documents/validate`                        |
| `documents.preflight(body)`              | POST   | `/documents/preflight`                       |
| `documents.convert(body)`                | POST   | `/documents/convert`                         |
| `documents.parse(xml)`                   | POST   | `/documents/parse`                           |
| `documents.outbox(params?)`              | GET    | `/documents/outbox`                          |
| `documents.responses(id)`                | GET    | `/documents/{id}/responses`                  |
| `documents.events(id, params?)`          | GET    | `/documents/{id}/events`                     |
| `documents.peppolDocuments(params?)`     | GET    | `/peppol-documents`                          |
| `documents.inbox.list(params?)`          | GET    | `/documents/inbox`                           |
| `documents.inbox.get(id)`                | GET    | `/documents/inbox/{id}`                      |
| `documents.inbox.acknowledge(id)`        | POST   | `/documents/inbox/{id}/acknowledge`          |
| `documents.inbox.listAll(params?)`       | GET    | `/documents/inbox/all`                       |
| `peppol.lookup(scheme, id)`              | GET    | `/peppol/participants/{scheme}/{id}`         |
| `peppol.directory.search(params?)`       | GET    | `/peppol/directory/search`                   |
| `peppol.companyLookup(ico)`              | GET    | `/company/lookup/{ico}`                      |
| `peppol.companySearch(params)`           | GET    | `/company/search`                            |
| `peppol.resolve(params)`                 | GET    | `/peppol/participants/resolve`               |
| `peppol.capabilities(body)`              | POST   | `/peppol/capabilities`                       |
| `peppol.lookupBatch(participants)`       | POST   | `/peppol/participants/batch`                 |
| `firms.list()`                           | GET    | `/firms`                                     |
| `firms.get(id)`                          | GET    | `/firms/{id}`                                |
| `firms.documents(id, params?)`           | GET    | `/firms/{id}/documents`                      |
| `firms.registerPeppolId(id, body)`       | POST   | `/firms/{id}/peppol-identifiers`             |
| `firms.assign(body)`                     | POST   | `/firms/assign`                              |
| `firms.assignBatch(body)`                | POST   | `/firms/assign/batch`                        |
| `webhooks.create(body, opts?)`           | POST   | `/webhooks`                                  |
| `webhooks.list()`                        | GET    | `/webhooks`                                  |
| `webhooks.get(id)`                       | GET    | `/webhooks/{id}`                             |
| `webhooks.update(id, body)`              | PATCH  | `/webhooks/{id}`                             |
| `webhooks.delete(id)`                    | DELETE | `/webhooks/{id}`                             |
| `webhooks.test(id, opts?)`               | POST   | `/webhooks/{id}/test`                        |
| `webhooks.deliveries(id, params?)`       | GET    | `/webhooks/{id}/deliveries`                  |
| `webhooks.rotateSecret(id)`              | POST   | `/webhooks/{id}/rotate-secret`               |
| `webhooks.deadLetters(params?)`          | GET    | `/webhook-dead-letter`                       |
| `webhooks.replayDeadLetter(id)`          | POST   | `/webhook-dead-letter/{id}/replay`           |
| `webhooks.resolveDeadLetter(id, body?)`  | POST   | `/webhook-dead-letter/{id}/resolve`          |
| `webhooks.queue.pull(params?)`           | GET    | `/webhook-queue`                             |
| `webhooks.queue.ack(eventId)`            | DELETE | `/webhook-queue/{eventId}`                   |
| `webhooks.queue.batchAck(ids)`           | POST   | `/webhook-queue/batch-ack`                   |
| `webhooks.queue.pullAll(params?)`        | GET    | `/webhook-queue/all`                         |
| `webhooks.queue.batchAckAll(ids)`        | POST   | `/webhook-queue/all/batch-ack`               |
| `payloads.extract(file, mimeType, fileName?)` | POST | `/payloads/extract`                          |
| `payloads.extractBatch(files)`           | POST   | `/payloads/extract/batch`                    |
| `payloads.parse(xml)`                    | POST   | `/payloads/parse`                            |
| `payloads.convert(body)`                 | POST   | `/payloads/convert`                          |
| `payloads.validate(body)`                | POST   | `/payloads/validate`                         |
| `events.pull(params?)`                   | GET    | `/events/pull`                               |
| `events.ack(eventId)`                    | POST   | `/events/{eventId}/ack`                      |
| `events.batchAck(ids)`                   | POST   | `/events/batch-ack`                          |
| `reporting.statistics(params?)`          | GET    | `/reporting/statistics`                      |
| `reporting.submissions(params?)`         | GET    | `/reporting/submissions`                     |
| `account.get()`                          | GET    | `/account`                                   |
| `account.licenseInfo()`                  | GET    | `/licenses/info`                             |
| `integrator.keys.list()`                 | GET    | `/integrator/keys`                           |
| `integrator.keys.deactivate(body)`       | DELETE | `/integrator/keys`                           |
| `integrator.licenses.info(params?)`      | GET    | `/integrator/licenses/info`                  |
| `extract.single(file, mime, name)`       | POST   | `/extract`                                   |
| `extract.batch(files)`                   | POST   | `/extract/batch`                             |
| `EPostak.validate(xml)`                  | POST   | `https://epostak.sk/api/validate`            |
| `box.list(params?)`                      | GET    | `/box/items`                                 |
| `box.create({ payloadXml, ... })`        | POST   | `/box/items`                                 |
| `box.get(itemId)`                        | GET    | `/box/items/{itemId}`                        |
| `box.schedule(itemId, body)`             | POST   | `/box/items/{itemId}/schedule`               |
| `box.sendNow(itemId)`                    | POST   | `/box/items/{itemId}/send-now`               |
| `box.retry(itemId)`                      | POST   | `/box/items/{itemId}/retry`                  |
| `box.cancel(itemId)`                     | POST   | `/box/items/{itemId}/cancel`                 |
| `connector.preflight(body)`              | POST   | `/connector/preflight`                       |
| `connector.send(body, opts?)`            | POST   | `/connector/send`                            |
| `connector.outbox.stage(body)`           | POST   | `/connector/outbox`                          |
| `connector.outbox.list(params?)`         | GET    | `/connector/outbox`                          |
| `connector.outbox.get(outboxId)`         | GET    | `/connector/outbox/{outboxId}`               |
| `connector.outbox.send(outboxId, opts?)` | POST   | `/connector/outbox/{outboxId}/send`          |
| `connector.outbox.sendBatch(body?)`      | POST   | `/connector/outbox/send`                     |
| `connector.outbox.cancel(outboxId)`      | DELETE | `/connector/outbox/{outboxId}`               |
| `connector.mapper(body)`                 | POST   | `/connector/mapper`                          |
| `connector.zenInput(body)`               | POST   | `/connector/zen-input`                       |
| `connector.autopilot(body)`              | POST   | `/connector/autopilot`                       |
| `connector.getAutopilotRun(autopilotId)` | GET    | `/connector/autopilot/{autopilotId}`         |
| `connector.sendAutopilotRun(autopilotId)` | POST  | `/connector/autopilot/{autopilotId}/send`    |
| `connector.reconcile(params?)`           | GET    | `/connector/reconcile`                       |
| `connector.mailboxes()`                  | GET    | `/connector/mailbox`                         |
| `connector.repairMailbox(body?)`         | POST   | `/connector/mailbox/repair`                  |
| `connector.updateMailboxSendPolicy(customerRef, body)` | PATCH | `/connector/mailbox/{customerRef}/send-policy` |
| `connector.sync(params?)`                | GET    | `/connector/sync`                            |
| `connector.getDocument(documentId)`      | GET    | `/connector/documents/{documentId}`          |
| `connector.getDocumentUbl(documentId)`   | GET    | `/connector/documents/{documentId}/ubl`      |
| `connector.getDocumentEvidence(documentId)` | GET | `/connector/documents/{documentId}/evidence` |
| `connector.getDocumentEvidenceBundle(documentId)` | GET | `/connector/documents/{documentId}/evidence-bundle` |
| `connector.getDocumentSupportPacket(documentId)` | GET | `/connector/documents/{documentId}/support-packet` |
| `connector.documents.supportPacket(documentId)` | GET | `/connector/documents/{documentId}/support-packet` |
| `connector.runAction(actionId, body?)`   | POST   | `/connector/actions/{actionId}`              |
| `connector.status(documentId)`           | GET    | `/connector/status/{documentId}`             |
| `connector.inbox(params?)`               | GET    | `/connector/inbox`                           |
| `connector.getInboxDocument(documentId)` | GET    | `/connector/inbox/{documentId}`              |
| `connector.ack(documentId)`              | POST   | `/connector/inbox/{documentId}/ack`          |
| `connector.events(params?)`              | GET    | `/connector/events`                          |
| `inbound.list(params?)`                  | GET    | `/inbound/documents`                         |
| `inbound.get(id)`                        | GET    | `/inbound/documents/{id}`                    |
| `inbound.getUbl(id)`                     | GET    | `/inbound/documents/{id}/ubl`                |
| `inbound.ack(id, params?)`               | POST   | `/inbound/documents/{id}/ack`                |
| `outbound.list(params?)`                 | GET    | `/outbound/documents`                        |
| `outbound.get(id)`                       | GET    | `/outbound/documents/{id}`                   |
| `outbound.getUbl(id)`                    | GET    | `/outbound/documents/{id}/ubl`               |
| `outbound.getMdn(id)`                    | GET    | `/outbound/documents/{id}/mdn`               |
| `outbound.events(params?)`               | GET    | `/outbound/events`                           |
| `sapi.participants.for(id).documents.send(body, opts?)` | POST | `/sapi/v1/document/send` |
| `sapi.participants.for(id).documents.receive(params?)` | GET | `/sapi/v1/document/receive` |
| `sapi.participants.for(id).documents.get(documentId)` | GET | `/sapi/v1/document/receive/{id}` |
| `sapi.participants.for(id).documents.acknowledge(documentId)` | POST | `/sapi/v1/document/receive/{id}/acknowledge` |

Production Enterprise paths are relative to `https://epostak.sk/api/v1`; test Enterprise paths use `https://dev.epostak.sk/api/v1`. SAPI uses the same host, for example `https://epostak.sk/sapi/v1` or `https://dev.epostak.sk/sapi/v1`.

---

## License

MIT
