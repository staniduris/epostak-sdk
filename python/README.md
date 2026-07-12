# epostak

Official Python SDK for the [ePošťák API](https://epostak.sk/api/docs) — Peppol e-invoicing for Slovakia and the EU.

Requires Python 3.9+. One runtime dependency: [httpx](https://www.python-httpx.org/).

> **v1.0.0** — breaking workflow-first release. Enterprise `/api/v1/*`
> resources are documented under `client.enterprise`; SAPI-SK document
> operations require `client.sapi.participants.for_participant(...)`.
> See [CHANGELOG.md](./CHANGELOG.md) and [../MIGRATION.md](../MIGRATION.md).

## Major release API shape

- Enterprise direct firm flow: `client.enterprise.documents.send(...)`
- Enterprise ERP/integrator flow: `client.enterprise.connector.customers.for_customer("erp-customer").submit_document(...)`
- Enterprise API facade flow: `client.enterprise.payloads.validate(...)`, `client.enterprise.events.pull(...)`, `client.enterprise.documents.support_packet(...)`
- SAPI-SK interoperable flow: `client.sapi.participants.for_participant("0245:1234567890").documents.send(...)`

Enterprise `firm_id` applies only to firm-scoped Enterprise calls. Connector
customer-scoped calls inject `customerRef` and omit `X-Firm-Id`. SAPI document
calls always send `X-Peppol-Participant-Id`.

## Enterprise API facade flow

Use this as the default low-friction path for ERP integrations that do not want
to receive webhooks on day one:

```python
client.enterprise.payloads.validate(invoice)

client.enterprise.documents.preflight(invoice["receiverPeppolId"])

sent = client.enterprise.documents.send(
    invoice,
    idempotency_key="erp-fa-2026-001",
)

events = client.enterprise.events.pull(limit=50)
client.enterprise.events.batch_ack([event["event_id"] for event in events["items"]])

support_packet = client.enterprise.documents.support_packet(sent["documentId"])
```

The older `client.enterprise.webhooks.queue` resource remains available for
compatibility. New pull-based integrations should prefer
`client.enterprise.events`.

Non-breaking adoption: facade helpers are additive. Existing
`client.enterprise.extract`, `client.enterprise.documents.validate`,
`client.enterprise.webhooks.queue`, and
`client.enterprise.documents.evidence_bundle` integrations can keep running;
migrate to `payloads`, `events`, and `support_packet` when your release window
allows.

## Recent changes

### Unreleased — 2026-07-12

- JSON billing payloads now expose the live receiver address, `prepaidAmount`,
  `prepayments`, and advanced line-item VAT/classification fields from the
  Enterprise OpenAPI.

### Unreleased — 2026-07-01

- Enterprise API facade helpers for Payload Assistant validation, pull/ack
  event handling, and document support packets:
  `client.enterprise.payloads.validate(...)`,
  `client.enterprise.events.pull(...)`, and
  `client.enterprise.documents.support_packet(...)`.

### Unreleased — 2026-06-30

- `client.connector.mapper(...)` and customer-scoped
  `client.enterprise.connector.customers.for_customer(customer_ref).mapper(...)`
  cover `/connector/mapper`.
- `client.box` / `client.enterprise.box` covers ePošťák Box list, create with
  `payloadXml`, detail, schedule, send-now, retry, and cancel over
  `/box/items`.

### v1.0.0 — 2026-06-14

- `client.enterprise` — workflow-first namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces.
- `client.sapi.participants.for_participant(participant_id).documents` —
  participant-scoped SAPI document send/receive/get/acknowledge.
- `client.enterprise.connector.customers.for_customer(customer_ref)` —
  customer-scoped Connector helper that injects `customerRef` and omits
  `X-Firm-Id`.
- `client.enterprise.connector` — Connector preflight, Zen input, Autopilot lifecycle, reconcile, mailbox policy, sync, Connector documents/UBL/evidence, action execution, send, outbox, status, inbox, ACK, and event polling.
- Docs: added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence.
- Static endpoint coverage expanded to 317 checks across TypeScript, Python, Ruby, PHP, .NET, and Java.

### v0.10.0 — 2026-05-18

- `client.sapi.participants.for_participant(id).documents` — SAPI-SK 1.0 send, receive list/detail, and acknowledge.
- `webhooks.test(id, event=None, count=None, mode=None)` — direct and queued webhook tests.
- `webhooks.deliveries(..., test_run_id=..., include_response_body=True)` — queued-test polling and response-body debugging.
- `webhooks.dead_letters()`, `replay_dead_letter(id)`, `resolve_dead_letter(id, reason=None)`.
- `peppol.resolve(...)` — resolve ERP identifiers to Peppol participant + routing capability.
- `documents.evidence_bundle`, `outbound.get_mdn`, `peppol.company_search`, `documents.peppol_documents`, and `account.license_info`.

### v0.9.0 — 2026-05-12

- `client.enterprise.pull.inbound` — Pull API for received documents: `list`, `get`, `get_ubl`, `ack`.
- `client.enterprise.pull.outbound` — Pull API for sent documents: `list`, `get`, `get_ubl`, `events`.
- `UblValidationError` — raised on 422 `UBL_VALIDATION_ERROR`; exposes `.rule` (e.g. `"BR-06"`). `UBL_RULES` constant tuple of the 7 known rule codes.
- `client.last_rate_limit` — `{"limit", "remaining", "reset_at"}` from `X-RateLimit-*` headers.
- `WebhookDelivery.idempotency_key` optional field.
- `webhooks.test(id, event=None)` now forwards `?event=` as a query param (matches server PR #114).

---

## Installation

```bash
pip install epostak
```

---

## Quick start

```python
from epostak import EPostak

client = EPostak(client_id="sk_live_xxxxx", client_secret="sk_live_xxxxx")

result = client.enterprise.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "receiverName": "Firma s.r.o.",
    "invoiceNumber": "FV-2026-001",
    "issueDate": "2026-04-04",
    "dueDate": "2026-04-18",
    "items": [
        {"description": "Konzultácia", "quantity": 10, "unitPrice": 50, "vatRate": 23},
    ],
})
print(result["documentId"], result["messageId"], result["payload_sha256"])
```

### Connector golden path for ERP developers

Use `client.enterprise.connector` for the ERP workflow instead of building raw HTTP calls.
The SDK handles OAuth token minting and refresh automatically after you create
the client.

```python
invoice = {
    "receiverPeppolId": "0245:1234567890",
    "document": {
        "receiverName": "Firma s.r.o.",
        "invoiceNumber": "FA-2026-001",
        "issueDate": "2026-06-04",
        "dueDate": "2026-06-18",
        "items": [
            {"description": "Služby", "quantity": 1, "unitPrice": 100, "vatRate": 23},
        ],
    },
}

preflight = client.enterprise.connector.preflight(invoice)
if not preflight["ready"]:
    print(preflight["repairReport"]["blocking"])
    raise RuntimeError("Invoice is not ready for Peppol delivery")

staged = client.enterprise.connector.stage_outbox({
    "items": [{
        "externalId": "FA-2026-001",
        "idempotencyKey": "erp-fa-2026-001",
        "payload": invoice,
    }],
})

sent = client.enterprise.connector.send_outbox_item(staged["items"][0]["outboxId"])
if not sent.get("documentId"):
    raise RuntimeError("Staged invoice was not sent")

status = client.enterprise.connector.status(sent["documentId"])

inbox = client.enterprise.connector.inbox(limit=20)
for doc in inbox["documents"]:
    client.enterprise.connector.ack(doc["documentId"])

# Evidence is shared with the Enterprise document API.
evidence = client.enterprise.documents.evidence(sent["documentId"])
print(status["status"], evidence)
```

For immediate send without staging:

```python
sent = client.enterprise.connector.send(invoice, idempotency_key="erp-fa-2026-001-send")
print(sent["documentId"], sent["status"])
```

Connector v2 Autopilot stores a durable lifecycle run and reconciliation gives
ERP sync jobs one place to read exceptions:

```python
run = client.enterprise.connector.autopilot({
    "customerRef": "erp-customer-1",
    "mode": "shadow",
    "externalId": "FA-2026-001",
    "idempotencyKey": "erp-fa-2026-001",
    "payload": invoice,
})
sent_run = client.enterprise.connector.send_autopilot_run(run["autopilotId"])
exceptions = client.enterprise.connector.reconcile(status="exceptions")
print(sent_run["lifecycleStatus"], exceptions["total"])
```

Customer-scoped Connector calls are the preferred integrator shape when you
already know the managed ERP customer:

```python
customer = client.enterprise.connector.customers.for_customer("erp-customer-1")
run = customer.submit_document({
    "externalId": "FA-2026-001",
    "idempotencyKey": "erp-fa-2026-001",
    "payload": invoice,
})
print(run["autopilotId"])
```

Common sandbox scenarios to test:

- nonexistent participant or unsupported document type: `preflight["ready"] is False` with blocking `repairReport` items
- invalid UBL or missing buyer/seller data: `preflight` or `send` returns validation details in the typed API error
- duplicate idempotency key: `409 idempotency_conflict`
- expired token: the SDK refreshes automatically; persistent auth failures surface as API errors
- received invoice processing: poll `client.enterprise.connector.inbox(...)`, store the payload, then call `client.enterprise.connector.ack(document_id)`

---

## SAPI-SK participant flow

```python
participant = client.sapi.participants.for_participant("0245:1234567890")
participant.documents.send({
    "metadata": {
        "documentId": "FA-2026-001",
        "documentTypeId": "invoice",
        "processId": "billing",
        "senderParticipantId": "0245:1234567890",
        "receiverParticipantId": "0245:0987654321",
        "creationDateTime": "2026-06-14T10:00:00Z",
    },
    "payload": "<Invoice/>",
    "payloadFormat": "XML",
}, idempotency_key="sapi-fa-2026-001")
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
    client_id="sk_live_xxxxx",
    client_secret="sk_live_xxxxx",
    base_url="https://dev.epostak.sk/api/v1",  # optional test env; omit for prod
    firm_id="uuid",           # optional, for legacy firm-scoped calls
    max_retries=3,            # optional, exponential backoff with jitter
)
```

Connector V2 integrator calls do not need `firm_id`; pass your ERP customer key
as `customerRef` on Connector requests. Use `firm_id` or `with_firm(...)` only
for legacy Enterprise API calls that target one firm directly.

Production is the SDK default: Enterprise `https://epostak.sk/api/v1`, SAPI
`https://epostak.sk/sapi/v1`, OAuth origin `https://epostak.sk`. For test
calls, set `base_url` to `https://dev.epostak.sk/api/v1`; SAPI derives
`https://dev.epostak.sk/sapi/v1`, and OAuth helpers need
`origin="https://dev.epostak.sk"` because OAuth is outside `/api/v1`.

### OAuth `client_credentials` (for short-lived access tokens)

The API key can be exchanged for a 15-minute JWT access token + 30-day
rotating refresh token. Use this when you want to hand a token to a
worker fleet without distributing the long-lived key itself.

```python
tokens = client.enterprise.auth.token(
    client_id="sk_live_xxxxx",
    client_secret="sk_live_xxxxx",
)
print(tokens["access_token"], tokens["expires_in"])  # 900s

# Before the access token expires:
renewed = client.enterprise.auth.renew(refresh_token=tokens["refresh_token"])

# On logout / key rotation:
client.enterprise.auth.revoke(
    token=tokens["refresh_token"],
    token_type_hint="refresh_token",
)
```

### Key introspection, rotation, IP allowlist

```python
status = client.enterprise.auth.status()
print(status["key"]["prefix"], status["plan"]["name"], status["firm"]["peppolStatus"])

rotated = client.enterprise.auth.rotate_secret()  # sk_live_* only
print(rotated["key"])  # store immediately — only returned once

client.enterprise.auth.ip_allowlist.update(["192.168.1.0/24", "203.0.113.42"])
print(client.enterprise.auth.ip_allowlist.get()["ip_allowlist"])
```

---

## API reference

### Documents

```python
# Send a document (JSON mode — UBL auto-generated)
result = client.enterprise.documents.send(
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
client.enterprise.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "xml": '<?xml version="1.0"?>...',
})

# Get document by ID
doc = client.enterprise.documents.get("doc-uuid")

# Update a draft
client.enterprise.documents.update("doc-uuid", invoiceNumber="FV-2026-002", dueDate="2026-05-01")

# Status with full history
status = client.enterprise.documents.status("doc-uuid")

# Delivery evidence (AS4, MLR, invoice response)
evidence = client.enterprise.documents.evidence("doc-uuid")

# Download PDF / UBL XML
pdf = client.enterprise.documents.pdf("doc-uuid")
ubl = client.enterprise.documents.ubl("doc-uuid")

# Respond to received invoice (AP=accept, RE=reject, UQ=query)
client.enterprise.documents.respond("doc-uuid", status="AP", note="Akceptované")

# Validate without sending
validation = client.enterprise.documents.validate({"receiverPeppolId": "0245:1234567890", "receiverName": "Firma s.r.o.", "items": [...]})

# Check receiver capability
check = client.enterprise.documents.preflight("0245:1234567890")

# Convert between JSON and UBL
converted = client.enterprise.documents.convert(
    input_format="json",
    output_format="ubl",
    document={"receiverName": "Firma s.r.o.", "invoiceNumber": "FV-001", "items": [...]},
)
```

### Inbox

```python
# List received documents
inbox = client.enterprise.documents.inbox.list(
    limit=20,
    status="RECEIVED",
    since="2026-04-01T00:00:00Z",
)

# Get full detail with UBL XML payload
detail = client.enterprise.documents.inbox.get("doc-uuid")
print(detail["document"], detail["payload"])

# Acknowledge (mark as processed)
client.enterprise.documents.inbox.acknowledge("doc-uuid")

# Cross-firm inbox (integrator only)
all_docs = client.enterprise.documents.inbox.list_all(limit=50, firm_id="firm-uuid")
```

### Audit (per-firm security feed)

Cursor-paginated walk over `(occurred_at DESC, id DESC)`.

```python
cursor = None
while True:
    page = client.enterprise.audit.list(
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
participant = client.enterprise.peppol.lookup("0245", "1234567890")
if participant.get("accepts") and participant.get("routingStatus") == "ready":
    # Receiver is registered and routable for the default BIS Billing invoice.
    pass

caps = client.enterprise.peppol.capabilities(
    "0245",
    "1234567890",
    document_type="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##...",
)

results = client.enterprise.peppol.directory.search(q="Telekom", country="SK")

company = client.enterprise.peppol.company_lookup("12345678")

matches = client.enterprise.peppol.company_search("Demo", limit=10)

resolved = client.enterprise.peppol.resolve(
    ico="12345678",
    document_type_id="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##...",
)
```

### Firms (integrator)

```python
firms = client.enterprise.firms.list()
firm = client.enterprise.firms.get("firm-uuid")
docs = client.enterprise.firms.documents("firm-uuid", limit=20, direction="inbound")
client.enterprise.firms.register_peppol_id("firm-uuid", scheme="0245", identifier="1234567890")

# Assign firm by ICO
client.enterprise.firms.assign(ico="12345678")
client.enterprise.firms.assign_batch(icos=["12345678", "87654321"])
```

### Webhooks

```python
# Create webhook (store secret for HMAC verification!)
webhook = client.enterprise.webhooks.create(
    url="https://example.com/webhook",
    events=["document.received", "document.sent"],
    idempotency_key="create-prod-webhook",
)

webhooks = client.enterprise.webhooks.list()
detail = client.enterprise.webhooks.get(webhook["id"])
client.enterprise.webhooks.update(webhook["id"], is_active=False)
client.enterprise.webhooks.delete(webhook["id"])

# Rotate the signing secret (issues a fresh one, invalidates the old).
rotated = client.enterprise.webhooks.rotate_secret(webhook["id"])
print(rotated["secret"])

queued = client.enterprise.webhooks.test(
    webhook["id"],
    event="document.received",
    count=250,
    mode="queued",
)
deliveries = client.enterprise.webhooks.deliveries(
    webhook["id"],
    test_run_id=queued["testRunId"],
    include_response_body=True,
)

dlq = client.enterprise.webhooks.dead_letters(include_response_body=True)
for failed in dlq["items"]:
    client.enterprise.webhooks.replay_dead_letter(failed["id"])
    # or: client.enterprise.webhooks.resolve_dead_letter(failed["id"], reason="Handled in ERP")
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
        signature=request.headers.get("x-webhook-signature", ""),
        timestamp=request.headers.get("x-webhook-timestamp", ""),
        secret=WEBHOOK_SECRET,
        # tolerance_seconds=300,  # default — clamps replay attacks
    )
    if not result.valid:
        return f"bad signature: {result.reason}", 400
    event = request.get_json()
    # process event...
    return "", 204
```

#### Dedup + retry headers (server v1.1 — 2026-05-12)

Three new headers on every push delivery:

| Header | Value | Use |
|-|-|-|
| `X-Webhook-Event-Id` | UUID, stable across retries | **Primary dedup key.** Body also carries it as `webhook_event_id`. |
| `X-Webhook-Attempt` | 1-based attempt number | Telemetry / logging. |
| `X-Webhook-Max-Attempts` | Total attempts in the retry window (10) | Telemetry / logging. |

Recommended receiver pattern:

```python
# INSERT ON CONFLICT DO NOTHING on the event id is enough — every retry
# of the same logical event carries the SAME X-Webhook-Event-Id.
event_id = request.headers["x-webhook-event-id"]
inserted = db.execute(
    "INSERT INTO processed_webhooks (event_id) VALUES (%s) "
    "ON CONFLICT (event_id) DO NOTHING RETURNING id",
    (event_id,),
).fetchone()
if inserted is None:
    return "", 200  # duplicate — ack and skip
# process event for the first time...
```

**Retry policy (server-side, as of 2026-05-12):** the server retries only on `408`, `425`, `429`, `502`, `503`, `504` and network errors (~44h bounded backoff). Returning any other 4xx/5xx — including `500` — terminates the retry loop immediately. If your handler hits an app-level error and you want a retry, return `503` (not `500`).

The signature contract is **unchanged** — `verify_webhook_signature` continues to work without code changes.

### Events pull facade

```python
queue = client.enterprise.events.pull(limit=50)
for item in queue["items"]:
    print(item["event_id"], item["event"], item["payload"])
    client.enterprise.events.ack(item["event_id"])

if queue["has_more"]:
    # more events waiting — pull again
    pass

# Batch acknowledge
client.enterprise.events.batch_ack([e["event_id"] for e in queue["items"]])
```

`client.enterprise.webhooks.queue` remains available for legacy queue and
cross-firm drain jobs.

### Reporting

```python
# Convenience period selector
stats = client.enterprise.reporting.statistics(period="month")
print(stats["sent"]["total"], stats["sent"]["by_type"])
print(stats["received"]["total"], stats["received"]["by_type"])
print(stats["delivery_rate"])  # e.g. 0.987
print(stats["top_recipients"])  # up to 5
print(stats["top_senders"])

# Or an explicit window
client.enterprise.reporting.statistics(from_date="2026-01-01", to_date="2026-03-31")
```

### Account

```python
account = client.enterprise.account.get()
print(account["firm"]["name"], account["plan"]["name"])
```

### Payload Assistant OCR

```python
# Single file
with open("invoice.pdf", "rb") as f:
    result = client.enterprise.payloads.extract(f.read(), "application/pdf", "invoice.pdf")

# Outbound invoices can include result["send_payload"] for review + validate + send.
if result.get("send_payload"):
    client.enterprise.payloads.validate(result["send_payload"])

# Batch (up to 50 files, server-side)
batch = client.enterprise.payloads.extract_batch([
    {"file": pdf_bytes, "mime_type": "application/pdf", "file_name": "inv1.pdf"},
    {"file": img_bytes, "mime_type": "image/png", "file_name": "inv2.png"},
])
```

`client.enterprise.extract` remains available for compatibility; new
integrations should use `client.enterprise.payloads.extract`.

---

## Integrator mode

Use this for legacy firm-scoped Enterprise API calls. Connector V2 calls
(`autopilot`, `mapper`, `zen_input`, mailbox, sync, Connector documents, actions) stay
integrator-scoped and resolve the managed firm from `customerRef`, so the SDK
does not send `X-Firm-Id` for those methods even if this client has `firm_id`.

```python
# Option 1: firm_id in constructor
client = EPostak(
    client_id="sk_int_xxxxx",
    client_secret="sk_int_xxxxx",
    firm_id="client-firm-uuid",
)

# Option 2: with_firm() for switching
base = EPostak(client_id="sk_int_xxxxx", client_secret="sk_int_xxxxx")
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
    client.enterprise.documents.send({...})
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
|-|-|-|
| `auth.token(client_id, client_secret, ...)`                    | POST   | `/auth/token`                        |
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
| `documents.status_batch(ids)`                                  | POST   | `/documents/status/batch`            |
| `documents.evidence(id)`                                       | GET    | `/documents/{id}/evidence`           |
| `documents.evidence_bundle(id)`                                | GET    | `/documents/{id}/evidence-bundle`    |
| `documents.support_packet(id)`                                 | GET    | `/documents/{id}/support-packet`     |
| `documents.envelope(id)`                                       | GET    | `/documents/{id}/envelope`           |
| `documents.pdf(id)`                                            | GET    | `/documents/{id}/pdf`                |
| `documents.ubl(id)`                                            | GET    | `/documents/{id}/ubl`                |
| `documents.respond(id, status, note)`                          | POST   | `/documents/{id}/respond`            |
| `documents.mark(id, state=..., note=...)`                      | POST   | `/documents/{id}/mark`               |
| `documents.validate(body)`                                     | POST   | `/documents/validate`                |
| `documents.preflight(receiver_peppol_id)`                      | POST   | `/documents/preflight`               |
| `documents.convert(...)`                                       | POST   | `/documents/convert`                 |
| `documents.parse(xml)`                                         | POST   | `/documents/parse`                   |
| `documents.outbox(**params)`                                   | GET    | `/documents/outbox`                  |
| `documents.responses(id)`                                      | GET    | `/documents/{id}/responses`          |
| `documents.events(id, **params)`                               | GET    | `/documents/{id}/events`             |
| `documents.peppol_documents(**params)`                         | GET    | `/peppol-documents`                  |
| `documents.inbox.list(**params)`                               | GET    | `/documents/inbox`                   |
| `documents.inbox.get(id)`                                      | GET    | `/documents/inbox/{id}`              |
| `documents.inbox.acknowledge(id)`                              | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents.inbox.list_all(**params)`                           | GET    | `/documents/inbox/all`               |
| `peppol.lookup(scheme, id)`                                    | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol.directory.search(**params)`                            | GET    | `/peppol/directory/search`           |
| `peppol.company_lookup(ico)`                                   | GET    | `/company/lookup/{ico}`              |
| `peppol.company_search(q, limit=None)`                         | GET    | `/company/search`                    |
| `peppol.resolve(**params)`                                     | GET    | `/peppol/participants/resolve`       |
| `peppol.capabilities(...)`                                     | POST   | `/peppol/capabilities`               |
| `peppol.lookup_batch(participants)`                            | POST   | `/peppol/participants/batch`         |
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
| `webhooks.test(id, event=None, count=None, mode=None)`         | POST   | `/webhooks/{id}/test`                |
| `webhooks.deliveries(id, **params)`                            | GET    | `/webhooks/{id}/deliveries`          |
| `webhooks.rotate_secret(id)`                                   | POST   | `/webhooks/{id}/rotate-secret`       |
| `webhooks.dead_letters(**params)`                              | GET    | `/webhook-dead-letter`               |
| `webhooks.replay_dead_letter(id)`                              | POST   | `/webhook-dead-letter/{id}/replay`   |
| `webhooks.resolve_dead_letter(id, reason=None)`                | POST   | `/webhook-dead-letter/{id}/resolve`  |
| `webhooks.queue.pull(**params)`                                | GET    | `/webhook-queue`                     |
| `webhooks.queue.ack(event_id)`                                 | DELETE | `/webhook-queue/{event_id}`          |
| `webhooks.queue.batch_ack(ids)`                                | POST   | `/webhook-queue/batch-ack`           |
| `webhooks.queue.pull_all(**params)`                            | GET    | `/webhook-queue/all`                 |
| `webhooks.queue.batch_ack_all(ids)`                            | POST   | `/webhook-queue/all/batch-ack`       |
| `payloads.extract(file, mime_type, file_name="document")`      | POST   | `/payloads/extract`                  |
| `payloads.extract_batch(files)`                                | POST   | `/payloads/extract/batch`            |
| `payloads.parse(xml)`                                          | POST   | `/payloads/parse`                    |
| `payloads.convert(input_format, output_format, document)`      | POST   | `/payloads/convert`                  |
| `payloads.validate(body)`                                      | POST   | `/payloads/validate`                 |
| `events.pull(**params)`                                        | GET    | `/events/pull`                       |
| `events.ack(event_id)`                                         | POST   | `/events/{eventId}/ack`              |
| `events.batch_ack(ids)`                                        | POST   | `/events/batch-ack`                  |
| `reporting.statistics(period=..., from_date=..., to_date=...)` | GET    | `/reporting/statistics`              |
| `reporting.submissions(limit=..., offset=..., report_type=...)` | GET   | `/reporting/submissions`             |
| `account.get()`                                                | GET    | `/account`                           |
| `account.license_info()`                                       | GET    | `/licenses/info`                     |
| `integrator.keys.list()`                                       | GET    | `/integrator/keys`                   |
| `integrator.keys.deactivate(key_id=..., client_id=...)`        | DELETE | `/integrator/keys`                   |
| `integrator.licenses.info(offset=..., limit=...)`              | GET    | `/integrator/licenses/info`          |
| `extract.single(file, mime, name)`                             | POST   | `/extract`                           |
| `extract.batch(files)`                                         | POST   | `/extract/batch`                     |
| `validate(xml)` / `client.validate(xml)`                       | POST   | `https://epostak.sk/api/validate`    |
| `box.list(**params)`                                           | GET    | `/box/items`                         |
| `box.create({"payloadXml": ...})`                              | POST   | `/box/items`                         |
| `box.get(item_id)`                                             | GET    | `/box/items/{itemId}`                |
| `box.schedule(item_id, scheduled_for)`                         | POST   | `/box/items/{itemId}/schedule`       |
| `box.send_now(item_id)`                                        | POST   | `/box/items/{itemId}/send-now`       |
| `box.retry(item_id)`                                           | POST   | `/box/items/{itemId}/retry`          |
| `box.cancel(item_id)`                                          | POST   | `/box/items/{itemId}/cancel`         |
| `connector.preflight(body)`                                    | POST   | `/connector/preflight`               |
| `connector.send(body, idempotency_key=...)`                    | POST   | `/connector/send`                    |
| `connector.stage_outbox(body)`                                 | POST   | `/connector/outbox`                  |
| `connector.list_outbox(**params)`                              | GET    | `/connector/outbox`                  |
| `connector.get_outbox_item(outbox_id)`                         | GET    | `/connector/outbox/{outboxId}`       |
| `connector.send_outbox_item(outbox_id, force=...)`             | POST   | `/connector/outbox/{outboxId}/send`  |
| `connector.send_outbox_batch(ids=..., limit=...)`              | POST   | `/connector/outbox/send`             |
| `connector.cancel_outbox_item(outbox_id)`                      | DELETE | `/connector/outbox/{outboxId}`       |
| `connector.mapper(body)`                                       | POST   | `/connector/mapper`                  |
| `connector.zen_input(body)`                                    | POST   | `/connector/zen-input`               |
| `connector.autopilot(body)`                                    | POST   | `/connector/autopilot`               |
| `connector.get_autopilot_run(autopilot_id)`                    | GET    | `/connector/autopilot/{autopilotId}` |
| `connector.send_autopilot_run(autopilot_id)`                   | POST   | `/connector/autopilot/{autopilotId}/send` |
| `connector.reconcile(status=..., since=...)`                   | GET    | `/connector/reconcile`               |
| `connector.mailboxes()`                                        | GET    | `/connector/mailbox`                 |
| `connector.repair_mailbox(body=None)`                          | POST   | `/connector/mailbox/repair`          |
| `connector.update_mailbox_send_policy(customer_ref, body)`      | PATCH  | `/connector/mailbox/{customerRef}/send-policy` |
| `connector.sync(customer_ref=..., cursor=..., limit=...)`       | GET    | `/connector/sync`                    |
| `connector.get_document(document_id)`                          | GET    | `/connector/documents/{documentId}`  |
| `connector.get_document_ubl(document_id)`                      | GET    | `/connector/documents/{documentId}/ubl` |
| `connector.get_document_evidence(document_id)`                 | GET    | `/connector/documents/{documentId}/evidence` |
| `connector.get_document_evidence_bundle(document_id)`          | GET    | `/connector/documents/{documentId}/evidence-bundle` |
| `connector.get_document_support_packet(document_id)`           | GET    | `/connector/documents/{documentId}/support-packet` |
| `connector.documents.support_packet(document_id)`              | GET    | `/connector/documents/{documentId}/support-packet` |
| `connector.run_action(action_id, body=None)`                   | POST   | `/connector/actions/{actionId}`      |
| `connector.status(document_id)`                                | GET    | `/connector/status/{documentId}`     |
| `connector.inbox(**params)`                                    | GET    | `/connector/inbox`                   |
| `connector.get_inbox_document(document_id)`                    | GET    | `/connector/inbox/{documentId}`      |
| `connector.ack(document_id)`                                   | POST   | `/connector/inbox/{documentId}/ack`  |
| `connector.events(**params)`                                   | GET    | `/connector/events`                  |
| `inbound.list(**params)`                                       | GET    | `/inbound/documents`                 |
| `inbound.get(id)`                                              | GET    | `/inbound/documents/{id}`            |
| `inbound.get_ubl(id)`                                          | GET    | `/inbound/documents/{id}/ubl`        |
| `inbound.ack(id, **params)`                                    | POST   | `/inbound/documents/{id}/ack`        |
| `outbound.list(**params)`                                      | GET    | `/outbound/documents`                |
| `outbound.get(id)`                                             | GET    | `/outbound/documents/{id}`           |
| `outbound.get_ubl(id)`                                         | GET    | `/outbound/documents/{id}/ubl`       |
| `outbound.get_mdn(id)`                                         | GET    | `/outbound/documents/{id}/mdn`       |
| `outbound.events(**params)`                                    | GET    | `/outbound/events`                   |
| `sapi.participants.for_participant(id).documents.send(body, idempotency_key=...)` | POST | `/sapi/v1/document/send` |
| `sapi.participants.for_participant(id).documents.receive(...)` | GET | `/sapi/v1/document/receive` |
| `sapi.participants.for_participant(id).documents.get(document_id)` | GET | `/sapi/v1/document/receive/{id}` |
| `sapi.participants.for_participant(id).documents.acknowledge(document_id)` | POST | `/sapi/v1/document/receive/{id}/acknowledge` |

Production Enterprise paths are relative to `https://epostak.sk/api/v1`; test Enterprise paths use `https://dev.epostak.sk/api/v1`. SAPI uses the same host, for example `https://epostak.sk/sapi/v1` or `https://dev.epostak.sk/sapi/v1`.

---

## License

MIT
