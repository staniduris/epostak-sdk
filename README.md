# ePošťák SDK

Official SDKs for the [ePošťák Enterprise API](https://epostak.sk/api/docs/enterprise) — Peppol e-invoicing for Slovakia and the EU.

---

## Major Release

This release makes the public API workflow-first across all SDK languages.
Enterprise `/api/v1/*` calls live under `enterprise`; SAPI-SK `/sapi/v1/*`
calls stay separate and require a participant scope before document
operations.

- Enterprise direct firm flow: `client.enterprise.documents.send(...)`
- Enterprise ERP/integrator flow: `client.enterprise.connector.customers.for(...).submitDocument(...)`
- Enterprise API facade flow: `client.enterprise.payloads.validate(...)`, `client.enterprise.events.pull(...)`, `client.enterprise.documents.supportPacket(...)`
- ePošťák Box flow: `client.enterprise.box.list(...)`, `create({ payloadXml, ... })`, `schedule(...)`, `sendNow(...)`, `retry(...)`, `cancel(...)`
- SAPI-SK interoperable flow: `client.sapi.participants.for(...).documents.send(...)`

TypeScript is `4.0.0`. Python, PHP, Ruby, Java, and .NET are `1.0.0`.

---

## Enterprise API facade flow

For enterprise integrators, start with the facade helpers before dropping to raw
transport endpoints:

1. `payloads.validate` or `payloads.extract` prepares and checks the payload.
2. `documents.preflight` verifies Peppol capability before send.
3. `documents.send` sends with an idempotency key.
4. `events.pull` plus `events.batchAck` drains delivery events without an inbound webhook on day one.
5. `documents.supportPacket` exports the support/debug bundle for failed or disputed sends.

The older `webhooks.queue` resources remain available for compatibility, but
new ERP integrations should prefer `events`.

Non-breaking adoption: facade helpers are additive. Existing `/extract`,
`/documents/validate`, `/webhook-queue`, and
`/documents/{id}/evidence-bundle` integrations can keep running; migrate to
`payloads`, `events`, and `supportPacket` when your release window allows.

---

## Available SDKs

| Language | Directory | Package | Version | Status |
|-|-|-|-|-|
| TypeScript / JavaScript | [`typescript/`](./typescript/) | `@epostak/sdk` | 4.0.0 | `npm install @epostak/sdk` |
| Python | [`python/`](./python/) | `epostak` | 1.0.0 | Source on GitHub |
| PHP | [`php/`](./php/) | `epostak/sdk` | 1.0.0 | Source on GitHub |
| C# / .NET | [`dotnet/`](./dotnet/) | `EPostak` | 1.0.0 | Source on GitHub |
| Java | [`java/`](./java/) | `sk.epostak:epostak-sdk` | 1.0.0 | Source on GitHub |
| Ruby | [`ruby/`](./ruby/) | `epostak` | 1.0.0 | Source on GitHub |

TypeScript SDK is published on [npm](https://www.npmjs.com/package/@epostak/sdk). Other SDKs are available as source code — install directly from GitHub or copy into your project.

---

## Enterprise Direct Firm Flow

```typescript
import { EPostak } from "@epostak/sdk";

const client = new EPostak({
  clientId: "sk_live_xxxxx",
  clientSecret: "sk_live_xxxxx",
});

const result = await client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  receiverName: "Firma s.r.o.",
  invoiceNumber: "FV-2026-001",
  issueDate: "2026-04-04",
  dueDate: "2026-04-18",
  items: [
    { description: "Konzultácia", quantity: 10, unitPrice: 50, vatRate: 23 },
  ],
});
```

## Enterprise ERP/Integrator Flow

For ERP integrations, prefer `client.enterprise.connector` over raw HTTP. It exposes the
golden path directly: preflight, Mapper, Autopilot, stage, send, status, inbox,
ACK, reconcile, mailbox policy, sync, Connector document evidence, and action
execution.

Connector V2 calls such as Autopilot, Mapper, Zen input, mailbox, sync, Connector
documents, and actions use the integrator token plus `customerRef`; they do not
need `X-Firm-Id`. Legacy Connector calls such as preflight, send, outbox,
status, inbox, and events remain firm-scoped.

```typescript
const invoice = {
  receiverPeppolId: "0245:1234567890",
  document: {
    receiverName: "Firma s.r.o.",
    invoiceNumber: "FA-2026-001",
    issueDate: "2026-06-04",
    dueDate: "2026-06-18",
    items: [
      { description: "Služby", quantity: 1, unitPrice: 100, vatRate: 23 },
    ],
  },
};

const preflight = await client.enterprise.connector.preflight(invoice);
if (!preflight.ready) throw new Error(preflight.repairReport.summary);

const customer = client.enterprise.connector.customers.for("erp-customer-1");
const autopilot = await customer.submitDocument({
  mode: "shadow",
  externalId: "FA-2026-001",
  idempotencyKey: "erp-fa-2026-001",
  payload: invoice,
});
const exceptions = await client.enterprise.connector.reconcile({ status: "exceptions" });

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
const status = sent.documentId
  ? await client.enterprise.connector.status(sent.documentId)
  : null;

const inbox = await client.enterprise.connector.inbox({ limit: 20 });
for (const doc of inbox.documents) {
  await client.enterprise.connector.ack(doc.documentId);
}

const evidence = sent.documentId
  ? await client.enterprise.documents.evidence(sent.documentId)
  : null;
console.log(status?.status, evidence);
console.log(autopilot.lifecycleStatus, exceptions.total);
```

## SAPI-SK Interoperable Flow

SAPI calls use the same configured host but a separate `/sapi/v1` base path.
Document operations require a Peppol participant scope, which the SDK sends as
`X-Peppol-Participant-Id`.

```typescript
const sapi = client.sapi.participants.for("0245:1234567890");

await sapi.documents.send(
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

See [`typescript/README.md`](./typescript/README.md) for the copy-paste
examples and common sandbox error scenarios.

---

## Peppol Participant ID Format (Slovakia)

Per Slovak PASR requirements, Slovak participants are identified with a single Peppol scheme:

| Scheme | Identifier                       | Format            | Example           |
| ------ | -------------------------------- | ----------------- | ----------------- |
| `0245` | DIČ (daňové identifikačné číslo) | `0245:XXXXXXXXXX` | `0245:1234567890` |

Use `0245:DIČ` for all Slovak firms. The `9950:SK...` VAT-number form is **not** supported by ePošťák — PASR mandates a single canonical scheme.

---

## Authentication

| Key prefix  | Use case                                           |
| ----------- | -------------------------------------------------- |
| `sk_live_*` | Direct access — acts on behalf of your own firm    |
| `sk_int_*`  | Integrator access — acts on behalf of client firms |

Generate API keys in your ePošťák firm settings.

---

## Environments

Production remains the SDK default:

- Enterprise API: `https://epostak.sk/api/v1`
- SAPI: `https://epostak.sk/sapi/v1`
- OAuth origin: `https://epostak.sk`

For the test environment, pass an explicit override:

- Enterprise API: `https://dev.epostak.sk/api/v1`
- SAPI: `https://dev.epostak.sk/sapi/v1`
- OAuth origin: `https://dev.epostak.sk`

Do not change production clients when you want to test against `dev.epostak.sk`; set the SDK's `baseUrl`/`base_url`/`BaseUrl` override only in the test configuration. SAPI is derived from the same host by stripping `/api/v1`.

---

## OAuth `authorization_code` + PKCE (integrator onboarding)

For integrators onboarding end-user firms from inside their own UI, every SDK ships stateless `OAuth` helpers (`generatePkce`, `buildAuthorizeUrl`, `exchangeCode`) that hit `https://epostak.sk/oauth/authorize` and `https://epostak.sk/api/oauth/token` directly — they bypass the configured base URL because the OAuth namespace lives outside `/api/v1`. For test OAuth, pass `origin: "https://dev.epostak.sk"` to the helper. Generate a fresh PKCE pair per attempt, redirect the user to `/oauth/authorize`, then exchange the returned `code` for a new `sk_int_*` client secret and firm metadata. Pre-register your `redirect_uris` with `info@epostak.sk` (exact-match enforced).

```typescript
import { OAuth } from "@epostak/sdk";

const { codeVerifier, codeChallenge } = OAuth.generatePkce();
const url = OAuth.buildAuthorizeUrl({
  clientId: process.env.EPOSTAK_OAUTH_CLIENT_ID!,
  redirectUri: "https://your-app.com/oauth/epostak/callback",
  state: req.session.id,
  codeChallenge,
  // origin: "https://dev.epostak.sk", // optional test environment override
});
// later, on the callback:
const credentials = await OAuth.exchangeCode({
  code: req.query.code,
  codeVerifier,
  clientId: process.env.EPOSTAK_OAUTH_CLIENT_ID!,
  clientSecret: process.env.EPOSTAK_OAUTH_CLIENT_SECRET!,
  redirectUri: "https://your-app.com/oauth/epostak/callback",
  // origin: "https://dev.epostak.sk", // optional test environment override
});
```

Use this when the firm has no API key with you yet. Store the returned `client_id`, `client_secret`, and `firm_id`; use the `client_id`/`client_secret` with the regular `client.enterprise.auth.token({ clientId, clientSecret })` (`client_credentials`) flow to mint JWTs for `/api/v1/*` calls.

See [MIGRATION.md](./MIGRATION.md) for the old top-level naming to workflow-first namespace mapping.

---

## API Coverage

All SDKs cover the current Enterprise API and SAPI-SK 1.0 document flow:

- **Connector** — ERP workflow mode: preflight repair report, Connector Mapper, Zen input, Autopilot lifecycle, reconciliation exceptions, mailbox repair/send policy, sync cursors, Connector document lifecycle/UBL/evidence manifests, action execution, send, outbox stage/list/detail/send/batch/cancel, status, inbox list/detail, inbox ACK, and events
- **Box** — durable Box items: list, create with `payloadXml`, detail, schedule, send-now, retry, and cancel
- **Documents** — send, batch send, get, update, status, batch status, outbox, AS4 envelope, evidence, evidence bundle ZIP, PDF, UBL, respond, mark, parse, validate, preflight, convert, response list, event audit, Peppol document listing
- **Inbox** — list, get, acknowledge, cross-firm list (integrator)
- **Inbound / Outbound Pull API** — cursor-paginated document polling, UBL downloads, ACK, outbound events, raw AS4 MDN evidence
- **Peppol** — SMP lookup, directory search, company lookup/search, participant resolve, capability checks, batch participant lookup
- **Firms** — list, get, documents, register Peppol ID, assign, batch assign (integrator)
- **Webhooks** — CRUD, queued tests, delivery history, dead-letter queue replay/resolve, pull queue with single/batch acknowledge
- **Reporting** — aggregated statistics and submissions
- **Account** — firm info, plan, usage, license info
- **Extract** — AI-powered OCR from PDFs/images (single + batch)
- **SAPI** — `/sapi/v1/document/send`, receive list/detail, and acknowledge

---

## Integrator Mode

Use `sk_int_*` keys for multi-tenant access. Integrator-only endpoints:

| Method              | Description                          |
| ------------------- | ------------------------------------ |
| `firms.assign`      | Link a firm to the integrator by ICO |
| `firms.assignBatch` | Batch link firms (max 50)            |
| `inbox.listAll`     | Cross-firm inbox                     |
| `queue.pullAll`     | Cross-firm event queue               |
| `queue.batchAckAll` | Cross-firm batch acknowledge         |
| `integrator.keys`   | List and deactivate production integrator API keys |
| `integrator.licenses.info` | Aggregate usage across managed firms |

---

## Error Handling

All SDKs throw/raise a typed error (`EPostakError` / `EPostakException`) with:

- `status` — HTTP status code (0 for network errors)
- `code` — Machine-readable error code (e.g. `VALIDATION_ERROR`)
- `message` — Human-readable description
- `details` — Additional context (validation errors, etc.)

---

## Documentation

- [Enterprise API Docs](https://epostak.sk/api/docs/enterprise)
- Each SDK directory contains a detailed README with language-specific examples

---

## License

MIT
