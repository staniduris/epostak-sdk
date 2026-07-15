# epostak

Official Python SDK for the [ePošťák API](https://epostak.sk/api/docs) — Peppol e-invoicing for Slovakia and the EU.

Requires Python 3.9+. One runtime dependency: [httpx](https://www.python-httpx.org/).

> **v1.1.0** — current workflow-first source release with the managed Connector
> surface. Enterprise `/api/v1/*` resources remain under `client.enterprise`;
> SAPI-SK document operations remain under
> `client.sapi.participants.for_participant(...)`.
> See [CHANGELOG.md](./CHANGELOG.md) and [../MIGRATION.md](../MIGRATION.md).

## Major release API shape

- Enterprise direct firm flow: `client.enterprise.documents.send(...)`
- Connector ERP/integrator flow: `client.connector.customers.for_customer("erp-customer").documents.send(...)`
- Enterprise API facade flow: `client.enterprise.payloads.validate(...)`, `client.enterprise.events.pull(...)`, `client.enterprise.documents.support_packet(...)`
- SAPI-SK interoperable flow: `client.sapi.participants.for_participant("0245:1234567890").documents.send(...)`

Connector is the recommended ERP path; Enterprise is the granular firm-scoped
API; SAPI-SK is the strict participant-scoped profile. ePošťák approves the
integrator, approves and Peppol-registers its firms, and issues Connector
credentials. The integrator then chooses and stores a stable `customerRef` for
each approved firm in the dashboard. The SDK cannot create or discover firms.

Request Connector access and Peppol firm approval at `integracie@epostak.sk`,
then set `customerRef` in the integrator dashboard. Connector omits
`X-Firm-Id`; SAPI always sends `X-Peppol-Participant-Id`.

Reference: [Connector guide](https://epostak.sk/api/docs/connector) and
[Connector OpenAPI](https://epostak.sk/api/openapi.connector.json).

## Connector quickstart

```python
import os
from epostak import EPostak

connector_client = EPostak(
    client_id=os.environ["EPOSTAK_CONNECTOR_CLIENT_ID"],
    client_secret=os.environ["EPOSTAK_CONNECTOR_CLIENT_SECRET"],
)
customer = connector_client.connector.customers.for_customer("erp-acme")
document = customer.documents.send({
    "externalId": "invoice-2026-0042",
    "type": "invoice",
    "number": "2026-0042",
    "recipient": {"country": "SK", "taxId": "2120123456"},
    "lines": [{"description": "Monthly licence", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
})
```

The integrator owns the stable `customerRef` after ePošťák has approved and
Peppol-registered the firm. The SDK cannot create or discover firms. It injects `customerRef`, sends
immediately by default, and derives a stable idempotency key from `customerRef`
and `externalId`.

```python
events = customer.events.list(limit=50)
configured_webhook = connector_client.connector.webhook.configure(
    "https://erp.example.com/webhooks/epostak",
    ["document.received", "document.delivered"],
)
if configured_webhook.get("secret"):
    # Persist this value in your server-side secret manager now. It is returned only once.
    one_time_webhook_secret = configured_webhook["secret"]
connector_client.connector.webhook.test("erp-acme")
```

Push uses the same canonical event item as polling with `customerRef` at the
root. `verify_webhook_signature` verifies HMAC-SHA256 over
`timestamp + "." + raw_body`.

### Acknowledge locally or respond to the supplier

```python
received_document_id = "document-id-from-list-or-event"
customer.documents.acknowledge(received_document_id, f"erp:{received_document_id}")
response = customer.documents.respond(received_document_id, {
    "status": "accepted",
    "note": "Imported and accepted",
})

# Direct alternative (do not call both); customerRef is still mandatory:
# connector_client.connector.documents.respond(
#     received_document_id, "erp-acme", {"status": "accepted"}
# )
```

`acknowledge` only records that the inbound document was processed locally; it
does not notify the supplier. `respond` sends the network business response via
`POST /connector/documents/{documentId}/respond?customerRef=...`. Use only the
business statuses `received`, `in_process`, `under_query`,
`conditionally_accepted`, `rejected`, `accepted`, or `paid`, with an optional
`note`. The Connector handles Peppol response codes and XML; do not send either.
The result reports `response.delivery` as `sent` or `queued` and marks safe
replays with `idempotent`.

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

### Included in v1.1.0 — 2026-07-14

- Connector is canonical at `client.connector`; the existing Enterprise
  namespace alias remains silently supported.
- ePošťák approves the integrator and Peppol firms; the integrator stores its
  own stable `customerRef` in the dashboard.
- Customer document creation snapshots the request, validates explicit
  1-255-byte idempotency keys, and retries network errors, `429`, and all `5xx`
  responses only when safe. Lifecycle calls are server-idempotent; `409` is
  never retried.
- `EPostakError` exposes `field`, `next_action`, `retryable`, `request_id`, and
  `retry_after`.
- `customerRef` and `externalId` use the backend ECMAScript `TrimString` code
  points before hashing; `U+0085` is preserved.
- `document.cancelled` is a first-class business event type.

### Included in v1.1.0 — 2026-07-12

- JSON billing payloads now expose the live receiver address, `prepaidAmount`,
  `prepayments`, and advanced line-item VAT/classification fields from the
  Enterprise OpenAPI.

### Included in v1.1.0 — 2026-07-01

- Enterprise API facade helpers for Payload Assistant validation, pull/ack
  event handling, and document support packets:
  `client.enterprise.payloads.validate(...)`,
  `client.enterprise.events.pull(...)`, and
  `client.enterprise.documents.support_packet(...)`.

### Included in v1.1.0 — 2026-06-30

- Customer-scoped
  `client.connector.customers.for_customer(customer_ref).advanced.mapper(...)`
  is the managed, preview-only Mapper flow. Top-level
  `client.connector.advanced.mapper(...)` remains a legacy compatibility alias
  and is unavailable with managed Connector credentials.
- `client.box` / `client.enterprise.box` covers ePošťák Box list, create with
  `payloadXml`, detail, schedule, send-now, retry, and cancel over
  `/box/items`.

### v1.0.0 — 2026-06-14

- `client.enterprise` — workflow-first namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces.
- `client.sapi.participants.for_participant(participant_id).documents` —
  participant-scoped SAPI document send/receive/get/acknowledge.
- `client.connector.customers.for_customer(customer_ref)` —
  customer-scoped Connector helper that injects `customerRef` and omits
  `X-Firm-Id`.
- The Connector compatibility surface includes preflight, Zen input, Autopilot,
  reconcile, mailbox, sync, document evidence, and event polling.
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

The Python SDK is currently distributed as reviewed source, not through PyPI:

```bash
git clone https://github.com/staniduris/epostak-sdk.git
python -m pip install ./epostak-sdk/python
```

---

## Quick start

```python
import os

from epostak import EPostak, EPostakError

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

Start from the stable `customerRef` configured by the integrator in the
dashboard after ePošťák approves and Peppol-registers the firm. There is no SDK
method for creating or discovering firms.

```python
connector_client = EPostak(
    client_id=os.environ["EPOSTAK_CONNECTOR_CLIENT_ID"],
    client_secret=os.environ["EPOSTAK_CONNECTOR_CLIENT_SECRET"],
)
customer = connector_client.connector.customers.for_customer("erp-customer-1")
document = customer.documents.send({
    "externalId": "FA-2026-001",
    "type": "invoice",
    "number": "FA-2026-001",
    "issueDate": "2026-07-14",
    "dueDate": "2026-07-28",
    "currency": "EUR",
    "recipient": {"country": "SK", "taxId": "2120123456"},
    "lines": [
        {"description": "Služby", "quantity": 1, "unitPrice": 100, "vatRate": 23},
    ],
})

page = customer.documents.list(state="needs_attention")
events = customer.events.list(limit=50)
for received in (item for item in page["documents"] if item["direction"] == "inbound"):
    customer.documents.acknowledge(received["id"], f"erp:{received['id']}")
ubl = customer.advanced.documents.ubl(document["id"])
evidence = customer.advanced.documents.evidence(document["id"])
evidence_bundle = customer.advanced.documents.evidence_bundle(document["id"])
support_packet = customer.advanced.documents.support_packet(document["id"])
print(document["id"], events["nextCursor"])
```

Every customer-scoped get, acknowledge, lifecycle, UBL, and evidence request
appends the URL-encoded `customerRef` query. The server verifies the document
against that exact approved firm and configured reference.

The SDK injects `customerRef`, omits `X-Firm-Id`, and applies the backend
ECMAScript `TrimString` set (`0009-000D`, `0020`, `00A0`, `1680`,
`2000-200A`, `2028-2029`, `202F`, `205F`, `3000`, `FEFF`) to `customerRef`
and `externalId`; `U+0085` is preserved. It then derives a stable bounded key
as `connector:v1:` plus SHA-256 of the two UTF-8 values, each prefixed by its
four-byte big-endian byte length. Explicit keys must be 1-255 UTF-8 bytes and
are otherwise unchanged. Use
`customer.documents.stage(...)` instead of `send(...)` when the ERP must hold a
document for review, then call `customer.documents.send_document(document_id)`
or `cancel_document(document_id)`.

Mapper is a customer-scoped preview/normalization helper; it never stages or
sends a document:

```python
preview = customer.advanced.mapper({
    "templateKey": "pohoda-csv-v1",
    "sourceType": "csv",
    "sourceText": csv_text,
})
normalized = preview["document"]
```

Managed Connector credentials support customer documents/events, one global
webhook, audited document evidence, and Mapper preview. Legacy preflight, raw send, Outbox,
Autopilot, reconciliation, mailbox, sync, and action helpers remain supported
source-compatibility aliases; availability depends on the credential's product
entitlement. The Connector property under the Enterprise namespace also
remains silently supported.

### Connector errors and retries

```python
try:
    customer.documents.send(invoice)
except EPostakError as error:
    print(error.code, error.message, error.field, error.next_action)
    print(error.retryable, error.request_id, error.retry_after)
```

Keyed document creation retries network failures, `429`, and every `5xx` while
reusing the exact body and key. Lifecycle send/cancel/acknowledge calls are
server-idempotent and use the same policy. `Retry-After` is honored; `409` is
always surfaced once and never retried automatically.

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
| `sk_int_*`  | Product-scoped integrator access; ePošťák approves Connector firms and the integrator configures `customerRef` |

```python
client = EPostak(
    client_id="sk_live_xxxxx",
    client_secret="sk_live_xxxxx",
    base_url="https://dev.epostak.sk/api/v1",  # optional test env; omit for prod
    firm_id="uuid",           # optional, for legacy firm-scoped calls
    max_retries=3,            # optional, exponential backoff with jitter
)
```

Managed Connector calls use Connector credentials and the integrator's stored
`customerRef` for an ePošťák-approved Peppol firm; they do not need `firm_id`.
Ordinary Enterprise credentials cannot provision or scout Connector firms.
Use `firm_id` or `with_firm(...)` only for Enterprise calls that target one firm
directly.

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

This section is only for Enterprise integrator credentials and firm-scoped
Enterprise calls. It does not provision Connector. Managed Connector uses its
own approved credentials; the integrator chooses each stable `customerRef` in
the dashboard after ePošťák approves the firm. Setting `firm_id` does not
change the Connector entitlement.

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
| `connector.customers.for_customer(ref).documents.send(body)`   | POST   | `/connector/documents`               |
| `connector.customers.for_customer(ref).documents.stage(body)`  | POST   | `/connector/documents`               |
| `connector.customers.for_customer(ref).documents.list(...)`    | GET    | `/connector/documents`               |
| `connector.customers.for_customer(ref).documents.get(id)`      | GET    | `/connector/documents/{documentId}`  |
| `connector.customers.for_customer(ref).documents.acknowledge(id, ref)` | POST | `/connector/documents/{documentId}/acknowledge` |
| `connector.customers.for_customer(ref).documents.respond(id, body)` | POST | `/connector/documents/{documentId}/respond` |
| `connector.customers.for_customer(ref).documents.send_document(id)` | POST | `/connector/documents/{documentId}/send` |
| `connector.customers.for_customer(ref).documents.cancel_document(id)` | POST | `/connector/documents/{documentId}/cancel` |
| `connector.customers.for_customer(ref).events.list(...)`       | GET    | `/connector/events`                  |
| `connector.customers.for_customer(ref).advanced.mapper(body)`  | POST   | `/connector/mapper`                  |
| `connector.customers.for_customer(ref).advanced.documents.ubl(id)` | GET | `/connector/documents/{documentId}/ubl` |
| `connector.customers.for_customer(ref).advanced.documents.evidence(id)` | GET | `/connector/documents/{documentId}/evidence` |
| `connector.customers.for_customer(ref).advanced.documents.evidence_bundle(id)` | GET | `/connector/documents/{documentId}/evidence-bundle` |
| `connector.customers.for_customer(ref).advanced.documents.support_packet(id)` | GET | `/connector/documents/{documentId}/support-packet` |

The remaining Connector rows are legacy compatibility helpers. They require a
separately entitled legacy/Enterprise context and are not available to managed
Connector credentials. Customer Mapper must be called through
`customer.advanced.mapper(...)` and is preview-only.

| Legacy compatibility method | Verb | Path |
| --- | --- | --- |
| `connector.advanced.preflight(body)`                           | POST   | `/connector/preflight`               |
| `connector.advanced.send(body, idempotency_key=...)`           | POST   | `/connector/send`                    |
| `connector.advanced.outbox.stage(body)`                        | POST   | `/connector/outbox`                  |
| `connector.advanced.outbox.list(**params)`                     | GET    | `/connector/outbox`                  |
| `connector.advanced.outbox.get(outbox_id)`                     | GET    | `/connector/outbox/{outboxId}`       |
| `connector.advanced.outbox.send(outbox_id, force=...)`         | POST   | `/connector/outbox/{outboxId}/send`  |
| `connector.advanced.outbox.send_batch(ids=..., limit=...)`     | POST   | `/connector/outbox/send`             |
| `connector.advanced.outbox.cancel(outbox_id)`                  | DELETE | `/connector/outbox/{outboxId}`       |
| `connector.advanced.mapper(body)`                              | POST   | `/connector/mapper`                  |
| `connector.advanced.zen_input(body)`                           | POST   | `/connector/zen-input`               |
| `connector.advanced.autopilot(body)`                           | POST   | `/connector/autopilot`               |
| `connector.advanced.get_autopilot_run(autopilot_id)`           | GET    | `/connector/autopilot/{autopilotId}` |
| `connector.advanced.send_autopilot_run(autopilot_id)`          | POST   | `/connector/autopilot/{autopilotId}/send` |
| `connector.advanced.reconcile(status=..., since=...)`          | GET    | `/connector/reconcile`               |
| `connector.advanced.mailboxes()`                               | GET    | `/connector/mailbox`                 |
| `connector.advanced.repair_mailbox(body=None)`                 | POST   | `/connector/mailbox/repair`          |
| `connector.advanced.update_mailbox_send_policy(customer_ref, body)` | PATCH | `/connector/mailbox/{customerRef}/send-policy` |
| `connector.advanced.sync(customer_ref=..., cursor=..., limit=...)` | GET | `/connector/sync`                    |
| `connector.get_document(document_id)`                          | GET    | `/connector/documents/{documentId}`  |
| `connector.get_document_ubl(document_id)`                      | GET    | `/connector/documents/{documentId}/ubl` |
| `connector.get_document_evidence(document_id)`                 | GET    | `/connector/documents/{documentId}/evidence` |
| `connector.get_document_evidence_bundle(document_id)`          | GET    | `/connector/documents/{documentId}/evidence-bundle` |
| `connector.get_document_support_packet(document_id)`           | GET    | `/connector/documents/{documentId}/support-packet` |
| `connector.documents.support_packet(document_id)`              | GET    | `/connector/documents/{documentId}/support-packet` |
| `connector.advanced.run_action(action_id, body=None)`          | POST   | `/connector/actions/{actionId}`      |
| `connector.advanced.status(document_id)`                       | GET    | `/connector/status/{documentId}`     |
| `connector.advanced.inbox(**params)`                           | GET    | `/connector/inbox`                   |
| `connector.advanced.get_inbox_document(document_id)`           | GET    | `/connector/inbox/{documentId}`      |
| `connector.advanced.ack(document_id)`                          | POST   | `/connector/inbox/{documentId}/ack`  |
| `connector.advanced.events(**params)`                          | GET    | `/connector/events`                  |
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
