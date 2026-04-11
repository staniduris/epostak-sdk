# @epostak/sdk

Official Node.js / TypeScript SDK for the [ePošťák Enterprise API](https://epostak.sk/api/docs/enterprise) — Peppol e-invoicing for Slovakia and the EU.

Zero runtime dependencies. Requires Node.js 18+.

---

## Installation

```bash
npm install @epostak/sdk
```

---

## Quick Start

```typescript
import { EPostak } from "@epostak/sdk";

const client = new EPostak({ apiKey: "sk_live_xxxxx" });

const result = await client.documents.send({
  receiverPeppolId: "0245:1234567890",
  invoiceNumber: "FV-2026-001",
  issueDate: "2026-04-04",
  dueDate: "2026-04-18",
  items: [
    { description: "Konzultácia", quantity: 10, unitPrice: 50, vatRate: 23 },
  ],
});
console.log(result.documentId, result.messageId);
```

---

## Peppol ID Formats (Slovakia)

| Scheme | Identifier              | Format              | Example             |
| ------ | ----------------------- | ------------------- | ------------------- |
| `0245` | DIČ                     | `0245:XXXXXXXXXX`   | `0245:1234567890`   |
| `9950` | IČ DPH (with SK prefix) | `9950:SKXXXXXXXXXX` | `9950:SK1234567890` |

---

## Authentication

| Key prefix  | Use case                                           |
| ----------- | -------------------------------------------------- |
| `sk_live_*` | Direct access — acts on behalf of your own firm    |
| `sk_int_*`  | Integrator access — acts on behalf of client firms |

```typescript
const client = new EPostak({
  apiKey: "sk_live_xxxxx",
  baseUrl: "https://...", // optional, defaults to https://epostak.sk/api/enterprise
  firmId: "uuid", // optional, required for integrator keys
});
```

---

## API Reference

### Documents

```typescript
// Send a document (JSON mode — UBL auto-generated)
const result = await client.documents.send({
  receiverPeppolId: "0245:1234567890",
  receiverName: "Firma s.r.o.",
  invoiceNumber: "FV-2026-001",
  issueDate: "2026-04-04",
  dueDate: "2026-04-18",
  currency: "EUR",
  items: [{ description: "Konzultácia", quantity: 10, unitPrice: 50, vatRate: 23 }],
});

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
const converted = await client.documents.convert({ direction: "json_to_ubl", data: { ... } });
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

### Peppol

```typescript
// SMP participant lookup
const participant = await client.peppol.lookup("0245", "1234567890");

// Peppol directory search
const results = await client.peppol.directory.search({
  q: "Telekom",
  country: "SK",
});

// Company lookup by ICO
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
const webhook = await client.webhooks.create({
  url: "https://example.com/webhook",
  events: ["document.received", "document.sent"],
});

const list = await client.webhooks.list();
const detail = await client.webhooks.get(webhook.id);
await client.webhooks.update(webhook.id, { isActive: false });
await client.webhooks.delete(webhook.id);
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
const stats = await client.reporting.statistics({
  from: "2026-01-01",
  to: "2026-03-31",
});
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
  apiKey: "sk_int_xxxxx",
  firmId: "client-firm-uuid",
});

// Option 2: withFirm() for switching
const base = new EPostak({ apiKey: "sk_int_xxxxx" });
const clientA = base.withFirm("firm-uuid-a");
const clientB = base.withFirm("firm-uuid-b");
```

---

## Error Handling

```typescript
import { EPostak, EPostakError } from "@epostak/sdk";

try {
  await client.documents.send({ ... });
} catch (err) {
  if (err instanceof EPostakError) {
    console.error(err.status);   // HTTP status (0 for network errors)
    console.error(err.code);     // e.g. 'VALIDATION_ERROR'
    console.error(err.message);  // Human-readable
    console.error(err.details);  // Validation details (422)
  }
}
```

---

## Full Endpoint Map

| Method                             | HTTP   | Path                                 |
| ---------------------------------- | ------ | ------------------------------------ |
| `documents.get(id)`                | GET    | `/documents/{id}`                    |
| `documents.update(id, body)`       | PATCH  | `/documents/{id}`                    |
| `documents.send(body)`             | POST   | `/documents/send`                    |
| `documents.status(id)`             | GET    | `/documents/{id}/status`             |
| `documents.evidence(id)`           | GET    | `/documents/{id}/evidence`           |
| `documents.pdf(id)`                | GET    | `/documents/{id}/pdf`                |
| `documents.ubl(id)`                | GET    | `/documents/{id}/ubl`                |
| `documents.respond(id, body)`      | POST   | `/documents/{id}/respond`            |
| `documents.validate(body)`         | POST   | `/documents/validate`                |
| `documents.preflight(body)`        | POST   | `/documents/preflight`               |
| `documents.convert(body)`          | POST   | `/documents/convert`                 |
| `documents.inbox.list(params?)`    | GET    | `/documents/inbox`                   |
| `documents.inbox.get(id)`          | GET    | `/documents/inbox/{id}`              |
| `documents.inbox.acknowledge(id)`  | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents.inbox.listAll(params?)` | GET    | `/documents/inbox/all`               |
| `peppol.lookup(scheme, id)`        | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol.directory.search(params?)` | GET    | `/peppol/directory/search`           |
| `peppol.companyLookup(ico)`        | GET    | `/company/lookup/{ico}`              |
| `firms.list()`                     | GET    | `/firms`                             |
| `firms.get(id)`                    | GET    | `/firms/{id}`                        |
| `firms.documents(id, params?)`     | GET    | `/firms/{id}/documents`              |
| `firms.registerPeppolId(id, body)` | POST   | `/firms/{id}/peppol-identifiers`     |
| `firms.assign(body)`               | POST   | `/firms/assign`                      |
| `firms.assignBatch(body)`          | POST   | `/firms/assign/batch`                |
| `webhooks.create(body)`            | POST   | `/webhooks`                          |
| `webhooks.list()`                  | GET    | `/webhooks`                          |
| `webhooks.get(id)`                 | GET    | `/webhooks/{id}`                     |
| `webhooks.update(id, body)`        | PATCH  | `/webhooks/{id}`                     |
| `webhooks.delete(id)`              | DELETE | `/webhooks/{id}`                     |
| `webhooks.queue.pull(params?)`     | GET    | `/webhook-queue`                     |
| `webhooks.queue.ack(eventId)`      | DELETE | `/webhook-queue/{eventId}`           |
| `webhooks.queue.batchAck(ids)`     | POST   | `/webhook-queue/batch-ack`           |
| `webhooks.queue.pullAll(params?)`  | GET    | `/webhook-queue/all`                 |
| `webhooks.queue.batchAckAll(ids)`  | POST   | `/webhook-queue/all/batch-ack`       |
| `reporting.statistics(params?)`    | GET    | `/reporting/statistics`              |
| `account.get()`                    | GET    | `/account`                           |
| `extract.single(file, mime)`       | POST   | `/extract`                           |
| `extract.batch(files)`             | POST   | `/extract/batch`                     |

All paths relative to `https://epostak.sk/api/enterprise`.

---

## License

MIT
