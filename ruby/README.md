# epostak

Ruby SDK for the [ePošťák APIs](https://epostak.sk/api/docs) — managed
Connector workflows, the Enterprise API, and SAPI-SK interoperability.

## Major release API shape

Ruby `1.1.0` is the current workflow-first source release with the managed
Connector surface:

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

```ruby
require "epostak"

connector_client = EPostak::Client.new(
  client_id: ENV.fetch("EPOSTAK_CONNECTOR_CLIENT_ID"),
  client_secret: ENV.fetch("EPOSTAK_CONNECTOR_CLIENT_SECRET")
)
customer = connector_client.connector.customers.for_customer("erp-acme")
document = customer.documents.send({
  externalId: "invoice-2026-0042",
  type: "invoice",
  number: "2026-0042",
  recipient: { country: "SK", taxId: "2120123456" },
  lines: [{ description: "Monthly licence", quantity: 1, unitPrice: 100, vatRate: 23 }]
})
```

The integrator owns the stable `customerRef` after ePošťák has approved and
Peppol-registered the firm. The SDK cannot create or discover firms. It injects `customerRef`, sends
immediately by default, and derives a stable idempotency key from `customerRef`
and `externalId`.

```ruby
events = customer.events.list(limit: 50)
configured_webhook = connector_client.connector.webhook.configure(
  "https://erp.example.com/webhooks/epostak",
  ["document.received", "document.delivered"]
)
if configured_webhook["secret"]
  # Persist this value in your server-side secret manager now. It is returned only once.
  one_time_webhook_secret = configured_webhook["secret"]
end
connector_client.connector.webhook.test("erp-acme")
```

Push uses the same canonical event item as polling with `customerRef` at the
root. `EPostak.verify_webhook_signature(...)` verifies HMAC-SHA256 over
`timestamp + "." + raw_body`.

### Acknowledge locally or respond to the supplier

```ruby
received_document_id = "document-id-from-list-or-event"
customer.documents.acknowledge(received_document_id, "erp:#{received_document_id}")
response = customer.documents.respond(
  received_document_id,
  { status: "accepted", note: "Imported and accepted" }
)

# Direct alternative (do not call both); customerRef is still mandatory:
# connector_client.connector.documents.respond(
#   received_document_id, "erp-acme", { status: "accepted" }
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

```ruby
client.enterprise.payloads.validate(invoice)

client.enterprise.documents.preflight(invoice[:receiverPeppolId])

sent = client.enterprise.documents.send(
  invoice,
  idempotency_key: "erp-fa-2026-001"
)

events = client.enterprise.events.pull(limit: 50)
client.enterprise.events.batch_ack(events["items"].map { |event| event["event_id"] })

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

**Unreleased** (2026-07-19)
- Enterprise 1.6.0 JSON sends accept `processId`, JSON self-billing and credit
  notes through `documentType`, `supplier*`, and `precedingInvoiceRef`.
- Send responses document replay/link metadata. Document events now document
  `{ process_id, pagination: { limit, nextCursor, hasMore } }`.

**Included in v1.1.0** (2026-07-14)
- Connector is canonical at `client.connector`; the existing Enterprise
  namespace alias remains silently supported
- ePošťák approves the integrator and Peppol firms; the integrator stores its
  own stable `customerRef` in the dashboard
- Customer document creation snapshots the request, validates explicit
  1-255-byte idempotency keys, and retries network errors, `429`, and all `5xx`
  only when safe; lifecycle calls are server-idempotent and `409` is never retried
- `EPostak::Error` exposes `field`, `next_action`, `retryable`, `request_id`, and
  `retry_after`
- `customerRef` and `externalId` use the backend ECMAScript `TrimString` code
  points before hashing; `U+0085` is preserved
- `document.cancelled` is a first-class business event

**Included in v1.1.0** (2026-07-12)
- JSON billing payloads now expose the live receiver address, `prepaidAmount`,
  `prepayments`, and advanced line-item VAT/classification fields from the
  Enterprise OpenAPI

**Included in v1.1.0** (2026-07-01)
- `client.enterprise.payloads.validate(...)`, `client.enterprise.events.pull(...)`,
  and `client.enterprise.documents.support_packet(...)` cover the Enterprise
  API facade flow for validation, event pull/ack, and support packets

**Included in v1.1.0** (2026-06-30)
- Customer-scoped
  `client.connector.customers.for_customer(customer_ref).advanced.mapper(...)`
  is the managed, preview-only Mapper flow. Top-level
  `client.connector.advanced.mapper(...)` remains a legacy compatibility alias
  and is unavailable with managed Connector credentials
- `client.box` / `client.enterprise.box` covers ePošťák Box list, create with
  `payload_xml`, detail, schedule, send-now, retry, and cancel over
  `/box/items`

**v1.0.0** (2026-06-14)
- `client.enterprise` is the documented namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces
- `client.sapi.participants.for_participant(participant_id).documents` requires
  participant scoping before SAPI document send/receive/get/acknowledge
- `client.connector.customers.for_customer(customer_ref)` injects
  `customerRef` and keeps `X-Firm-Id` off customer-managed Connector calls
- The Connector compatibility surface includes preflight, Zen input, Autopilot,
  reconcile, mailbox, sync, document evidence, and event polling
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

The Ruby SDK is currently distributed as reviewed source, not through
RubyGems. Clone this repository and reference its `ruby/` directory from your
Gemfile:

```ruby
gem "epostak", path: "/path/to/epostak-sdk/ruby"
```

```bash
bundle install
```

## Quick start

```ruby
require "epostak"

client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "your_secret")

# Send an invoice
result = client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  receiverName: "Firma s.r.o.",
  invoiceNumber: "FV-2026-042",
  items: [
    { description: "Web development", quantity: 40, unit: "HUR", unitPrice: 80, vatRate: 23 }
  ]
})

puts result["documentId"]
```

### Connector golden path for ERP developers

Start from the stable `customerRef` configured by the integrator in the
dashboard after ePošťák approves and Peppol-registers the firm. There is no SDK
method for creating or discovering firms.

```ruby
connector_client = EPostak::Client.new(
  client_id: ENV.fetch("EPOSTAK_CONNECTOR_CLIENT_ID"),
  client_secret: ENV.fetch("EPOSTAK_CONNECTOR_CLIENT_SECRET")
)
customer = connector_client.connector.customers.for_customer("erp-customer-1")
document = customer.documents.send({
  externalId: "FA-2026-001",
  type: "invoice",
  number: "FA-2026-001",
  issueDate: "2026-07-14",
  dueDate: "2026-07-28",
  currency: "EUR",
  recipient: { country: "SK", taxId: "2120123456" },
  lines: [
    { description: "Služby", quantity: 1, unitPrice: 100, vatRate: 23 }
  ]
})

page = customer.documents.list(state: "needs_attention")
events = customer.events.list(limit: 50)
page["documents"].select { |item| item["direction"] == "inbound" }.each do |received|
  customer.documents.acknowledge(received["id"], "erp:#{received['id']}")
end
ubl = customer.advanced.documents.ubl(document["id"])
evidence = customer.advanced.documents.evidence(document["id"])
evidence_bundle = customer.advanced.documents.evidence_bundle(document["id"])
support_packet = customer.advanced.documents.support_packet(document["id"])
puts "#{document['id']} #{events['nextCursor']}"
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

```ruby
preview = customer.advanced.mapper({
  templateKey: "pohoda-csv-v1",
  sourceType: "csv",
  sourceText: csv_text
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

```ruby
begin
  customer.documents.send(invoice)
rescue EPostak::Error => error
  warn [error.code, error.message, error.field, error.next_action].inspect
  warn [error.retryable, error.request_id, error.retry_after].inspect
end
```

Keyed document creation retries network failures, `429`, and every `5xx` while
reusing the exact body and key. Lifecycle send/cancel/acknowledge calls are
server-idempotent and use the same policy. `Retry-After` is honored; `409` is
always surfaced once and never retried automatically.

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

The SDK recognizes these credential contexts:

- **Direct keys** (`sk_live_*`) — single firm, all requests scoped to your firm
- **Enterprise integrator keys** (`sk_int_*`) — multi-tenant Enterprise access;
  use `with_firm` or `X-Firm-Id`
- **Connector credentials** — issued by ePošťák after integrator and firm
  approval; the integrator configures `customerRef` for each approved Peppol
  firm in the dashboard

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

Managed Connector calls use Connector credentials and the integrator's stored
`customerRef` for an ePošťák-approved Peppol firm and do not need `firm_id`.
Enterprise credentials cannot provision or scout Connector firms.

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
result = client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  receiverName: "Firma s.r.o.",
  invoiceNumber: "FV-2026-042",
  issueDate: "2026-04-11",
  dueDate: "2026-05-11",
  items: [
    { description: "Consulting", quantity: 10, unit: "HUR", unitPrice: 100, vatRate: 23 }
  ]
})
# => { "documentId" => "uuid", "peppolMessageId" => "...", "status" => "SENDING" }
```

Send with raw UBL XML:

```ruby
result = client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  xml: '<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">...</Invoice>'
})
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
  receiverName: "Firma s.r.o.",
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
  document: { receiverName: "Firma s.r.o.", invoiceNumber: "FV-001", items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }] }
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
if participant["accepts"] && participant["routingStatus"] == "ready"
  # Receiver is registered and routable for the default BIS Billing invoice.
end

caps = client.enterprise.peppol.capabilities(
  scheme: "0245",
  identifier: "1234567890",
  document_type: "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."
)
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

### Events pull facade

Accessed via `client.enterprise.events`. This is the preferred pull/ack path
for environments that cannot receive inbound HTTPS requests on day one.

#### Pull events

```ruby
response = client.enterprise.events.pull(limit: 50, event_type: "document.received")
response["items"].each do |item|
  puts "#{item['type']}: #{item['payload']}"
end
```

#### Acknowledge a single event

```ruby
client.enterprise.events.ack("event-uuid")
# => {"acknowledged" => true}
```

#### Batch acknowledge events

```ruby
response = client.enterprise.events.pull(limit: 50)
ids = response["items"].map { |i| i["event_id"] }
client.enterprise.events.batch_ack(ids)
# => {"acknowledged" => ids.length}
```

`client.enterprise.webhooks.queue` remains available for legacy queue and
cross-firm drain jobs.

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

### Payload Assistant OCR

#### Extract from a single file

```ruby
# From a file path
result = client.enterprise.payloads.extract("invoice.pdf", "application/pdf", file_name: "invoice.pdf")
puts result["confidence"] # => 0.95
puts result["ubl_xml"]    # => Generated UBL XML
puts result["send_payload"] # Outbound draft for review + validate + send

# From an IO object
io = File.open("scan.png", "rb")
result = client.enterprise.payloads.extract(io, "image/png", file_name: "scan.png")
```

#### Extract from multiple files

```ruby
result = client.enterprise.payloads.extract_batch([
  { file: "invoice1.pdf", mime_type: "application/pdf", file_name: "invoice1.pdf" },
  { file: "invoice2.pdf", mime_type: "application/pdf", file_name: "invoice2.pdf" }
])
puts "#{result['successful']}/#{result['total']} extracted successfully"
```

`client.enterprise.extract` remains available for compatibility; new
integrations should use `client.enterprise.payloads.extract`.

---

## Enterprise Integrator mode

This section is only for Enterprise integrator credentials and firm-scoped
Enterprise calls. It does not provision Connector. Use `with_firm` to scope
requests:

```ruby
integrator = EPostak::Client.new(client_id: "sk_int_xxxxx", client_secret: "your_secret")

# List all managed firms
firms = integrator.enterprise.firms.list

# Work with a specific firm
# This scopes only Enterprise integrator credentials. It does not provision
# Connector. Use Connector credentials and choose each customerRef in the
# dashboard after ePošťák approves the firm.
firm_client = integrator.with_firm("firm-a-uuid")
firm_client.enterprise.documents.send({
  receiverPeppolId: "0245:1234567890",
  receiverName: "Firma s.r.o.",
  items: [{ description: "Service", quantity: 1, unitPrice: 500, vatRate: 23 }]
})

# Switch to another firm
other_client = integrator.with_firm("firm-b-uuid")
other_client.enterprise.documents.inbox.list

# Cross-firm endpoints (no with_firm needed)
all_docs = integrator.enterprise.documents.inbox.list_all(since: "2026-04-01T00:00:00Z")
all_events = integrator.enterprise.webhooks.queue.pull_all(limit: 200)

# Integrator API-key management (production supports list + deactivate)
integrator_api = integrator.enterprise.integrator
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

## Connector webhook debugger

Inspect the exact signed body and attempt timeline, then use idempotent replay
after fixing the receiver. `run_test_suite` exercises all receiver scenarios.

```ruby
failed = connector_client.connector.webhook.list_deliveries(status: "FAILED")
detail = connector_client.connector.webhook.get_delivery(failed["deliveries"].first["id"])
connector_client.connector.webhook.replay_delivery(detail["delivery"]["id"], "erp:replay:1")
connector_client.connector.webhook.run_test_suite("erp-acme", "erp:suite:1")
```

## License

MIT
