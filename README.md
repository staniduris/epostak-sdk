# ePošťák SDK

Official SDKs for the [ePošťák APIs](https://epostak.sk/api/docs) — managed
Connector workflows, the full Enterprise API, and SAPI-SK interoperability.

---

## Major Release

Connector is the recommended ERP path: business JSON, ordinary company or tax
identifiers, one stable `customerRef`, polling, and one global webhook. Use
Enterprise when you need granular firm-scoped protocol controls. Use SAPI-SK
when strict participant-scoped interoperability is the requirement.

- Enterprise direct firm flow: `client.enterprise.documents.send(...)`
- Connector ERP/integrator flow: `client.connector.customers.for(...).documents.send(...)`
- Enterprise API facade flow: `client.enterprise.payloads.validate(...)`, `client.enterprise.events.pull(...)`, `client.enterprise.documents.supportPacket(...)`
- ePošťák Box flow: `client.enterprise.box.list(...)`, `create({ payloadXml, ... })`, `schedule(...)`, `sendNow(...)`, `retry(...)`, `cancel(...)`
- SAPI-SK interoperable flow: `client.sapi.participants.for(...).documents.send(...)`

TypeScript is `4.1.0`. PHP is `1.1.1`. Python, Ruby, Java, and .NET are `1.1.0`.

---

## Enterprise API facade flow

For enterprise integrators, start with the facade helpers before dropping to raw
transport endpoints:

1. `payloads.validate` or `payloads.extract` prepares and checks the payload.
2. `documents.preflight` verifies Peppol capability before send.
3. `documents.send` sends with an idempotency key.
4. `events.pull` plus `events.batchAck` drains delivery events without an inbound webhook on day one.
5. `documents.supportPacket` exports the support/debug bundle for failed or disputed sends.

Deprecated SDK resource and method names remain available as source-compatibility adapters. They already call the canonical `payloads`, `events`, and `supportPacket` routes; they do not call the retired URLs.

The nine unused pre-launch alias URLs were removed on 20 July 2026. Raw HTTP clients must use `/payloads/*`, `/events/*`, and `/documents/{id}/support-packet`. Existing SDK calls through deprecated names keep working because those adapters already delegate to the canonical routes.

---

## Available SDKs

| Language | Directory | Package | Version | Status |
|-|-|-|-|-|
| TypeScript / JavaScript | [`typescript/`](./typescript/) | `@epostak/sdk` | 4.1.0 | `npm install @epostak/sdk@^4.1.0` |
| Python | [`python/`](./python/) | `epostak` | 1.1.0 | Source on GitHub |
| PHP | [`php/`](./php/) | `epostak/sdk` | 1.1.1 | Source on GitHub |
| C# / .NET | [`dotnet/`](./dotnet/) | `EPostak` | 1.1.0 | Source on GitHub |
| Java | [`java/`](./java/) | `sk.epostak:epostak-sdk` | 1.1.0 | Source on GitHub |
| Ruby | [`ruby/`](./ruby/) | `epostak` | 1.1.0 | Source on GitHub |

The npm release is ready to announce only after `npm view @epostak/sdk version`
returns `4.1.0` or newer; older npm releases do not contain this Connector
surface. The normal current install is `npm install @epostak/sdk@^4.1.0`, with a
local source fallback documented in [`typescript/README.md`](./typescript/README.md).
The other five SDKs are source-only until their registry releases are explicitly
published and verified; each language README contains a local-source install.

---

## Enterprise Direct Firm Flow

```typescript
import { EPostak } from "@epostak/sdk";

const client = new EPostak({
  clientId: "api-key-uuid-or-prefix",
  clientSecret: "sk_live_xxxxx", // full secret, distinct from clientId
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

Enterprise 1.6.0 structured JSON also supports `processId`, `documentType`
(`invoice`, `credit_note`, `self_billing`, `self_billing_credit_note`), the
self-billing `supplier*` aliases, and `precedingInvoiceRef` for credit notes.
Send responses expose idempotent-replay `duplicate` plus convenience `links`;
document/event payloads expose the canonical Peppol `process_id`.

## Connector ERP/Integrator Flow

ePošťák approves the integrator, approves its firms, and registers those firms
in Peppol. The integrator then chooses and stores a stable ERP `customerRef`
for each approved firm in the ePošťák dashboard. The SDK cannot create,
discover, or register firms. Firm Settings keys and Enterprise OAuth remain a
separate product surface.

Request Connector access and Peppol firm approval at `integracie@epostak.sk`.
After approval, set `customerRef` in the integrator dashboard before using this
flow.

Reference: [Connector guide](https://epostak.sk/api/docs/connector) and
[Connector OpenAPI](https://epostak.sk/api/openapi.connector.json).

```typescript
const connectorClient = new EPostak({
  clientId: process.env.EPOSTAK_CONNECTOR_CLIENT_ID!,
  clientSecret: process.env.EPOSTAK_CONNECTOR_CLIENT_SECRET!,
  baseUrl: "https://dev.epostak.sk/api/v1", // Connector sandbox
});
const customer = connectorClient.connector.customers.for("erp-customer-1");
const document = await customer.documents.send({
  externalId: "FA-2026-001",
  type: "invoice",
  number: "FA-2026-001",
  issueDate: "2026-07-14",
  dueDate: "2026-07-28",
  currency: "EUR",
  recipient: { country: "SK", taxId: "2120123456" },
  lines: [
    { description: "Služby", quantity: 1, unitPrice: 100, vatRate: 23 },
  ],
});
const receivedPage = await customer.documents.list({
  direction: "inbound",
  state: "received",
});
const events = await customer.events.list({ limit: 50 });
for (const received of receivedPage.documents) {
  await customer.documents.acknowledge(received.id, `erp:${received.id}`);
}
console.log(document.id, events.nextCursor);
```

Every customer-scoped point operation appends the URL-encoded `customerRef`
query parameter. The backend verifies that the document belongs to the
approved firm mapped to that integrator-owned reference.

### Inbound invoice lifecycle

Acknowledging and responding are deliberately separate operations:

```typescript
const receivedDocumentId = "document-id-from-list-or-event";
await customer.documents.acknowledge(receivedDocumentId, `erp:${receivedDocumentId}`);
const response = await customer.documents.respond(receivedDocumentId, {
  status: "accepted",
  note: "Imported and accepted",
});

// Direct alternative (do not call both); customerRef is explicit:
// await connectorClient.connector.documents.respond(
//   receivedDocumentId, "erp-customer-1", { status: "accepted" },
// );
```

`acknowledge` marks the inbound document as processed locally and never
notifies the supplier. `respond` sends a network business response through
`POST /connector/documents/{documentId}/respond?customerRef=...`. Its business
status is one of `received`, `in_process`, `under_query`,
`conditionally_accepted`, `rejected`, `accepted`, or `paid`; `note` is
optional. Integrators never provide Peppol response codes or XML. The result
reports delivery as `sent` or `queued` and marks safe replays as idempotent.

When no explicit idempotency key is supplied, every SDK applies the backend
ECMAScript `TrimString` contract to `customerRef` and `externalId`, then derives
the same 77-character key: `connector:v1:` plus lowercase SHA-256 of both UTF-8
values, each prefixed by its unsigned four-byte big-endian byte length. This
trims code points `0009-000D`, `0020`, `00A0`, `1680`, `2000-200A`, `2028-2029`,
`202F`, `205F`, `3000`, and `FEFF`; it deliberately preserves `U+0085`.
Explicit keys must contain 1-255 UTF-8 bytes and pass through unchanged.

The supported Connector golden-path surface is customer documents
(`send`, `stage`, `get`, `list`, `acknowledge`, `respond`, `sendDocument`,
`cancelDocument`) and customer events. Legacy preflight, raw send, Outbox,
Autopilot, reconciliation, mailbox, sync, and action aliases remain supported
for source compatibility. New examples use top-level `connector`; the existing
`enterprise.connector` alias remains silent and supported.

Configure one webhook for the whole integrator through `connector.webhook` or
poll each customer through `customer.events`. Webhook payloads use the same
canonical event item as polling and carry `customerRef` at the root. Verify
`X-Webhook-Signature` over `timestamp + "." + rawBody` with HMAC-SHA256.

```typescript
const configuredWebhook = await connectorClient.connector.webhook.configure(
  "https://erp.example.com/webhooks/epostak",
  ["document.received", "document.delivered"],
);
if (configuredWebhook.secret) {
  // Persist this value in your server-side secret manager now. It is returned only once.
  const oneTimeWebhookSecret = configuredWebhook.secret;
}
await connectorClient.connector.webhook.test("erp-customer-1");
```

### Connector advanced tools

Technical artifacts stay outside the golden path:

```typescript
const ubl = await customer.advanced.documents.ubl(document.id);
const evidence = await customer.advanced.documents.evidence(document.id);
const supportPacket = await customer.advanced.documents.supportPacket(document.id);
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
| `sk_int_*`  | Integrator access; exact capabilities depend on the provisioned product |

Enterprise keys are generated in ePošťák firm Settings. Connector integrator
credentials are issued by ePošťák only after manual approval; the integrator
chooses each `customerRef` after firm approval. Enterprise keys cannot be
reused to provision Connector access.

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

Use this when an Enterprise firm has no API key with you yet. Store the returned
`client_id`, `client_secret`, and `firm_id`; use the credentials with the regular
`client.enterprise.auth.token(...)` flow for Enterprise `/api/v1/*` calls.
This OAuth flow does not provision Connector credentials or customer links.

See [MIGRATION.md](./MIGRATION.md) for the old top-level naming to workflow-first namespace mapping.

---

## API Coverage

All SDKs cover the current Enterprise API and SAPI-SK 1.0 document flow:

- **Connector** — managed customer documents and lifecycle, customer events,
  document UBL/evidence, and Mapper preview/normalization
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

## Enterprise Integrator Mode

This section is only for Enterprise multi-tenant credentials and firm-scoped
Enterprise calls. It does not provision Connector credentials or customer
links. Enterprise-only integrator endpoints:

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
- `field` — Request field that needs correction, when supplied
- `nextAction` — Stable server guidance for the caller
- `retryable` — Whether the server classifies the failure as transient
- `requestId` — Correlation ID from the response body or header
- `retryAfter` — Parsed `Retry-After` delta seconds, when present

Connector document creation retries transient network failures, `429`, and all
`5xx` responses only when a non-empty `Idempotency-Key` was attached. Lifecycle
send/cancel/acknowledge calls are server-idempotent and follow the same retry
policy. The SDK honors `Retry-After`; `409` is always surfaced immediately and
is never retried automatically.

---

## Documentation

- [Connector guide](https://epostak.sk/api/docs/connector)
- [Connector OpenAPI](https://epostak.sk/api/openapi.connector.json)
- [Enterprise API Docs](https://epostak.sk/api/docs/enterprise)
- Each SDK directory contains a detailed README with language-specific examples

## Connector webhook debugger

The additive debugger API exposes the exact signed body, safe attempt evidence,
human-readable diagnosis, idempotent replay, and a deterministic seven-scenario
test suite. Existing Connector, Enterprise, and SAPI methods remain unchanged.

```typescript
const detail = await connectorClient.connector.webhook.getDelivery("delivery-id");
await connectorClient.connector.webhook.replayDelivery(
  detail.delivery.id,
  "erp:replay:delivery-id",
);
const run = await connectorClient.connector.webhook.runTestSuite(
  { customerRef: "erp-customer-1" },
  "erp:test-suite:1",
);
```

---

## License

MIT
