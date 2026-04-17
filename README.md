# ePošťák SDK

Official SDKs for the [ePošťák Enterprise API](https://epostak.sk/api/docs/enterprise) — Peppol e-invoicing for Slovakia and the EU.

---

## Available SDKs

| Language                | Directory                      | Package                  | Status                     |
| ----------------------- | ------------------------------ | ------------------------ | -------------------------- |
| TypeScript / JavaScript | [`typescript/`](./typescript/) | `@epostak/sdk`           | `npm install @epostak/sdk` |
| Python                  | [`python/`](./python/)         | `epostak`                | Source on GitHub           |
| PHP                     | [`php/`](./php/)               | `epostak/sdk`            | Source on GitHub           |
| C# / .NET               | [`dotnet/`](./dotnet/)         | `EPostak`                | Source on GitHub           |
| Java                    | [`java/`](./java/)             | `sk.epostak:epostak-sdk` | Source on GitHub           |
| Ruby                    | [`ruby/`](./ruby/)             | `epostak`                | Source on GitHub           |

TypeScript SDK is published on [npm](https://www.npmjs.com/package/@epostak/sdk). Other SDKs are available as source code — install directly from GitHub or copy into your project.

---

## Quick Start (TypeScript)

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
