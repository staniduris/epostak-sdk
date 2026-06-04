# ePošťák SDK

Official SDKs for the [ePošťák Enterprise API](https://epostak.sk/api/docs/enterprise) — Peppol e-invoicing for Slovakia and the EU.

---

## Available SDKs

| Language | Directory | Package | Version | Status |
|-|-|-|-|-|
| TypeScript / JavaScript | [`typescript/`](./typescript/) | `@epostak/sdk` | 3.3.3 | `npm install @epostak/sdk` |
| Python | [`python/`](./python/) | `epostak` | 0.10.0 | Source on GitHub |
| PHP | [`php/`](./php/) | `epostak/sdk` | 0.10.0 | Source on GitHub |
| C# / .NET | [`dotnet/`](./dotnet/) | `EPostak` | 0.10.1 | Source on GitHub |
| Java | [`java/`](./java/) | `sk.epostak:epostak-sdk` | 0.10.0 | Source on GitHub |
| Ruby | [`ruby/`](./ruby/) | `epostak` | 0.10.0 | Source on GitHub |

TypeScript SDK is published on [npm](https://www.npmjs.com/package/@epostak/sdk). Other SDKs are available as source code — install directly from GitHub or copy into your project.

---

## Quick Start (TypeScript)

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
```

### Connector Workflow (ERP Golden Path)

For ERP integrations, prefer `client.connector` over raw HTTP. It exposes the
golden path directly: preflight, stage, send, status, inbox, ACK, and evidence
via `client.documents.evidence(...)`.

```typescript
const invoice = {
  receiverPeppolId: "0245:1234567890",
  document: {
    invoiceNumber: "FA-2026-001",
    issueDate: "2026-06-04",
    dueDate: "2026-06-18",
    items: [
      { description: "Služby", quantity: 1, unitPrice: 100, vatRate: 23 },
    ],
  },
};

const preflight = await client.connector.preflight(invoice);
if (!preflight.ready) throw new Error(preflight.repairReport.summary);

const staged = await client.connector.outbox.stage({
  items: [
    {
      externalId: "FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: invoice,
    },
  ],
});

const sent = await client.connector.outbox.send(staged.items[0].outboxId);
const status = sent.documentId
  ? await client.connector.status(sent.documentId)
  : null;

const inbox = await client.connector.inbox({ limit: 20 });
for (const doc of inbox.documents) {
  await client.connector.ack(doc.documentId);
}

const evidence = sent.documentId
  ? await client.documents.evidence(sent.documentId)
  : null;
console.log(status?.status, evidence);
```

See [`typescript/README.md`](./typescript/README.md) for the copy-paste
Connector quickstart and common sandbox error scenarios.

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

Use this when the firm has no API key with you yet. Store the returned `client_id`, `client_secret`, and `firm_id`; use the `client_id`/`client_secret` with the regular `client.auth.token({ clientId, clientSecret })` (`client_credentials`) flow to mint JWTs for `/api/v1/*` calls.

---

## API Coverage

All SDKs cover the current Enterprise API and SAPI-SK 1.0 document flow:

- **Connector** — ERP workflow mode: preflight repair report, send, outbox stage/list/detail/send/batch/cancel, status, inbox list/detail, inbox ACK, and events
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
