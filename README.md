# ePošťák SDK

Official SDKs for the [ePošťák Enterprise API](https://epostak.sk/api/docs/enterprise) — Peppol e-invoicing for Slovakia and the EU.

---

## Available SDKs

| Language                | Directory                      | Package                  | Version | Status                     |
| ----------------------- | ------------------------------ | ------------------------ | ------- | -------------------------- |
| TypeScript / JavaScript | [`typescript/`](./typescript/) | `@epostak/sdk`           | 3.0.0   | `npm install @epostak/sdk` |
| Python                  | [`python/`](./python/)         | `epostak`                | 3.0.0   | Source on GitHub           |
| PHP                     | [`php/`](./php/)               | `epostak/sdk`            | 3.0.0   | Source on GitHub           |
| C# / .NET               | [`dotnet/`](./dotnet/)         | `EPostak`                | 3.0.0   | Source on GitHub           |
| Java                    | [`java/`](./java/)             | `sk.epostak:epostak-sdk` | 3.0.0   | Source on GitHub           |
| Ruby                    | [`ruby/`](./ruby/)             | `epostak`                | 3.0.0   | Source on GitHub           |

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

## OAuth `authorization_code` + PKCE (integrator onboarding)

For integrators onboarding end-user firms from inside their own UI, every SDK ships stateless `OAuth` helpers (`generatePkce`, `buildAuthorizeUrl`, `exchangeCode`) that hit `https://epostak.sk/oauth/authorize` and `https://epostak.sk/api/oauth/token` directly — they bypass the configured base URL because the OAuth namespace lives outside `/api/v1`. Generate a fresh PKCE pair per attempt, redirect the user to `/oauth/authorize`, then exchange the returned `code` for a 15-minute access JWT and 30-day rotating refresh token. Pre-register your `redirect_uris` with `info@epostak.sk` (exact-match enforced).

```typescript
import { OAuth } from "@epostak/sdk";

const { codeVerifier, codeChallenge } = OAuth.generatePkce();
const url = OAuth.buildAuthorizeUrl({
  clientId: process.env.EPOSTAK_OAUTH_CLIENT_ID!,
  redirectUri: "https://your-app.com/oauth/epostak/callback",
  state: req.session.id,
  codeChallenge,
});
// later, on the callback:
const tokens = await OAuth.exchangeCode({
  code: req.query.code,
  codeVerifier,
  clientId: process.env.EPOSTAK_OAUTH_CLIENT_ID!,
  clientSecret: process.env.EPOSTAK_OAUTH_CLIENT_SECRET!,
  redirectUri: "https://your-app.com/oauth/epostak/callback",
});
```

Use this when the firm has no API key with you yet. Once linked, switch to the regular `client.auth.token({ clientId, clientSecret })` (`client_credentials`) flow.

---

## API Coverage

All SDKs cover the complete Enterprise API (35+ endpoints):

- **Documents** — send, get, update, status, evidence, PDF, UBL, respond, validate, preflight, convert
- **Inbox** — list, get, acknowledge, cross-firm list (integrator)
- **Peppol** — SMP lookup, directory search, company lookup by ICO
- **Firms** — list, get, documents, register Peppol ID, assign, batch assign (integrator)
- **Webhooks** — CRUD + pull queue with single/batch acknowledge
- **Reporting** — aggregated statistics
- **Account** — firm info, plan, usage
- **Extract** — AI-powered OCR from PDFs/images (single + batch)

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
