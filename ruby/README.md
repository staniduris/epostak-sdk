# epostak

Ruby SDK for the [ePosťák](https://epostak.sk) Enterprise API — send and receive e-invoices via the Slovak Peppol network.

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
result = client.documents.send_document(
  receiverPeppolId: "0245:1234567890",
  invoiceNumber: "FV-2026-042",
  items: [
    { description: "Web development", quantity: 40, unit: "HUR", unitPrice: 80, vatRate: 23 }
  ]
)

puts result["documentId"]
```

## Authentication

The SDK supports two types of API keys:

- **Direct keys** (`sk_live_*`) — single firm, all requests scoped to your firm
- **Integrator keys** (`sk_int_*`) — multi-tenant, use `with_firm` or `X-Firm-Id` to act on behalf of client firms

```ruby
# Direct key
client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "your_secret")

# Integrator key — scope to a specific firm
integrator = EPostak::Client.new(client_id: "sk_int_xxxxx", client_secret: "your_secret")
firm_a = integrator.with_firm("firm-a-uuid")
firm_a.documents.inbox.list
```

## Configuration

```ruby
client = EPostak::Client.new(
  client_id: "sk_live_xxxxx", client_secret: "your_secret",
  base_url: "https://staging.epostak.sk/api/enterprise",  # optional, override for staging
  firm_id: "firm-uuid"                                     # optional, for integrator keys
)
```

## API Reference

All methods return parsed JSON as Ruby hashes with string keys.

---

### Documents

#### Send a document

```ruby
result = client.documents.send_document(
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
result = client.documents.send_document(
  receiverPeppolId: "0245:1234567890",
  xml: '<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">...</Invoice>'
)
```

#### Get a document

```ruby
doc = client.documents.get("doc-uuid")
# => { "id" => "...", "totals" => { "withVat" => 1230.0 }, ... }
```

#### Update a draft

```ruby
updated = client.documents.update("doc-uuid", dueDate: "2026-05-15", note: "Updated terms")
```

#### Check delivery status

```ruby
status = client.documents.status("doc-uuid")
# => { "status" => "DELIVERED", "deliveredAt" => "2026-04-11T12:30:00Z", "history" => [...] }
```

#### Get delivery evidence

```ruby
evidence = client.documents.evidence("doc-uuid")
# => { "as4Receipt" => {...}, "mlr" => {...}, "invoiceResponse" => {...} }
```

#### Download PDF

```ruby
pdf_bytes = client.documents.pdf("doc-uuid")
File.binwrite("invoice.pdf", pdf_bytes)
```

#### Download UBL XML

```ruby
xml = client.documents.ubl("doc-uuid")
# => '<?xml version="1.0"?><Invoice ...>...</Invoice>'
```

#### Respond to a received invoice

```ruby
# Accept
client.documents.respond("doc-uuid", status: "AP")

# Reject with a reason
client.documents.respond("doc-uuid", status: "RE", note: "Incorrect VAT rate applied")
```

Response status codes: `AP` (accepted), `RE` (rejected), `UQ` (under query).

#### Validate without sending

```ruby
result = client.documents.validate(
  receiverPeppolId: "0245:1234567890",
  items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }]
)
# => { "valid" => true, "warnings" => [], "ublPreview" => "..." }
```

#### Preflight check

```ruby
check = client.documents.preflight(receiver_peppol_id: "0245:1234567890")
# => { "registered" => true, "capabilities" => [...] }
```

#### Convert between JSON and UBL

```ruby
# JSON to UBL
result = client.documents.convert(
  input_format: "json",
  output_format: "ubl",
  document: { invoiceNumber: "FV-001", items: [{ description: "Test", quantity: 1, unitPrice: 100, vatRate: 23 }] }
)
puts result["document"] # => UBL XML string

# UBL to JSON
result = client.documents.convert(
  input_format: "ubl",
  output_format: "json",
  document: "<Invoice>...</Invoice>"
)
puts result["document"] # => parsed invoice hash
```

---

### Inbox

Accessed via `client.documents.inbox`.

#### List inbox documents

```ruby
response = client.documents.inbox.list(status: "RECEIVED", limit: 50)
response["documents"].each do |doc|
  puts "#{doc['id']}: #{doc['senderName']}"
end
```

#### Get a single inbox document

```ruby
result = client.documents.inbox.get("doc-uuid")
puts result["payload"] # => UBL XML string
```

#### Acknowledge a document

```ruby
ack = client.documents.inbox.acknowledge("doc-uuid")
puts ack["acknowledgedAt"] # => "2026-04-11T12:00:00Z"
```

#### List all inbox documents (integrator)

```ruby
response = client.documents.inbox.list_all(
  since: "2026-04-01T00:00:00Z",
  status: "RECEIVED"
)
```

---

### Firms

#### List firms

```ruby
response = client.firms.list
response["firms"].each { |f| puts "#{f['name']} (#{f['peppolStatus']})" }
```

#### Get firm details

```ruby
firm = client.firms.get("firm-uuid")
puts firm["icDph"] # => "SK2020123456"
```

#### List firm documents

```ruby
response = client.firms.documents("firm-uuid", direction: "inbound", limit: 100)
```

#### Register a Peppol identifier

Slovak Peppol ID format: `0245:DIC` (e.g. `0245:1234567890`). Per Slovak PASR, only the `0245` scheme is used — the `9950:SK...` VAT form is not supported.

```ruby
result = client.firms.register_peppol_id("firm-uuid",
  scheme: "0245",
  identifier: "1234567890"
)
puts result["peppolId"] # => "0245:1234567890"
```

#### Assign a firm by ICO

```ruby
result = client.firms.assign(ico: "12345678")
puts result["firm"]["id"]
```

#### Batch assign firms

```ruby
result = client.firms.assign_batch(icos: ["12345678", "87654321", "11223344"])
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
participant = client.peppol.lookup("0245", "1234567890")
participant["capabilities"].each do |cap|
  puts cap["documentTypeId"]
end
```

#### Directory search

```ruby
results = client.peppol.directory.search(q: "consulting", country: "SK", page_size: 50)
puts "Found #{results['total']} participants"
results["results"].each { |r| puts r["name"] }
```

#### Company lookup by ICO

```ruby
company = client.peppol.company_lookup("12345678")
puts company["name"]     # => "ACME s.r.o."
puts company["peppolId"] # => "0245:1234567890" or nil
```

---

### Webhooks

#### Create a webhook

```ruby
webhook = client.webhooks.create(
  url: "https://example.com/webhooks/epostak",
  events: ["document.received", "document.sent"]
)
puts webhook["secret"] # Store this for HMAC-SHA256 verification!
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

#### List webhooks

```ruby
response = client.webhooks.list
response["data"].each { |w| puts "#{w['url']} (active: #{w['isActive']})" }
```

#### Get webhook details

```ruby
webhook = client.webhooks.get("webhook-uuid")
failed = webhook["deliveries"].select { |d| d["status"] == "failed" }
```

#### Update a webhook

```ruby
# Pause
client.webhooks.update("webhook-uuid", isActive: false)

# Change URL
client.webhooks.update("webhook-uuid", url: "https://new-url.com/webhooks")
```

#### Delete a webhook

```ruby
client.webhooks.delete("webhook-uuid")
```

---

### Webhook Queue (Pull-based)

Accessed via `client.webhooks.queue`. An alternative to push webhooks for environments that cannot receive inbound HTTPS requests.

#### Pull events

```ruby
response = client.webhooks.queue.pull(limit: 50, event_type: "document.received")
response["items"].each do |item|
  puts "#{item['type']}: #{item['payload']}"
end
```

#### Acknowledge a single event

```ruby
client.webhooks.queue.ack("event-uuid")
# => nil (HTTP 204)
```

#### Batch acknowledge events

```ruby
response = client.webhooks.queue.pull(limit: 50)
ids = response["items"].map { |i| i["event_id"] }
client.webhooks.queue.batch_ack(ids)
# => nil (HTTP 204)
```

#### Pull all events (integrator)

```ruby
response = client.webhooks.queue.pull_all(since: "2026-04-11T00:00:00Z", limit: 200)
response["items"].each { |e| puts "#{e['firm_id']}: #{e['event']}" }
```

#### Batch acknowledge all (integrator)

```ruby
response = client.webhooks.queue.pull_all(limit: 100)
ids = response["items"].map { |e| e["event_id"] }
result = client.webhooks.queue.batch_ack_all(ids)
puts "Acknowledged: #{result['acknowledged']}"
```

---

### Reporting

#### Get statistics

```ruby
stats = client.reporting.statistics(from: "2026-01-01", to: "2026-03-31")
puts "Sent: #{stats['outbound']['total']}"
puts "Received: #{stats['inbound']['total']}"
puts "Delivery rate: #{stats['outbound']['delivered']}/#{stats['outbound']['total']}"
```

---

### Account

#### Get account info

```ruby
account = client.account.get
puts "Plan: #{account['plan']['name']} (#{account['plan']['status']})"
puts "Usage: #{account['usage']['outbound']} sent, #{account['usage']['inbound']} received"
```

---

### Extract (AI-powered OCR)

#### Extract from a single file

```ruby
# From a file path
result = client.extract.single("invoice.pdf", "application/pdf", file_name: "invoice.pdf")
puts result["confidence"] # => 0.95
puts result["ubl_xml"]   # => Generated UBL XML

# From an IO object
io = File.open("scan.png", "rb")
result = client.extract.single(io, "image/png", file_name: "scan.png")
```

#### Extract from multiple files

```ruby
result = client.extract.batch([
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
firm_client = integrator.with_firm("firm-a-uuid")
firm_client.documents.send_document(
  receiverPeppolId: "0245:1234567890",
  items: [{ description: "Service", quantity: 1, unitPrice: 500, vatRate: 23 }]
)

# Switch to another firm
other_client = integrator.with_firm("firm-b-uuid")
other_client.documents.inbox.list

# Cross-firm endpoints (no with_firm needed)
all_docs = integrator.documents.inbox.list_all(since: "2026-04-01T00:00:00Z")
all_events = integrator.webhooks.queue.pull_all(limit: 200)
```

## Error handling

All API errors raise `EPostak::Error` with the HTTP status, error code, and details:

```ruby
begin
  client.documents.send_document(body)
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
