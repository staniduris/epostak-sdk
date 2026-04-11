# epostak

Official Python SDK for the [ePostak Enterprise API](https://epostak.sk/api/docs/enterprise) -- Peppol e-invoicing for Slovakia and the EU.

Requires Python 3.9+. Uses [httpx](https://www.python-httpx.org/) for HTTP.

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

# Send an invoice
result = client.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "invoiceNumber": "FV-2026-001",
    "issueDate": "2026-04-04",
    "dueDate": "2026-04-18",
    "items": [
        {"description": "Consulting", "quantity": 10, "unitPrice": 50, "vatRate": 23},
    ],
})
print(result["documentId"], result["messageId"])
```

---

## Authentication

| Key prefix  | Use case                                            |
| ----------- | --------------------------------------------------- |
| `sk_live_*` | Direct access -- acts on behalf of your own firm    |
| `sk_int_*`  | Integrator access -- acts on behalf of client firms |

### Constructor options

```python
client = EPostak(
    api_key="sk_live_xxxxx",             # Required
    base_url="https://...",              # Optional, defaults to https://epostak.sk/api/enterprise
    firm_id="uuid",                      # Optional, required for integrator keys
)
```

The client supports context managers for automatic cleanup:

```python
with EPostak(api_key="sk_live_xxxxx") as client:
    account = client.account.get()
```

---

## API Reference

### Documents

#### `documents.send(body)` -- Send a document via Peppol

**JSON mode** -- structured data, UBL XML is auto-generated:

```python
result = client.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "receiverName": "Firma s.r.o.",
    "receiverIco": "12345678",
    "receiverCountry": "SK",
    "invoiceNumber": "FV-2026-001",
    "issueDate": "2026-04-04",
    "dueDate": "2026-04-18",
    "currency": "EUR",
    "iban": "SK1234567890123456789012",
    "items": [
        {"description": "Consulting", "quantity": 10, "unitPrice": 50, "vatRate": 23},
    ],
})
# -> {"documentId": "...", "messageId": "...", "status": "SENT"}
```

**XML mode** -- send a pre-built UBL XML document:

```python
result = client.documents.send({
    "receiverPeppolId": "0245:1234567890",
    "xml": '<?xml version="1.0" encoding="UTF-8"?>...',
})
```

#### `documents.get(id)` -- Get a document by ID

```python
doc = client.documents.get("doc-uuid")
# -> {"id", "number", "status", "direction", "docType", "issueDate", ...}
```

#### `documents.update(id, **kwargs)` -- Update a draft document

Only documents with status `draft` can be updated. All fields are optional.

```python
updated = client.documents.update(
    "doc-uuid",
    invoiceNumber="FV-2026-002",
    dueDate="2026-05-01",
    items=[{"description": "Development", "quantity": 20, "unitPrice": 75, "vatRate": 23}],
)
```

#### `documents.status(id)` -- Full status with history

```python
status = client.documents.status("doc-uuid")
# -> {"id", "status", "documentType", "senderPeppolId", "receiverPeppolId",
#     "statusHistory": [{"status", "timestamp", "detail"}],
#     "validationResult", "deliveredAt", "acknowledgedAt",
#     "invoiceResponseStatus", "as4MessageId", "createdAt", "updatedAt"}
```

#### `documents.evidence(id)` -- Delivery evidence

```python
evidence = client.documents.evidence("doc-uuid")
# -> {"documentId", "as4Receipt", "mlrDocument", "invoiceResponse", "deliveredAt", "sentAt"}
```

#### `documents.pdf(id)` -- Download PDF as bytes

```python
pdf_bytes = client.documents.pdf("doc-uuid")
with open("invoice.pdf", "wb") as f:
    f.write(pdf_bytes)
```

#### `documents.ubl(id)` -- Download UBL XML as string

```python
ubl = client.documents.ubl("doc-uuid")
```

#### `documents.respond(id, status, note=None)` -- Send invoice response

Send an invoice response (accept, reject, or query) for a received document.

```python
result = client.documents.respond("doc-uuid", status="AP", note="Invoice accepted")
# -> {"documentId", "responseStatus", "respondedAt"}
```

#### `documents.validate(body)` -- Validate without sending

```python
validation = client.documents.validate({
    "receiverPeppolId": "0245:1234567890",
    "items": [{"description": "Test", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
})
# -> {"valid": True, "warnings": [], "ubl": "..."}
```

#### `documents.preflight(receiver_peppol_id, document_type_id=None)` -- Check receiver capability

```python
check = client.documents.preflight("0245:1234567890")
# -> {"receiverPeppolId", "registered", "supportsDocumentType", "smpUrl"}
```

#### `documents.convert(direction, data=None, xml=None)` -- Convert between JSON and UBL

```python
# JSON -> UBL
result = client.documents.convert(
    "json_to_ubl",
    data={"invoiceNumber": "FV-001", "items": [...]},
)

# UBL -> JSON
result = client.documents.convert(
    "ubl_to_json",
    xml='<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">...</Invoice>',
)
```

---

### Inbox

#### `documents.inbox.list(...)` -- List received documents

```python
inbox = client.documents.inbox.list(
    limit=20,                        # 1-100, default 20
    offset=0,
    status="RECEIVED",               # "RECEIVED" | "ACKNOWLEDGED"
    since="2026-04-01T00:00:00Z",    # ISO 8601
)
# -> {"documents": [...], "total", "limit", "offset"}
```

#### `documents.inbox.get(id)` -- Full detail with UBL XML

```python
detail = client.documents.inbox.get("doc-uuid")
print(detail["document"])  # Document dict
print(detail["payload"])   # UBL XML string or None
```

#### `documents.inbox.acknowledge(id)` -- Mark as processed

```python
result = client.documents.inbox.acknowledge("doc-uuid")
# -> {"documentId", "status": "ACKNOWLEDGED", "acknowledgedAt"}
```

#### `documents.inbox.list_all(...)` -- Cross-firm inbox (integrator)

List received documents across all firms linked to an integrator key.

```python
all_docs = client.documents.inbox.list_all(
    limit=50,                        # 1-200, default 50
    offset=0,
    status="RECEIVED",
    since="2026-04-01T00:00:00Z",
    firm_id="specific-firm-uuid",    # Optional filter
)
# -> {"documents": [...], "total", "limit", "offset"}
# Each document includes firm_id and firm_name
```

---

### Peppol

#### `peppol.lookup(scheme, identifier)` -- SMP participant lookup

```python
participant = client.peppol.lookup("0245", "12345678")
# -> {"peppolId", "name", "country", "capabilities": [{"documentTypeId", "processId", "transportProfile"}]}
```

#### `peppol.directory.search(...)` -- Business Card directory

```python
results = client.peppol.directory.search(q="Telekom", country="SK", page=0, page_size=20)
# -> {"results": [...], "total", "page", "page_size"}
```

#### `peppol.company_lookup(ico)` -- Slovak company lookup

```python
company = client.peppol.company_lookup("12345678")
# -> {"ico", "name", "dic", "icDph", "address", "peppolId"}
```

---

### Firms (integrator keys)

#### `firms.list()` -- List all accessible firms

```python
firms = client.firms.list()
# -> [{"id", "name", "ico", "peppolId", "peppolStatus"}, ...]
```

#### `firms.get(id)` -- Firm detail

```python
firm = client.firms.get("firm-uuid")
# -> {"id", "name", "ico", "peppolId", "peppolStatus", "dic", "icDph", "address",
#     "peppolIdentifiers": [{"scheme", "identifier"}], "createdAt"}
```

#### `firms.documents(id, ...)` -- List firm documents

```python
docs = client.firms.documents("firm-uuid", limit=20, direction="inbound")
```

#### `firms.register_peppol_id(id, scheme, identifier)` -- Register Peppol ID

```python
result = client.firms.register_peppol_id("firm-uuid", scheme="0245", identifier="12345678")
# -> {"peppolId", "scheme", "identifier", "registeredAt"}
```

#### `firms.assign(ico)` -- Assign firm to integrator

```python
result = client.firms.assign("12345678")
# -> {"firm": {"id", "name", "ico", "peppol_id", "peppol_status"}, "status": "active"}
```

#### `firms.assign_batch(icos)` -- Batch assign firms

```python
result = client.firms.assign_batch(["12345678", "87654321", "11223344"])
# -> {"results": [{"ico", "firm", "status", "error", "message"}, ...]}
```

---

### Webhooks

#### `webhooks.create(url, events=None)` -- Register a webhook

```python
webhook = client.webhooks.create(
    url="https://example.com/webhook",
    events=["document.received", "document.sent"],
)
# Store webhook["secret"] for HMAC-SHA256 signature verification
```

#### `webhooks.list()` -- List webhooks

```python
webhooks = client.webhooks.list()
```

#### `webhooks.get(id)` -- Webhook detail with deliveries

```python
detail = client.webhooks.get("webhook-uuid")
# -> {...webhook, "deliveries": [...]}
```

#### `webhooks.update(id, ...)` -- Update webhook

```python
client.webhooks.update(
    "webhook-uuid",
    url="https://example.com/new-webhook",
    events=["document.received"],
    is_active=True,
)
```

#### `webhooks.delete(id)` -- Delete webhook

```python
client.webhooks.delete("webhook-uuid")
```

---

### Webhook Pull Queue

Alternative to push webhooks -- poll for events.

#### `webhooks.queue.pull(...)` -- Fetch pending events

```python
queue = client.webhooks.queue.pull(limit=50, event_type="document.received")
for item in queue["items"]:
    print(item["id"], item["type"], item["payload"])
# -> {"items": [{"id", "type", "created_at", "payload"}], "has_more": bool}
```

#### `webhooks.queue.ack(event_id)` -- Acknowledge single event

```python
client.webhooks.queue.ack("event-uuid")
# Returns None (HTTP 204)
```

#### `webhooks.queue.batch_ack(event_ids)` -- Batch acknowledge

```python
ids = [item["id"] for item in queue["items"]]
client.webhooks.queue.batch_ack(ids)
# Returns None (HTTP 204)
```

#### `webhooks.queue.pull_all(...)` -- Cross-firm queue (integrator)

```python
queue = client.webhooks.queue.pull_all(limit=200, since="2026-04-01T00:00:00Z")
for event in queue["events"]:
    print(event["firm_id"], event["event"], event["payload"])
# -> {"events": [...], "count": int}
```

#### `webhooks.queue.batch_ack_all(event_ids)` -- Cross-firm batch ack (integrator)

```python
ids = [e["event_id"] for e in queue["events"]]
result = client.webhooks.queue.batch_ack_all(ids)
print(result["acknowledged"])  # number of events acknowledged
```

---

### Reporting

#### `reporting.statistics(...)` -- Aggregated stats

```python
stats = client.reporting.statistics(from_date="2026-01-01", to_date="2026-03-31")
# -> {"period": {"from", "to"},
#     "outbound": {"total", "delivered", "failed"},
#     "inbound": {"total", "acknowledged", "pending"}}
```

---

### Account

#### `account.get()` -- Account info

```python
account = client.account.get()
# -> {"firm": {"name", "ico", "peppolId", "peppolStatus"},
#     "plan": {"name", "status"},
#     "usage": {"outbound", "inbound"}}
```

---

### Extract (AI OCR)

Requires Enterprise plan.

#### `extract.single(file, mime_type, file_name=...)` -- Single file

```python
with open("invoice.pdf", "rb") as f:
    pdf_bytes = f.read()

result = client.extract.single(pdf_bytes, "application/pdf", "invoice.pdf")
# -> {"extraction": {...}, "ubl_xml": "...", "confidence": 0.95, "file_name": "invoice.pdf"}
```

Supported MIME types: `application/pdf`, `image/jpeg`, `image/png`, `image/webp`. Max 20 MB.

#### `extract.batch(files)` -- Batch extraction

Up to 10 files per request. Processed server-side in a single call.

```python
result = client.extract.batch([
    {"file": pdf_bytes, "mime_type": "application/pdf", "file_name": "inv1.pdf"},
    {"file": img_bytes, "mime_type": "image/png", "file_name": "inv2.png"},
])
# -> {"batch_id", "total", "successful", "failed",
#     "results": [{"file_name", "extraction", "ubl_xml", "confidence", "error"}, ...]}
```

---

## Integrator Mode

Use `sk_int_*` keys to act on behalf of client firms. Integrator keys unlock multi-tenant endpoints.

```python
# Option 1: pass firm_id in constructor
client = EPostak(api_key="sk_int_xxxxx", firm_id="client-firm-uuid")

# Option 2: scope at call time with with_firm()
base = EPostak(api_key="sk_int_xxxxx")
client_a = base.with_firm("firm-uuid-a")
client_b = base.with_firm("firm-uuid-b")

client_a.documents.send({...})
client_b.documents.inbox.list()
```

### Integrator-only endpoints

| Method                              | Description                            |
| ----------------------------------- | -------------------------------------- |
| `firms.assign(ico)`                 | Link a firm to the integrator          |
| `firms.assign_batch(icos)`          | Batch link firms (max 50)              |
| `documents.inbox.list_all()`        | Cross-firm inbox with `firm_id` filter |
| `webhooks.queue.pull_all()`         | Cross-firm event queue                 |
| `webhooks.queue.batch_ack_all(ids)` | Cross-firm batch acknowledge           |

---

## Error Handling

All errors are raised as `EPostakError`:

```python
from epostak import EPostak, EPostakError

try:
    client.documents.send({...})
except EPostakError as err:
    print(err.status)    # HTTP status code (0 for network errors)
    print(err.code)      # Machine-readable code, e.g. "VALIDATION_FAILED"
    print(err.message)   # Human-readable message
    print(err.details)   # Validation details (for 422 errors)
```

### Common error codes

| Status | Code                   | Meaning                                          |
| ------ | ---------------------- | ------------------------------------------------ |
| 400    | `BAD_REQUEST`          | Invalid request body or parameters               |
| 401    | `UNAUTHORIZED`         | Missing or invalid API key                       |
| 403    | `FORBIDDEN`            | Insufficient permissions or wrong plan           |
| 404    | `NOT_FOUND`            | Resource not found                               |
| 409    | `CONFLICT`             | Duplicate operation (e.g. firm already assigned) |
| 422    | `UNPROCESSABLE_ENTITY` | Validation failed (check `err.details`)          |
| 429    | `RATE_LIMITED`         | Too many requests                                |
| 503    | `SERVICE_UNAVAILABLE`  | Extraction service not configured                |

---

## Full API Endpoint Map

| SDK Method                                | HTTP   | Path                                 |
| ----------------------------------------- | ------ | ------------------------------------ |
| `documents.get(id)`                       | GET    | `/documents/{id}`                    |
| `documents.update(id, **kwargs)`          | PATCH  | `/documents/{id}`                    |
| `documents.send(body)`                    | POST   | `/documents/send`                    |
| `documents.status(id)`                    | GET    | `/documents/{id}/status`             |
| `documents.evidence(id)`                  | GET    | `/documents/{id}/evidence`           |
| `documents.pdf(id)`                       | GET    | `/documents/{id}/pdf`                |
| `documents.ubl(id)`                       | GET    | `/documents/{id}/ubl`                |
| `documents.respond(id, status, note)`     | POST   | `/documents/{id}/respond`            |
| `documents.validate(body)`                | POST   | `/documents/validate`                |
| `documents.preflight(receiver_peppol_id)` | POST   | `/documents/preflight`               |
| `documents.convert(direction, data, xml)` | POST   | `/documents/convert`                 |
| `documents.inbox.list(...)`               | GET    | `/documents/inbox`                   |
| `documents.inbox.get(id)`                 | GET    | `/documents/inbox/{id}`              |
| `documents.inbox.acknowledge(id)`         | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents.inbox.list_all(...)`           | GET    | `/documents/inbox/all`               |
| `peppol.lookup(scheme, identifier)`       | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol.directory.search(...)`            | GET    | `/peppol/directory/search`           |
| `peppol.company_lookup(ico)`              | GET    | `/company/lookup/{ico}`              |
| `firms.list()`                            | GET    | `/firms`                             |
| `firms.get(id)`                           | GET    | `/firms/{id}`                        |
| `firms.documents(id, ...)`                | GET    | `/firms/{id}/documents`              |
| `firms.register_peppol_id(id, ...)`       | POST   | `/firms/{id}/peppol-identifiers`     |
| `firms.assign(ico)`                       | POST   | `/firms/assign`                      |
| `firms.assign_batch(icos)`                | POST   | `/firms/assign/batch`                |
| `webhooks.create(url, events)`            | POST   | `/webhooks`                          |
| `webhooks.list()`                         | GET    | `/webhooks`                          |
| `webhooks.get(id)`                        | GET    | `/webhooks/{id}`                     |
| `webhooks.update(id, ...)`                | PATCH  | `/webhooks/{id}`                     |
| `webhooks.delete(id)`                     | DELETE | `/webhooks/{id}`                     |
| `webhooks.queue.pull(...)`                | GET    | `/webhook-queue`                     |
| `webhooks.queue.ack(event_id)`            | DELETE | `/webhook-queue/{event_id}`          |
| `webhooks.queue.batch_ack(ids)`           | POST   | `/webhook-queue/batch-ack`           |
| `webhooks.queue.pull_all(...)`            | GET    | `/webhook-queue/all`                 |
| `webhooks.queue.batch_ack_all(ids)`       | POST   | `/webhook-queue/all/batch-ack`       |
| `reporting.statistics(...)`               | GET    | `/reporting/statistics`              |
| `account.get()`                           | GET    | `/account`                           |
| `extract.single(file, mime_type)`         | POST   | `/extract`                           |
| `extract.batch(files)`                    | POST   | `/extract/batch`                     |

All paths are relative to `https://epostak.sk/api/enterprise`.

---

## Full API Documentation

https://epostak.sk/api/docs/enterprise
