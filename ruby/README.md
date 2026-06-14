# epostak

Ruby SDK for the [ePosťák](https://epostak.sk) Enterprise API — send and receive e-invoices via the Slovak Peppol network.

## Major release API shape

Ruby `1.0.0` is a breaking workflow-first release:

- Enterprise direct firm flow: `client.enterprise.documents.send(...)`
- Enterprise ERP/integrator flow: `client.enterprise.connector.customers.for_customer("erp-customer").submit_document(...)`
- SAPI-SK interoperable flow: `client.sapi.participants.for_participant("0245:1234567890").documents.send(...)`

Enterprise `firm_id` applies only to firm-scoped Enterprise calls. Connector
customer-scoped calls inject `customerRef` and omit `X-Firm-Id`. SAPI document
calls always send `X-Peppol-Participant-Id`.

## Recent changes

**v1.0.0** (2026-06-14)
- `client.enterprise` is the documented namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces
- `client.sapi.participants.for_participant(participant_id).documents` requires
  participant scoping before SAPI document send/receive/get/acknowledge
- `client.enterprise.connector.customers.for_customer(customer_ref)` injects
  `customerRef` and keeps `X-Firm-Id` off customer-managed Connector calls
- `client.enterprise.connector` covers Connector preflight, Zen input, Autopilot lifecycle, reconcile, mailbox policy, sync, Connector documents/UBL/evidence, action execution, send, outbox, status, inbox, ACK, and event polling
- Docs: added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence
- `client.enterprise.documents.status_batch(ids)` covers `POST /documents/status/batch` for up to 100 document IDs
- `client.enterprise.reporting.submissions(...)` covers `GET /reporting/submissions`
- `client.enterprise.integrator.keys.list` and `deactivate(...)` cover the production `GET`/`DELETE /integrator/keys` surface
- README environment data now lists production (`https://epostak.sk`) and test (`https://dev.epostak.sk`) Enterprise, SAPI, and OAuth origins

**v0.10.0** (2026-05-18)
- SAPI-SK 1.0 document flow via `client.sapi.participants.for_participant(id).documents`
- Enterprise evidence downloads: `documents.evidence_bundle` and `outbound.get_mdn`
- Peppol additions: `company_search` and `resolve`
- Webhook queued tests: `webhooks.test(id, count:, mode:)`
- Webhook dead-letter queue: `dead_letters`, `replay_dead_letter`, `resolve_dead_letter`
- License and Peppol document listing helpers

**v0.9.0** (2026-05-12)
- Pull API: `client.enterprise.pull.inbound` (list/get/get_ubl/ack) and `client.enterprise.pull.outbound` (list/get/get_ubl/events)
- `EPostak::UblValidationError` raised on 422 UBL_VALIDATION_ERROR with `.rule` attr
- `client.last_rate_limit` exposes limit/remaining/reset_at after first request
- `webhooks.test` now forwards `event:` kwarg to the server
- `WebhookDelivery` data class gains optional `idempotency_key`; new `InboundDocument`, `OutboundDocument`, `OutboundEvent` data classes with `.from_hash`

## Installation

Add to your Gemfile:

```ruby
gem "epostak"
```

Or install directly:

```bash
gem install epostak
```

## Quick start

```ruby
require "epostak"

client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "your_secret")

# Send an invoice
result = client.enterprise.documents.send(
  receiverPeppolId: "0245:1234567890",
  invoiceNumber: "FV-2026-042",
  items: [
    { description: "Web development", quantity: 40, unit: "HUR", unitPrice: 80, vatRate: 23 }
  ]
)

puts result["documentId"]
```

Connector workflow mode for ERP teams:

```ruby
invoice = {
  receiverPeppolId: "0245:1234567890",
  document: {
    invoiceNumber: "FA-2026-001",
    issueDate: "2026-06-04",
    dueDate: "2026-06-18",
    items: [
      { description: "Služby", quantity: 1, unitPrice: 100, vatRate: 23 }
    ]
  }
}

preflight = client.enterprise.connector.preflight(invoice)
unless preflight["ready"]
  warn preflight.dig("repairReport", "blocking").inspect
  raise "Invoice is not ready for Peppol delivery"
end

staged = client.enterprise.connector.stage_outbox(
  items: [
    {
      externalId: "FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: invoice
    }
  ]
)

sent = client.enterprise.connector.send_outbox_item(staged["items"].first["outboxId"])
raise "Staged invoice was not sent" if sent["documentId"].nil?

status = client.enterprise.connector.status(sent["documentId"])

inbox = client.enterprise.connector.inbox(limit: 20)
inbox["documents"].each do |doc|
  client.enterprise.connector.ack(doc["documentId"])
end

# Evidence is shared with the Enterprise document API.
evidence = client.enterprise.documents.evidence(sent["documentId"])
puts "#{status["status"]} #{evidence["documentId"]}"
```

For immediate send without staging:

```ruby
sent = client.enterprise.connector.send_document(invoice, idempotency_key: "erp-fa-2026-001-send")
puts "#{sent["documentId"]} #{sent["status"]}"
```

Connector v2 Autopilot stores a durable lifecycle run and reconciliation gives
ERP sync jobs one place to read exceptions:

```ruby
run = client.enterprise.connector.autopilot(
  customerRef: "erp-customer-1",
  mode: "shadow",
  externalId: "FA-2026-001",
  idempotencyKey: "erp-fa-2026-001",
  payload: invoice
)
sent_run = client.enterprise.connector.send_autopilot_run(run["autopilotId"])
exceptions = client.enterprise.connector.reconcile(status: "exceptions")
puts "#{sent_run["lifecycleStatus"]} #{exceptions["total"]}"
```

Customer-scoped Connector calls are the preferred integrator shape when you
already know the managed ERP customer:

```ruby
customer = client.enterprise.connector.customers.for_customer("erp-customer-1")
run = customer.submit_document(
  externalId: "FA-2026-001",
  idempotencyKey: "erp-fa-2026-001",
  payload: invoice
)
puts run["autopilotId"]
```

Common sandbox scenarios to test:

- nonexistent participant or unsupported document type: `preflight["ready"] == false` with blocking `repairReport` items
- invalid UBL or missing buyer/seller data: `preflight` or `send` returns validation details in `EPostak::Error`
- duplicate idempotency key: `409 idempotency_conflict`
- expired token: the SDK refreshes automatically; persistent auth failures surface as API errors
- received invoice processing: poll `client.enterprise.connector.inbox(...)`, store the payload, then call `client.enterprise.connector.ack(document_id)`

Batch workers can send queued items with:

```ruby
client.enterprise.connector.send_outbox_batch(limit: 50)
```

## SAPI-SK participant flow

```ruby
participant = client.sapi.participants.for_participant("0245:1234567890")
participant.documents.send(
  {
    metadata: {
      documentId: "FA-2026-001",
      documentTypeId: "invoice",
      processId: "billing",
      senderParticipantId: "0245:1234567890",
      receiverParticipantId: "0245:0987654321",
      creationDateTime: "2026-06-14T10:00:00Z"
    },
    payload: "<Invoice/>",
    payloadFormat: "XML"
  },
  idempotency_key: "sapi-fa-2026-001"
)
```

## Authentication

The SDK supports two types of API keys:

- **Direct keys** (`sk_live_*`) — single firm, all requests scoped to your firm
- **Integrator keys** (`sk_int_*`) — multi-tenant; Connector V2 uses `customerRef`, while legacy firm-scoped calls use `with_firm` or `X-Firm-Id`

```ruby
# Direct key
client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "your_secret")

# Integrator key — scope to a specific firm
integrator = EPostak::Client.new(client_id: "sk_int_xxxxx", client_secret: "your_secret")
firm_a = integrator.with_firm("firm-a-uuid")
firm_a.enterprise.documents.inbox.list
```

## Configuration

```ruby
client = EPostak::Client.new(
  client_id: "sk_live_xxxxx", client_secret: "your_secret",
  base_url: "https://dev.epostak.sk/api/v1",  # optional test env; omit for prod
  firm_id: "firm-uuid"                        # optional, for legacy firm-scoped calls
)
```

Connector V2 integrator calls do not need `firm_id`; pass your ERP customer key
as `customerRef` on Connector requests. The SDK omits `X-Firm-Id` for those
methods even when the client has `firm_id`.

Production is the SDK default: Enterprise `https://epostak.sk/api/v1`, SAPI
`https://epostak.sk/sapi/v1`, OAuth origin `https://epostak.sk`. For test
calls, set `base_url` to `https://dev.epostak.sk/api/v1`; SAPI derives
`https://dev.epostak.sk/sapi/v1`, and OAuth helpers need
`origin: "https://dev.epostak.sk"` because OAuth is outside `/api/v1`.

## API Reference

All methods return parsed JSON as Ruby hashes with string keys.

---

### Documents

#### Send a document

```ruby
result = client.enterprise.documents.send(
  receiverPeppolId: "0245:1234567890",
  invoiceNumber: "FV-2026-042",
  issueDate: "2026-04-11",
  dueDate: "2026-05-11",
  items: [
    { description: "Consulting", quantity: 10, unit: "HUR", unitPrice: 100, vatRate: 23 }
  ]
)
# => { "documentId" => "uuid", "peppolMessageId" => "...", "status" => "SENDING" }
```

Send with raw UBL XML:

```ruby
result = client.enterprise.documents.send(
  receiverPeppolId: "0245:1234567890",
  xml: '<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">...</Invoice>'
)
```

#### Get a document

```ruby
doc = client.enterprise.documents.get("doc-uuid")
# => { "id" => "...", "totals" => { "withVat" => 1230.0 }, ... }
```

#### Update a draft

```ruby
updated = client.enterprise.documents.update("doc-uuid", dueDate: "2026-05-15", note: "Updated terms")
```

#### Check delivery status

```ruby
status = client.enterprise.documents.status("doc-uuid")
# => { "status" => "DELIVERED", "deliveredAt" => "2026-04-11T12:30:00Z", "history" => [...] }
```

#### Batch status check

```ruby
batch = client.enterprise.documents.status_batch(["doc-uuid-1", "doc-uuid-2"])
puts "#{batch['found']}/#{batch['total']} found"
```

#### Get delivery evidence

```ruby
evidence = client.enterprise.documents.evidence("doc-uuid")
# => { "as4Receipt" => {...}, "mlr" => {...}, "invoiceResponse" => {...} }
```

#### Download PDF

```ruby
pdf_bytes = client.enterprise.documents.pdf("doc-uuid")
File.binwrite("invoice.pdf", pdf_bytes)
```

#### Download UBL XML

```ruby
xml = client.enterprise.documents.ubl("doc-uuid")
# => '<?xml version="1.0"?><Invoice ...>...</Invoice>'
```

#### Respond to a received invoice

```ruby
# Accept
client.enterprise.documents.respond("doc-uuid", status: "AP")

# Reject with a reason
client.enterprise.documents.respond("doc-uuid", status: "RE", note: "Incorrect VAT rate applied")
```

Response status codes: `AP` (accepted), `RE` (rejected), `UQ` (under query).

#### Validate without sending

```ruby
result = client.enterprise.documents.validate(
  receiverPeppolId: "0245:1234567890",
  items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }]
)
# => { "valid" => true, "warnings" => [], "ublPreview" => "..." }
```

#### Preflight check

```ruby
check = client.enterprise.documents.preflight(receiver_peppol_id: "0245:1234567890")
# => { "registered" => true, "capabilities" => [...] }
```

#### Convert between JSON and UBL

```ruby
# JSON to UBL
result = client.enterprise.documents.convert(
  input_format: "json",
  output_format: "ubl",
  document: { invoiceNumber: "FV-001", items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }] }
)
puts result["document"] # => UBL XML string

# UBL to JSON
result = client.enterprise.documents.convert(
  input_format: "ubl",
  output_format: "json",
  document: "<Invoice>...</Invoice>"
)
puts result["document"] # => parsed invoice hash
```

---

### Inbox

Accessed via `client.enterprise.documents.inbox`.

#### List inbox documents

```ruby
response = client.enterprise.documents.inbox.list(status: "RECEIVED", limit: 50)
response["documents"].each do |doc|
  puts "#{doc['id']}: #{doc['senderName']}"
end
```

#### Get a single inbox document

```ruby
result = client.enterprise.documents.inbox.get("doc-uuid")
puts result["payload"] # => UBL XML string
```

#### Acknowledge a document

```ruby
ack = client.enterprise.documents.inbox.acknowledge("doc-uuid")
puts ack["acknowledgedAt"] # => "2026-04-11T12:00:00Z"
```

#### List all inbox documents (integrator)

```ruby
response = client.enterprise.documents.inbox.list_all(
  since: "2026-04-01T00:00:00Z",
  status: "RECEIVED"
)
```

---

### Firms

#### List firms

```ruby
response = client.enterprise.firms.list
response["firms"].each { |f| puts "#{f['name']} (#{f['peppolStatus']})" }
```

#### Get firm details

```ruby
firm = client.enterprise.firms.get("firm-uuid")
puts firm["icDph"] # => "SK2020123456"
```

#### List firm documents

```ruby
response = client.enterprise.firms.documents("firm-uuid", direction: "inbound", limit: 100)
```

#### Register a Peppol identifier

Slovak Peppol ID format: `0245:DIC` (e.g. `0245:1234567890`). Per Slovak PASR, only the `0245` scheme is used — the `9950:SK...` VAT form is not supported.

```ruby
result = client.enterprise.firms.register_peppol_id("firm-uuid",
  scheme: "0245",
  identifier: "1234567890"
)
puts result["peppolId"] # => "0245:1234567890"
```

#### Assign a firm by ICO

```ruby
result = client.enterprise.firms.assign(ico: "12345678")
puts result["firm"]["id"]
```

#### Batch assign firms

```ruby
result = client.enterprise.firms.assign_batch(icos: ["12345678", "87654321", "11223344"])
result["results"].each do |r|
  if r["error"]
    puts "#{r['ico']}: #{r['message']}"
  else
    puts "#{r['ico']}: #{r['status']}"
  end
end
```

---

### Peppol

#### SMP lookup

```ruby
participant = client.enterprise.peppol.lookup("0245", "1234567890")
participant["capabilities"].each do |cap|
  puts cap["documentTypeId"]
end
```

#### Directory search

```ruby
results = client.enterprise.peppol.directory.search(q: "consulting", country: "SK", page_size: 50)
puts "Found #{results['total']} participants"
results["results"].each { |r| puts r["name"] }
```

#### Company lookup by ICO

```ruby
company = client.enterprise.peppol.company_lookup("12345678")
puts company["name"]     # => "ACME s.r.o."
puts company["peppolId"] # => "0245:1234567890" or nil

matches = client.enterprise.peppol.company_search(q: "Demo", limit: 10)

resolved = client.enterprise.peppol.resolve(
  ico: "12345678",
  document_type_id: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."
)
```

---

### Webhooks

#### Create a webhook

```ruby
webhook = client.enterprise.webhooks.create(
  url: "https://example.com/webhooks/epostak",
  events: ["document.received", "document.sent"]
)
puts webhook["secret"] # Store this for HMAC-SHA256 verification!

queued = client.enterprise.webhooks.test(
  webhook["id"],
  event: "document.received",
  count: 250,
  mode: "queued"
)

deliveries = client.enterprise.webhooks.deliveries(
  webhook["id"],
  test_run_id: queued["testRunId"],
  includeResponseBody: true
)

dlq = client.enterprise.webhooks.dead_letters(includeResponseBody: true)
dlq["items"].each do |failed|
  client.enterprise.webhooks.replay_dead_letter(failed["id"])
  # or: client.enterprise.webhooks.resolve_dead_letter(failed["id"], reason: "Handled in ERP")
end
```

#### Verify an incoming webhook

The server sends two headers: `X-Webhook-Signature: sha256=<hex>` and `X-Webhook-Timestamp: <unix>`.

```ruby
post "/webhooks/epostak" do
  raw = request.body.read
  result = EPostak.verify_webhook_signature(
    payload:   raw,
    signature: request.env["HTTP_X_WEBHOOK_SIGNATURE"].to_s,
    timestamp: request.env["HTTP_X_WEBHOOK_TIMESTAMP"].to_s,
    secret:    ENV["EPOSTAK_WEBHOOK_SECRET"]
  )
  halt 400, "bad signature: #{result[:reason]}" unless result[:valid]
  event = JSON.parse(raw)
  # process event...
  status 204
end
```

#### Dedup + retry headers (server v1.1 — 2026-05-12)

Three new headers on every push delivery:

| Header | Value | Use |
|-|-|-|
| `X-Webhook-Event-Id` | UUID, stable across retries | **Primary dedup key.** Body also carries it as `webhook_event_id`. |
| `X-Webhook-Attempt` | 1-based attempt number | Telemetry / logging. |
| `X-Webhook-Max-Attempts` | Total attempts in the retry window (10) | Telemetry / logging. |

Recommended receiver pattern:

```ruby
# INSERT ON CONFLICT DO NOTHING on the event id is enough — every retry
# of the same logical event carries the SAME X-Webhook-Event-Id.
event_id = request.env["HTTP_X_WEBHOOK_EVENT_ID"]
inserted = DB.exec_params(
  "INSERT INTO processed_webhooks (event_id) VALUES ($1) " \
  "ON CONFLICT (event_id) DO NOTHING RETURNING id",
  [event_id]
)
halt 200 if inserted.ntuples == 0  # duplicate — ack and skip
# process event for the first time...
```

**Retry policy (server-side, as of 2026-05-12):** retries fire only for `408`, `425`, `429`, `502`, `503`, `504` and network errors (~44h bounded backoff). Returning any other 4xx/5xx — including `500` — terminates the retry loop immediately. If your handler wants a retry on a transient failure, return `503` (not `500`).

The signature contract is **unchanged** — `EPostak.verify_webhook_signature` continues to work without code changes.

#### List webhooks

```ruby
response = client.enterprise.webhooks.list
response["data"].each { |w| puts "#{w['url']} (active: #{w['isActive']})" }
```

#### Get webhook details

```ruby
webhook = client.enterprise.webhooks.get("webhook-uuid")
failed = webhook["deliveries"].select { |d| d["status"] == "failed" }
```

#### Update a webhook

```ruby
# Pause
client.enterprise.webhooks.update("webhook-uuid", isActive: false)

# Change URL
client.enterprise.webhooks.update("webhook-uuid", url: "https://new-url.com/webhooks")
```

#### Delete a webhook

```ruby
client.enterprise.webhooks.delete("webhook-uuid")
```

---

### Webhook Queue (Pull-based)

Accessed via `client.enterprise.webhooks.queue`. An alternative to push webhooks for environments that cannot receive inbound HTTPS requests.

#### Pull events

```ruby
response = client.enterprise.webhooks.queue.pull(limit: 50, event_type: "document.received")
response["items"].each do |item|
  puts "#{item['type']}: #{item['payload']}"
end
```

#### Acknowledge a single event

```ruby
client.enterprise.webhooks.queue.ack("event-uuid")
# => nil (HTTP 204)
```

#### Batch acknowledge events

```ruby
response = client.enterprise.webhooks.queue.pull(limit: 50)
ids = response["items"].map { |i| i["event_id"] }
client.enterprise.webhooks.queue.batch_ack(ids)
# => nil (HTTP 204)
```

#### Pull all events (integrator)

```ruby
response = client.enterprise.webhooks.queue.pull_all(since: "2026-04-11T00:00:00Z", limit: 200)
response["items"].each { |e| puts "#{e['firm_id']}: #{e['event']}" }
```

#### Batch acknowledge all (integrator)

```ruby
response = client.enterprise.webhooks.queue.pull_all(limit: 100)
ids = response["items"].map { |e| e["event_id"] }
result = client.enterprise.webhooks.queue.batch_ack_all(ids)
puts "Acknowledged: #{result['acknowledged']}"
```

---

### Reporting

#### Get statistics

```ruby
stats = client.enterprise.reporting.statistics(from: "2026-01-01", to: "2026-03-31")
puts "Sent: #{stats['outbound']['total']}"
puts "Received: #{stats['inbound']['total']}"
puts "Delivery rate: #{stats['outbound']['delivered']}/#{stats['outbound']['total']}"

submissions = client.enterprise.reporting.submissions(limit: 20, report_type: "EUSR")
puts submissions["total"]
```

---

### Account

#### Get account info

```ruby
account = client.enterprise.account.get
puts "Plan: #{account['plan']['name']} (#{account['plan']['status']})"
puts "Usage: #{account['usage']['outbound']} sent, #{account['usage']['inbound']} received"
```

---

### Extract (AI-powered OCR)

#### Extract from a single file

```ruby
# From a file path
result = client.enterprise.extract.single("invoice.pdf", "application/pdf", file_name: "invoice.pdf")
puts result["confidence"] # => 0.95
puts result["ubl_xml"]   # => Generated UBL XML

# From an IO object
io = File.open("scan.png", "rb")
result = client.enterprise.extract.single(io, "image/png", file_name: "scan.png")
```

#### Extract from multiple files

```ruby
result = client.enterprise.extract.batch([
  { file: "invoice1.pdf", mime_type: "application/pdf", file_name: "invoice1.pdf" },
  { file: "invoice2.pdf", mime_type: "application/pdf", file_name: "invoice2.pdf" }
])
puts "#{result['successful']}/#{result['total']} extracted successfully"
```

---

## Integrator mode

Integrator keys (`sk_int_*`) can manage multiple client firms. Use `with_firm` to scope requests:

```ruby
integrator = EPostak::Client.new(client_id: "sk_int_xxxxx", client_secret: "your_secret")

# List all managed firms
firms = integrator.firms.list

# Work with a specific firm
# Use this for legacy firm-scoped Enterprise API calls. Connector V2 calls stay
# integrator-scoped and resolve the managed firm from customerRef.
firm_client = integrator.with_firm("firm-a-uuid")
firm_client.enterprise.documents.send(
  receiverPeppolId: "0245:1234567890",
  items: [{ description: "Service", quantity: 1, unitPrice: 500, vatRate: 23 }]
)

# Switch to another firm
other_client = integrator.with_firm("firm-b-uuid")
other_client.enterprise.documents.inbox.list

# Cross-firm endpoints (no with_firm needed)
all_docs = integrator.documents.inbox.list_all(since: "2026-04-01T00:00:00Z")
all_events = integrator.webhooks.queue.pull_all(limit: 200)

# Integrator API-key management (production supports list + deactivate)
integrator_api = integrator.integrator
keys = integrator_api.keys.list
integrator_api.keys.deactivate(client_id: "sk_int_xxxxx...abcd")
usage = integrator_api.licenses.info(limit: 100)
```

## Error handling

All API errors raise `EPostak::Error` with the HTTP status, error code, and details:

```ruby
begin
  client.enterprise.documents.send(body)
rescue EPostak::Error => e
  puts "HTTP #{e.status}: #{e.message}"
  puts "Code: #{e.code}" if e.code           # e.g. "VALIDATION_ERROR"
  puts "Details: #{e.details}" if e.details   # field-level errors
end
```

Common error statuses:

| Status | Meaning                                        |
| ------ | ---------------------------------------------- |
| 400    | Validation error (check `e.details`)           |
| 401    | Invalid or expired API key                     |
| 403    | Insufficient permissions or missing firm scope |
| 404    | Resource not found                             |
| 429    | Rate limited (retry after delay)               |
| 500    | Server error                                   |

## Peppol ID format

Slovak Peppol identifiers use a single scheme per Slovak PASR:

| Scheme | Format              | Example           |
| ------ | ------------------- | ----------------- |
| `0245` | DIC (tax ID number) | `0245:1234567890` |

**Do not use scheme `9950:SK...`** (VAT-number form) or `0191` — neither is valid for Slovak participants.

## License

MIT
