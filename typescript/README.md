# @epostak/sdk

Official Node.js / TypeScript SDK for the [ePošťák API](https://epostak.sk/api/docs) — Peppol e-invoicing for Slovakia and the EU.

Zero runtime dependencies. Requires Node.js 18+.

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

const result = await client.documents.send({
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
  baseUrl: "https://...", // optional, defaults to https://epostak.sk/api/v1
  firmId: "uuid", // optional, required for integrator keys
});
```

### OAuth `client_credentials` (automatic)

The SDK automatically mints a JWT on the first request and refreshes it
before expiry. You never handle tokens directly. For manual token management:

```typescript
const tokens = await client.auth.token({
  clientId: "sk_live_xxxxx",
  clientSecret: "sk_live_xxxxx",
});
console.log(tokens.access_token, tokens.expires_in); // 900s

const renewed = await client.auth.renew({
  refreshToken: tokens.refresh_token,
});

await client.auth.revoke({
  token: tokens.refresh_token,
  tokenTypeHint: "refresh_token",
});
```

### Key introspection, rotation, IP allowlist

```typescript
const status = await client.auth.status();
console.log(status.key.prefix, status.plan.name, status.firm.peppolStatus);

const rotated = await client.auth.rotateSecret(); // sk_live_* only
console.log(rotated.key); // store immediately — only returned once

await client.auth.ipAllowlist.update({
  cidrs: ["192.168.1.0/24", "203.0.113.42"],
});
const { ip_allowlist } = await client.auth.ipAllowlist.get();
```

---

## API Reference

### Documents

```typescript
// Send a document (JSON mode — UBL auto-generated)
const result = await client.documents.send(
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
await client.documents.send({
  receiverPeppolId: "0245:1234567890",
  xml: '<?xml version="1.0"?>...',
});

// Get document by ID
const doc = await client.documents.get("doc-uuid");

// Update a draft document
await client.documents.update("doc-uuid", { invoiceNumber: "FV-2026-002", dueDate: "2026-05-01" });

// Status with full history
const status = await client.documents.status("doc-uuid");

// Delivery evidence (AS4, MLR, invoice response)
const evidence = await client.documents.evidence("doc-uuid");

// Download PDF / UBL XML
const pdf = await client.documents.pdf("doc-uuid");
const ubl = await client.documents.ubl("doc-uuid");

// Respond to received invoice (AP=accept, RE=reject, UQ=query)
await client.documents.respond("doc-uuid", { status: "AP", note: "Akceptované" });

// Validate without sending
const validation = await client.documents.validate({ receiverPeppolId: "0245:1234567890", items: [...] });

// Check receiver capability
const check = await client.documents.preflight({ receiverPeppolId: "0245:1234567890" });

// Convert between JSON and UBL
const converted = await client.documents.convert({
  input_format: "json",
  output_format: "ubl",
  document: { ... },
});
```

### Inbox

```typescript
// List received documents
const inbox = await client.documents.inbox.list({
  limit: 20,
  status: "RECEIVED",
  since: "2026-04-01T00:00:00Z",
});

// Get full detail with UBL XML payload
const detail = await client.documents.inbox.get("doc-uuid");
console.log(detail.document, detail.payload);

// Acknowledge (mark as processed)
await client.documents.inbox.acknowledge("doc-uuid");

// Cross-firm inbox (integrator only)
const all = await client.documents.inbox.listAll({
  limit: 50,
  firm_id: "firm-uuid",
});
```

### Audit (per-firm security feed)

Cursor-paginated walk over `(occurred_at DESC, id DESC)`.

```typescript
let cursor: string | null = null;
do {
  const page = await client.audit.list({
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
const participant = await client.peppol.lookup("0245", "1234567890");

const results = await client.peppol.directory.search({
  q: "Telekom",
  country: "SK",
});

const company = await client.peppol.companyLookup("12345678");
```

### Firms (integrator)

```typescript
const firms = await client.firms.list();
const firm = await client.firms.get("firm-uuid");
const docs = await client.firms.documents("firm-uuid", {
  limit: 20,
  direction: "inbound",
});
await client.firms.registerPeppolId("firm-uuid", {
  scheme: "0245",
  identifier: "1234567890",
});

// Assign firm by ICO
await client.firms.assign({ ico: "12345678" });
await client.firms.assignBatch({ icos: ["12345678", "87654321"] });
```

### Webhooks

```typescript
// Create webhook (store secret for HMAC verification!)
const webhook = await client.webhooks.create(
  {
    url: "https://example.com/webhook",
    events: ["document.received", "document.sent"],
  },
  { idempotencyKey: "create-prod-webhook" },
);

const list = await client.webhooks.list();
const detail = await client.webhooks.get(webhook.id);
await client.webhooks.update(webhook.id, { isActive: false });
await client.webhooks.delete(webhook.id);

// Rotate the signing secret (issues a fresh one, invalidates the old).
const { secret } = await client.webhooks.rotateSecret(webhook.id);
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
      signatureHeader: req.header("x-epostak-signature") ?? "",
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

### Webhook Pull Queue

```typescript
// Pull pending events
const queue = await client.webhooks.queue.pull({ limit: 50 });
for (const item of queue.items) {
  console.log(item.id, item.type, item.payload);
  await client.webhooks.queue.ack(item.id);
}

// Batch acknowledge
await client.webhooks.queue.batchAck(queue.items.map((e) => e.id));

// Cross-firm (integrator)
const allEvents = await client.webhooks.queue.pullAll({ limit: 200 });
await client.webhooks.queue.batchAckAll(
  allEvents.events.map((e) => e.event_id),
);
```

### Reporting

```typescript
// Convenience period selector
const stats = await client.reporting.statistics({ period: "month" });
console.log(stats.sent.total, stats.sent.by_type);
console.log(stats.received.total, stats.received.by_type);
console.log(stats.delivery_rate); // e.g. 0.987
console.log(stats.top_recipients); // up to 5
console.log(stats.top_senders);

// Or an explicit window
await client.reporting.statistics({ from: "2026-01-01", to: "2026-03-31" });
```

### Account

```typescript
const account = await client.account.get();
```

### Extract (AI OCR)

```typescript
import { readFileSync } from "fs";

// Single file
const result = await client.extract.single(
  readFileSync("invoice.pdf"),
  "application/pdf",
  "invoice.pdf",
);

// Batch (up to 10 files, server-side)
const batch = await client.extract.batch([
  { file: pdfBuffer, mimeType: "application/pdf", fileName: "inv1.pdf" },
  { file: imgBuffer, mimeType: "image/png", fileName: "inv2.png" },
]);
```

---

## Integrator Mode

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
  await client.documents.send({ ... });
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
| ---------------------------------------- | ------ | -------------------------------------------- |
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
| `documents.evidence(id)`                 | GET    | `/documents/{id}/evidence`                   |
| `documents.pdf(id)`                      | GET    | `/documents/{id}/pdf`                        |
| `documents.ubl(id)`                      | GET    | `/documents/{id}/ubl`                        |
| `documents.respond(id, body)`            | POST   | `/documents/{id}/respond`                    |
| `documents.validate(body)`               | POST   | `/documents/validate`                        |
| `documents.preflight(body)`              | POST   | `/documents/preflight`                       |
| `documents.convert(body)`                | POST   | `/documents/convert`                         |
| `documents.receiveCallback(body)`        | POST   | `/document/receive-callback`                 |
| `documents.inbox.list(params?)`          | GET    | `/documents/inbox`                           |
| `documents.inbox.get(id)`                | GET    | `/documents/inbox/{id}`                      |
| `documents.inbox.acknowledge(id)`        | POST   | `/documents/inbox/{id}/acknowledge`          |
| `documents.inbox.listAll(params?)`       | GET    | `/documents/inbox/all`                       |
| `peppol.lookup(scheme, id)`              | GET    | `/peppol/participants/{scheme}/{id}`         |
| `peppol.directory.search(params?)`       | GET    | `/peppol/directory/search`                   |
| `peppol.companyLookup(ico)`              | GET    | `/company/lookup/{ico}`                      |
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
| `webhooks.rotateSecret(id)`              | POST   | `/webhooks/{id}/rotate-secret`               |
| `webhooks.queue.pull(params?)`           | GET    | `/webhook-queue`                             |
| `webhooks.queue.ack(eventId)`            | DELETE | `/webhook-queue/{eventId}`                   |
| `webhooks.queue.batchAck(ids)`           | POST   | `/webhook-queue/batch-ack`                   |
| `webhooks.queue.pullAll(params?)`        | GET    | `/webhook-queue/all`                         |
| `webhooks.queue.batchAckAll(ids)`        | POST   | `/webhook-queue/all/batch-ack`               |
| `reporting.statistics(params?)`          | GET    | `/reporting/statistics`                      |
| `account.get()`                          | GET    | `/account`                                   |
| `extract.single(file, mime, name)`       | POST   | `/extract`                                   |
| `extract.batch(files)`                   | POST   | `/extract/batch`                             |

All paths relative to `https://epostak.sk/api/v1`.

---

## License

MIT
