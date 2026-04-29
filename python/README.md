# epostak

Official Python SDK for the [ePošťák API](https://epostak.sk/api/docs) — Peppol e-invoicing for Slovakia and the EU.

Requires Python 3.9+. One runtime dependency: [httpx](https://www.python-httpx.org/).

> **v2.0 — break-clean release.** All endpoints now live under
> `/api/v1` (Wave-5 namespace migration). New `auth` resource for the
> OAuth `client_credentials` flow, new `audit` resource, top-level
> `verify_webhook_signature` helper, and `Idempotency-Key` support on
> mutating endpoints. See [CHANGELOG.md](./CHANGELOG.md) for the full
> migration table.

---

## Installation

```bash
pip install epostak
```

---

## Quick start

```python
from epostak import EPostak

client = EPostak(api_key="sk_live_xxxxx")

result = client.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "invoiceNumber": "FV-2026-001",
    "issueDate": "2026-04-04",
    "dueDate": "2026-04-18",
    "items": [
        {"description": "Konzultácia", "quantity": 10, "unitPrice": 50, "vatRate": 23},
    ],
})
print(result["documentId"], result["messageId"], result["payload_sha256"])
```

---

## Peppol ID format (Slovakia)

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

```python
client = EPostak(
    api_key="sk_live_xxxxx",
    base_url="https://...",   # optional, defaults to https://epostak.sk/api/v1
    firm_id="uuid",           # optional, required for integrator keys
    max_retries=3,            # optional, exponential backoff with jitter
)
```

### OAuth `client_credentials` (for short-lived access tokens)

The API key can be exchanged for a 15-minute JWT access token + 30-day
rotating refresh token. Use this when you want to hand a token to a
worker fleet without distributing the long-lived key itself.

```python
tokens = client.auth.token(api_key="sk_live_xxxxx")
print(tokens["access_token"], tokens["expires_in"])  # 900s

# Before the access token expires:
renewed = client.auth.renew(refresh_token=tokens["refresh_token"])

# On logout / key rotation:
client.auth.revoke(
    token=tokens["refresh_token"],
    token_type_hint="refresh_token",
)
```

### Key introspection, rotation, IP allowlist

```python
status = client.auth.status()
print(status["key"]["prefix"], status["plan"]["name"], status["firm"]["peppolStatus"])

rotated = client.auth.rotate_secret()  # sk_live_* only
print(rotated["key"])  # store immediately — only returned once

client.auth.ip_allowlist.update(["192.168.1.0/24", "203.0.113.42"])
print(client.auth.ip_allowlist.get()["ip_allowlist"])
```

---

## API reference

### Documents

```python
# Send a document (JSON mode — UBL auto-generated)
result = client.documents.send(
    {
        "receiverPeppolId": "0245:1234567890",
        "receiverName": "Firma s.r.o.",
        "invoiceNumber": "FV-2026-001",
        "issueDate": "2026-04-04",
        "dueDate": "2026-04-18",
        "currency": "EUR",
        "items": [{"description": "Konzultácia", "quantity": 10, "unitPrice": 50, "vatRate": 23}],
    },
    # Optional: replay-safe send. Server returns 409 (idempotency_conflict)
    # if the same key is replayed before the original request finishes.
    idempotency_key="fv-2026-001-send",
)

# Send pre-built UBL XML
client.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "xml": '<?xml version="1.0"?>...',
})

# Get document by ID
doc = client.documents.get("doc-uuid")

# Update a draft
client.documents.update("doc-uuid", invoiceNumber="FV-2026-002", dueDate="2026-05-01")

# Status with full history
status = client.documents.status("doc-uuid")

# Delivery evidence (AS4, MLR, invoice response)
evidence = client.documents.evidence("doc-uuid")

# Download PDF / UBL XML
pdf = client.documents.pdf("doc-uuid")
ubl = client.documents.ubl("doc-uuid")

# Respond to received invoice (AP=accept, RE=reject, UQ=query)
client.documents.respond("doc-uuid", status="AP", note="Akceptované")

# Validate without sending
validation = client.documents.validate({"receiverPeppolId": "0245:1234567890", "items": [...]})

# Check receiver capability
check = client.documents.preflight("0245:1234567890")

# Convert between JSON and UBL
converted = client.documents.convert(
    input_format="json",
    output_format="ubl",
    document={"invoiceNumber": "FV-001", "items": [...]},
)
```

### Inbox

```python
# List received documents
inbox = client.documents.inbox.list(
    limit=20,
    status="RECEIVED",
    since="2026-04-01T00:00:00Z",
)

# Get full detail with UBL XML payload
detail = client.documents.inbox.get("doc-uuid")
print(detail["document"], detail["payload"])

# Acknowledge (mark as processed)
client.documents.inbox.acknowledge("doc-uuid")

# Cross-firm inbox (integrator only)
all_docs = client.documents.inbox.list_all(limit=50, firm_id="firm-uuid")
```

### Audit (per-firm security feed)

Cursor-paginated walk over `(occurred_at DESC, id DESC)`.

```python
cursor = None
while True:
    page = client.audit.list(
        event="jwt.issued",
        since="2026-04-01T00:00:00Z",
        cursor=cursor,
        limit=50,
    )
    for ev in page["items"]:
        print(ev["occurred_at"], ev["event"], ev["actor_id"])
    cursor = page.get("next_cursor")
    if not cursor:
        break
```

### Peppol

```python
participant = client.peppol.lookup("0245", "1234567890")

results = client.peppol.directory.search(q="Telekom", country="SK")

company = client.peppol.company_lookup("12345678")
```

### Firms (integrator)

```python
firms = client.firms.list()
firm = client.firms.get("firm-uuid")
docs = client.firms.documents("firm-uuid", limit=20, direction="inbound")
client.firms.register_peppol_id("firm-uuid", scheme="0245", identifier="1234567890")

# Assign firm by ICO
client.firms.assign(ico="12345678")
client.firms.assign_batch(icos=["12345678", "87654321"])
```

### Webhooks

```python
# Create webhook (store secret for HMAC verification!)
webhook = client.webhooks.create(
    url="https://example.com/webhook",
    events=["document.received", "document.sent"],
    idempotency_key="create-prod-webhook",
)

webhooks = client.webhooks.list()
detail = client.webhooks.get(webhook["id"])
client.webhooks.update(webhook["id"], is_active=False)
client.webhooks.delete(webhook["id"])

# Rotate the signing secret (issues a fresh one, invalidates the old).
rotated = client.webhooks.rotate_secret(webhook["id"])
print(rotated["secret"])
```

#### Verifying a delivery

```python
from flask import Flask, request
from epostak import verify_webhook_signature

app = Flask(__name__)

@app.post("/webhooks/epostak")
def hook():
    result = verify_webhook_signature(
        # IMPORTANT: hash the bytes off the wire — do NOT re-serialise the
        # parsed JSON, the round-trip will reorder keys and mutate whitespace.
        payload=request.get_data(),
        signature_header=request.headers.get("X-Epostak-Signature", ""),
        secret=WEBHOOK_SECRET,
        # tolerance_seconds=300,  # default — clamps replay attacks
    )
    if not result.valid:
        return f"bad signature: {result.reason}", 400
    event = request.get_json()
    # process event...
    return "", 204
```

### Webhook pull queue

```python
queue = client.webhooks.queue.pull(limit=50)
for item in queue["events"]:
    print(item["event_id"], item["event_type"], item["payload"])
    client.webhooks.queue.ack(item["event_id"])

# Batch acknowledge
client.webhooks.queue.batch_ack([e["event_id"] for e in queue["events"]])

# Cross-firm (integrator)
all_events = client.webhooks.queue.pull_all(limit=200)
client.webhooks.queue.batch_ack_all([e["event_id"] for e in all_events["events"]])
```

### Reporting

```python
# Convenience period selector
stats = client.reporting.statistics(period="month")
print(stats["sent"]["total"], stats["sent"]["by_type"])
print(stats["received"]["total"], stats["received"]["by_type"])
print(stats["delivery_rate"])  # e.g. 0.987
print(stats["top_recipients"])  # up to 5
print(stats["top_senders"])

# Or an explicit window
client.reporting.statistics(from_date="2026-01-01", to_date="2026-03-31")
```

### Account

```python
account = client.account.get()
print(account["firm"]["name"], account["plan"]["name"])
```

### Extract (AI OCR)

```python
# Single file
with open("invoice.pdf", "rb") as f:
    result = client.extract.single(f.read(), "application/pdf", "invoice.pdf")

# Batch (up to 10 files, server-side)
batch = client.extract.batch([
    {"file": pdf_bytes, "mime_type": "application/pdf", "file_name": "inv1.pdf"},
    {"file": img_bytes, "mime_type": "image/png", "file_name": "inv2.png"},
])
```

---

## Integrator mode

```python
# Option 1: firm_id in constructor
client = EPostak(api_key="sk_int_xxxxx", firm_id="client-firm-uuid")

# Option 2: with_firm() for switching
base = EPostak(api_key="sk_int_xxxxx")
client_a = base.with_firm("firm-uuid-a")
client_b = base.with_firm("firm-uuid-b")
```

---

## Error handling

`EPostakError` normalises both the legacy `{"error": {"code", "message"}}`
envelope and RFC 7807 `application/problem+json`.

```python
from epostak import EPostak, EPostakError

try:
    client.documents.send({...})
except EPostakError as err:
    print(err.status)         # HTTP status (0 for network errors)
    print(err.code)           # e.g. 'VALIDATION_FAILED'
    print(err.message)        # human-readable
    print(err.details)        # validation error list (422)
    print(err.request_id)     # from X-Request-Id (or body)
    print(err.title, err.detail, err.type, err.instance)  # RFC 7807

    if err.code == "idempotency_conflict":
        # Same Idempotency-Key still in flight server-side.
        pass
    if err.required_scope:
        # 403 with WWW-Authenticate: insufficient_scope
        print(f"Mint a token with scope: {err.required_scope}")
```

**Common error codes from `documents.send()`:**

| Status | Code                   | Meaning                                                                  |
| ------ | ---------------------- | ------------------------------------------------------------------------ |
| 409    | `idempotency_conflict` | Same `Idempotency-Key` is still in flight server-side. Retry shortly.    |
| 422    | `VALIDATION_FAILED`    | Document failed Peppol BIS 3.0 validation. `details` has the error list. |
| 502    | `SEND_FAILED`          | Peppol network temporarily unavailable. Retryable.                       |

---

## Full endpoint map

| Method                                                         | HTTP   | Path                                 |
| -------------------------------------------------------------- | ------ | ------------------------------------ |
| `auth.token(api_key, ...)`                                     | POST   | `/auth/token`                        |
| `auth.renew(refresh_token)`                                    | POST   | `/auth/renew`                        |
| `auth.revoke(token, ...)`                                      | POST   | `/auth/revoke`                       |
| `auth.status()`                                                | GET    | `/auth/status`                       |
| `auth.rotate_secret()`                                         | POST   | `/auth/rotate-secret`                |
| `auth.ip_allowlist.get()`                                      | GET    | `/auth/ip-allowlist`                 |
| `auth.ip_allowlist.update(cidrs)`                              | PUT    | `/auth/ip-allowlist`                 |
| `audit.list(**params)`                                         | GET    | `/audit`                             |
| `documents.get(id)`                                            | GET    | `/documents/{id}`                    |
| `documents.update(id, **fields)`                               | PATCH  | `/documents/{id}`                    |
| `documents.send(body, idempotency_key=...)`                    | POST   | `/documents/send`                    |
| `documents.send_batch(items, idempotency_key=...)`             | POST   | `/documents/send/batch`              |
| `documents.status(id)`                                         | GET    | `/documents/{id}/status`             |
| `documents.evidence(id)`                                       | GET    | `/documents/{id}/evidence`           |
| `documents.pdf(id)`                                            | GET    | `/documents/{id}/pdf`                |
| `documents.ubl(id)`                                            | GET    | `/documents/{id}/ubl`                |
| `documents.respond(id, status, note)`                          | POST   | `/documents/{id}/respond`            |
| `documents.validate(body)`                                     | POST   | `/documents/validate`                |
| `documents.preflight(receiver_peppol_id)`                      | POST   | `/documents/preflight`               |
| `documents.convert(...)`                                       | POST   | `/documents/convert`                 |
| `documents.inbox.list(**params)`                               | GET    | `/documents/inbox`                   |
| `documents.inbox.get(id)`                                      | GET    | `/documents/inbox/{id}`              |
| `documents.inbox.acknowledge(id)`                              | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents.inbox.list_all(**params)`                           | GET    | `/documents/inbox/all`               |
| `peppol.lookup(scheme, id)`                                    | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol.directory.search(**params)`                            | GET    | `/peppol/directory/search`           |
| `peppol.company_lookup(ico)`                                   | GET    | `/company/lookup/{ico}`              |
| `firms.list()`                                                 | GET    | `/firms`                             |
| `firms.get(id)`                                                | GET    | `/firms/{id}`                        |
| `firms.documents(id, **params)`                                | GET    | `/firms/{id}/documents`              |
| `firms.register_peppol_id(id, ...)`                            | POST   | `/firms/{id}/peppol-identifiers`     |
| `firms.assign(...)`                                            | POST   | `/firms/assign`                      |
| `firms.assign_batch(...)`                                      | POST   | `/firms/assign/batch`                |
| `webhooks.create(url, events, idempotency_key=...)`            | POST   | `/webhooks`                          |
| `webhooks.list()`                                              | GET    | `/webhooks`                          |
| `webhooks.get(id)`                                             | GET    | `/webhooks/{id}`                     |
| `webhooks.update(id, ...)`                                     | PATCH  | `/webhooks/{id}`                     |
| `webhooks.delete(id)`                                          | DELETE | `/webhooks/{id}`                     |
| `webhooks.rotate_secret(id)`                                   | POST   | `/webhooks/{id}/rotate-secret`       |
| `webhooks.queue.pull(**params)`                                | GET    | `/webhook-queue`                     |
| `webhooks.queue.ack(event_id)`                                 | DELETE | `/webhook-queue/{event_id}`          |
| `webhooks.queue.batch_ack(ids)`                                | POST   | `/webhook-queue/batch-ack`           |
| `webhooks.queue.pull_all(**params)`                            | GET    | `/webhook-queue/all`                 |
| `webhooks.queue.batch_ack_all(ids)`                            | POST   | `/webhook-queue/all/batch-ack`       |
| `reporting.statistics(period=..., from_date=..., to_date=...)` | GET    | `/reporting/statistics`              |
| `account.get()`                                                | GET    | `/account`                           |
| `extract.single(file, mime, name)`                             | POST   | `/extract`                           |
| `extract.batch(files)`                                         | POST   | `/extract/batch`                     |

All paths relative to `https://epostak.sk/api/v1`.

---

## License

MIT
