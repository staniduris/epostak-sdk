# epostak-sdk-java

Official Java SDK for the [ePosťak Enterprise API](https://epostak.sk/api/docs/enterprise) -- Peppol e-invoicing for Slovakia and the EU.

Java 17+. Single dependency: Gson. Uses `java.net.http.HttpClient` (built-in since Java 11).

---

## Installation

### Maven

```xml
<dependency>
    <groupId>sk.epostak</groupId>
    <artifactId>epostak-sdk</artifactId>
    <version>0.10.0</version>
</dependency>
```

### Gradle

```groovy
implementation 'sk.epostak:epostak-sdk:0.10.0'
```

## Recent changes

### Unreleased

- `client.connector()` covers Connector preflight, send, outbox stage/list/detail/send/batch/cancel, status, inbox list/detail, ACK, and event polling.
- Docs: added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence.

### v0.10.0 — 2026-05-18

- `client.sapi()` covers SAPI-SK 1.0 document send, receive list/detail, and acknowledge.
- `client.webhooks().test(id, new WebhookTestParams().count(250).mode("queued"))` supports direct and queued webhook tests.
- `client.webhooks().deadLetters(...)`, `replayDeadLetter(id)`, and `resolveDeadLetter(id, reason)` cover webhook DLQ operations.
- `client.peppol().resolve(...)` resolves ERP identifiers to Peppol participant + routing capability.
- Added evidence bundle, outbound MDN, company search, Peppol document listing, and license info helpers.

---

## Quick start

```java
import sk.epostak.sdk.EPostak;
import sk.epostak.sdk.models.*;

import java.util.List;

EPostak client = EPostak.builder()
    .clientId("sk_live_xxxxx")
    .clientSecret("sk_live_xxxxx")
    .build();

// Send an invoice
SendDocumentResponse result = client.documents().send(
    SendDocumentRequest.builder("0245:1234567890")
        .invoiceNumber("FV-2026-001")
        .issueDate("2026-04-04")
        .dueDate("2026-04-18")
        .items(List.of(
            new SendDocumentRequest.LineItem("Consulting", 10, 50, 23)
        ))
        .build()
);
System.out.println(result.documentId() + " " + result.messageId());
```

### Connector golden path for ERP developers

Use `client.connector()` for the ERP workflow instead of building raw HTTP
calls. The SDK handles OAuth token minting and refresh automatically after you
create the client.

```java
import java.util.List;
import java.util.Map;

Map<String, Object> invoice = Map.of(
    "receiverPeppolId", "0245:1234567890",
    "document", Map.of(
        "invoiceNumber", "FA-2026-001",
        "issueDate", "2026-06-04",
        "dueDate", "2026-06-18",
        "items", List.of(Map.of(
            "description", "Služby",
            "quantity", 1,
            "unitPrice", 100,
            "vatRate", 23
        ))
    )
);

@SuppressWarnings("unchecked")
Map<String, Object> document = (Map<String, Object>) invoice.get("document");

ConnectorPreflightResponse preflight = client.connector().preflight(
    new ConnectorPreflightRequest("0245:1234567890", document)
);
if (!preflight.ready()) {
    throw new IllegalStateException(preflight.repairReport().summary());
}

ConnectorOutboxStageResponse staged = client.connector().stageOutbox(
    new ConnectorOutboxStageRequest(
        List.of(new ConnectorOutboxStageItem(
            "FA-2026-001",
            "erp-fa-2026-001",
            null,
            invoice
        )),
        null,
        null,
        null
    )
);

ConnectorOutboxItem sent = client.connector().sendOutboxItem(
    staged.items().get(0).outboxId()
);
if (sent.documentId() == null) {
    throw new IllegalStateException("Staged invoice was not sent");
}

ConnectorStatusResponse status = client.connector().status(sent.documentId());

ConnectorInboxListResponse inbox = client.connector().inbox(
    new ConnectorListParams(null, 20)
);
for (ConnectorInboxDocument doc : inbox.documents()) {
    client.connector().ack(doc.documentId());
}

// Evidence is shared with the Enterprise document API.
DocumentEvidenceResponse evidence = client.documents().evidence(sent.documentId());
System.out.println(status.status() + " " + evidence.documentId());
```

For immediate send without staging:

```java
ConnectorSendResponse sent = client.connector().send(
    invoice,
    "erp-fa-2026-001-send"
);
System.out.println(sent.documentId() + " " + sent.status());
```

Common sandbox scenarios to test:

- nonexistent participant or unsupported document type: `preflight.ready() == false` with blocking `repairReport` items
- invalid UBL or missing buyer/seller data: `preflight` or `send` returns validation details in `EPostakException`
- duplicate idempotency key: `409 idempotency_conflict`
- expired token: the SDK refreshes automatically; persistent auth failures surface as API errors
- received invoice processing: poll `client.connector().inbox(...)`, store the payload, then call `client.connector().ack(documentId)`

---

## Authentication

| Key prefix  | Use case                                            |
| ----------- | --------------------------------------------------- |
| `sk_live_*` | Direct access -- acts on behalf of your own firm    |
| `sk_int_*`  | Integrator access -- acts on behalf of client firms |

### Constructor options

```java
EPostak client = EPostak.builder()
    .clientId("sk_live_xxxxx")      // Required
    .clientSecret("sk_live_xxxxx")  // Required
    .baseUrl("https://dev.epostak.sk/api/v1") // Optional test env; omit for prod
    .firmId("uuid")                  // Optional, required for integrator keys
    .build();
```

Production is the SDK default: Enterprise `https://epostak.sk/api/v1`, SAPI
`https://epostak.sk/sapi/v1`, OAuth origin `https://epostak.sk`. For test
calls, set `baseUrl` to `https://dev.epostak.sk/api/v1`; SAPI derives
`https://dev.epostak.sk/sapi/v1`, and OAuth helpers need
`origin = "https://dev.epostak.sk"` because OAuth is outside `/api/v1`.

---

## API Reference

### Documents

#### `documents().send(request)` -- Send a document via Peppol

**JSON mode** -- structured data, UBL XML is auto-generated:

```java
SendDocumentResponse result = client.documents().send(
    SendDocumentRequest.builder("0245:1234567890")
        .receiverName("Firma s.r.o.")
        .receiverIco("12345678")
        .receiverCountry("SK")
        .invoiceNumber("FV-2026-001")
        .issueDate("2026-04-04")
        .dueDate("2026-04-18")
        .currency("EUR")
        .iban("SK1234567890123456789012")
        .items(List.of(
            new SendDocumentRequest.LineItem("Consulting", 10, 50, 23)
        ))
        .build()
);
// result.documentId(), result.messageId(), result.status()
```

**XML mode** -- send a pre-built UBL XML document:

```java
client.documents().send(
    SendDocumentRequest.builder("0245:1234567890")
        .xml("<?xml version=\"1.0\" encoding=\"UTF-8\"?>...")
        .build()
);
```

#### `documents().get(id)` -- Get a document by ID

```java
Document doc = client.documents().get("doc-uuid");
// doc.getId(), doc.getNumber(), doc.getStatus(), doc.getDirection(),
// doc.getSupplier(), doc.getCustomer(), doc.getLines(), doc.getTotals()
```

#### `documents().update(id, request)` -- Update a draft document

```java
Document updated = client.documents().update("doc-uuid",
    UpdateDocumentRequest.builder()
        .invoiceNumber("FV-2026-002")
        .dueDate("2026-05-01")
        .items(List.of(
            new SendDocumentRequest.LineItem("Development", 20, 75, 23)
        ))
        .build()
);
```

#### `documents().status(id)` -- Full status with history

```java
DocumentStatusResponse status = client.documents().status("doc-uuid");
// status.status(), status.statusHistory(), status.deliveredAt()
```

#### `documents().evidence(id)` -- Delivery evidence

```java
DocumentEvidenceResponse evidence = client.documents().evidence("doc-uuid");
// evidence.as4Receipt(), evidence.mlrDocument(), evidence.invoiceResponse()
```

#### `documents().pdf(id)` -- Download PDF

```java
byte[] pdf = client.documents().pdf("doc-uuid");
java.nio.file.Files.write(java.nio.file.Path.of("invoice.pdf"), pdf);
```

#### `documents().ubl(id)` -- Download UBL XML

```java
String ubl = client.documents().ubl("doc-uuid");
```

#### `documents().respond(id, request)` -- Send invoice response

```java
InvoiceRespondResponse response = client.documents().respond("doc-uuid",
    new InvoiceRespondRequest("AP", "Invoice accepted"));
// "AP" = accepted, "RE" = rejected, "UQ" = under query
```

#### `documents().validate(request)` -- Validate without sending

```java
ValidationResult validation = client.documents().validate(
    SendDocumentRequest.builder("0245:1234567890")
        .items(List.of(
            new SendDocumentRequest.LineItem("Test", 1, 100, 23)
        ))
        .build()
);
// validation.valid(), validation.warnings(), validation.ubl()
```

#### `documents().preflight(receiverPeppolId, documentTypeId)` -- Check receiver capability

```java
PreflightResult check = client.documents().preflight("0245:1234567890", null);
// check.registered(), check.supportsDocumentType(), check.smpUrl()
```

#### `documents().convert(inputFormat, outputFormat, document)` -- Convert between JSON and UBL

```java
// JSON to UBL
ConvertResult result = client.documents().convert(
    "json",
    "ubl",
    Map.of("invoiceNumber", "FV-001", "items", List.of(...))
);
// result.outputFormat() == "ubl", result.document() is a UBL XML String, result.warnings()

// UBL to JSON
ConvertResult result = client.documents().convert(
    "ubl",
    "json",
    "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\">..."
);
// result.outputFormat() == "json", result.document() is a parsed JSON object
```

---

### Inbox

#### `documents().inbox().list(offset, limit, status, since)` -- List received documents

```java
InboxListResponse inbox = client.documents().inbox().list(0, 20, "RECEIVED", null);
// inbox.documents(), inbox.total(), inbox.limit(), inbox.offset()
```

#### `documents().inbox().get(id)` -- Full detail with UBL XML

```java
InboxDocumentDetailResponse detail = client.documents().inbox().get("doc-uuid");
// detail.document(), detail.payload() (UBL XML string or null)
```

#### `documents().inbox().acknowledge(id)` -- Mark as processed

```java
AcknowledgeResponse ack = client.documents().inbox().acknowledge("doc-uuid");
// ack.status() == "ACKNOWLEDGED"
```

#### `documents().inbox().listAll(...)` -- Cross-firm inbox (integrator)

```java
InboxAllResponse all = client.documents().inbox().listAll(0, 50, "RECEIVED", null, null);
// Each document includes firmId and firmName
```

---

### Peppol

#### `peppol().lookup(scheme, identifier)` -- SMP participant lookup

```java
PeppolParticipant participant = client.peppol().lookup("0245", "12345678");
// participant.peppolId(), participant.name(), participant.capabilities()
```

#### `peppol().directory().search(q, country, page, pageSize)` -- Business Card directory

```java
DirectorySearchResult results = client.peppol().directory().search(
    "Telekom", "SK", 0, 20);
// results.results(), results.total()
```

#### `peppol().companyLookup(ico)` -- Slovak company lookup

```java
CompanyLookup company = client.peppol().companyLookup("12345678");
// company.ico(), company.name(), company.dic(), company.peppolId()

Map<String, Object> matches = client.peppol().companySearch("Demo", 10);

Map<String, Object> resolved = client.peppol().resolve(Map.of(
    "ico", "12345678",
    "documentTypeId", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."));
```

---

### Firms (integrator keys)

#### `firms().list()` -- List all accessible firms

```java
List<FirmSummary> firms = client.firms().list();
// Each: id, name, ico, peppolId, peppolStatus
```

#### `firms().get(id)` -- Firm detail

```java
FirmDetail firm = client.firms().get("firm-uuid");
// firm.peppolIdentifiers(), firm.address(), firm.createdAt()
```

#### `firms().documents(id, offset, limit, direction)` -- List firm documents

```java
InboxListResponse docs = client.firms().documents("firm-uuid", 0, 20, "inbound");
```

#### `firms().registerPeppolId(id, scheme, identifier)` -- Register Peppol ID

```java
var result = client.firms().registerPeppolId("firm-uuid", "0245", "12345678");
// result.peppolId(), result.registeredAt()
```

#### `firms().assign(ico)` -- Assign firm to integrator

```java
AssignFirmResponse result = client.firms().assign("12345678");
// result.firm(), result.status()
```

#### `firms().assignBatch(icos)` -- Batch assign firms (max 50)

```java
BatchAssignResponse result = client.firms().assignBatch(
    List.of("12345678", "87654321", "11223344"));
// result.results() -- each with ico, firm, status, error
```

---

### Webhooks

#### `webhooks().create(url, events)` -- Register a webhook

```java
WebhookDetail webhook = client.webhooks().create(
    "https://example.com/webhook",
    List.of("document.received", "document.sent"));
// Store webhook.secret() for HMAC-SHA256 signature verification
```

#### `webhooks().list()` -- List webhooks

```java
List<Webhook> webhooks = client.webhooks().list();
```

#### `webhooks().get(id)` -- Webhook detail with deliveries

```java
WebhookDetail detail = client.webhooks().get("webhook-uuid");
// detail.deliveries()

WebhookTestResponse queued = client.webhooks().test(
    "webhook-uuid",
    new WebhookTestParams()
        .event("document.received")
        .count(250)
        .mode("queued"));

WebhookDeliveriesResponse deliveries = client.webhooks().deliveries(
    "webhook-uuid",
    Map.of("testRunId", queued.testRunId(), "includeResponseBody", true));

Map<String, Object> dlq = client.webhooks().deadLetters(Map.of("includeResponseBody", true));
client.webhooks().replayDeadLetter("delivery-id");
client.webhooks().resolveDeadLetter("delivery-id", "Handled in ERP");
```

#### `webhooks().update(id, url, events, isActive)` -- Update webhook

```java
client.webhooks().update("webhook-uuid",
    "https://example.com/new-webhook",
    List.of("document.received"),
    true);
```

#### `webhooks().delete(id)` -- Delete webhook

```java
client.webhooks().delete("webhook-uuid");
```

#### `WebhookSignature.verify(...)` -- Verify incoming webhook payload

The server signs every delivery with HMAC-SHA256 over `"${timestamp}.${rawBody}"` and sends two headers:
- `X-Webhook-Signature: sha256=<hex>`
- `X-Webhook-Timestamp: <unix_seconds>`

```java
import sk.epostak.sdk.WebhookSignature;

// In your HTTP handler (example: Jakarta EE / Spring MVC):
WebhookSignature.VerifyResult result = WebhookSignature.verify(
    rawBody,                                             // byte[] or String
    request.getHeader("x-webhook-signature"),            // "sha256=<hex>"
    request.getHeader("x-webhook-timestamp"),            // unix seconds
    System.getenv("EPOSTAK_WEBHOOK_SECRET"));

if (!result.valid()) {
    response.setStatus(400);
    response.getWriter().write("bad signature: " + result.reason());
    return;
}
// Process event — parse rawBody as JSON
```

Use `express.raw()` (Node) or the equivalent in your framework to capture raw bytes. Do NOT re-serialize parsed JSON — the round-trip breaks the HMAC.

> **Migration note:** the old single-header format (`X-Epostak-Signature: t=…,v1=…`) is no longer
> used by the server. Use `client.webhooks().create(url, events)` to register webhook endpoints.

#### Dedup + retry headers (server v1.1 — 2026-05-12)

Three new headers on every push delivery:

| Header | Value | Use |
|-|-|-|
| `X-Webhook-Event-Id` | UUID, stable across retries | **Primary dedup key.** Body also carries it as `webhook_event_id`. |
| `X-Webhook-Attempt` | 1-based attempt number | Telemetry / logging. |
| `X-Webhook-Max-Attempts` | Total attempts in the retry window (10) | Telemetry / logging. |

Recommended receiver pattern:

```java
// INSERT ON CONFLICT DO NOTHING on the event id is enough — every retry
// of the same logical event carries the SAME X-Webhook-Event-Id.
String eventId = request.getHeader("X-Webhook-Event-Id");
try (PreparedStatement ps = db.prepareStatement(
        "INSERT INTO processed_webhooks (event_id) VALUES (?) " +
        "ON CONFLICT (event_id) DO NOTHING")) {
    ps.setString(1, eventId);
    int rows = ps.executeUpdate();
    if (rows == 0) {
        response.setStatus(200);
        return; // duplicate — ack and skip
    }
}
// process event for the first time...
```

**Retry policy (server-side, as of 2026-05-12):** retries fire only for `408`, `425`, `429`, `502`, `503`, `504` and network errors (~44h bounded backoff). Returning any other 4xx/5xx — including `500` — terminates the retry loop immediately. If your handler wants a retry on a transient failure, return `503` (not `500`).

The signature contract is **unchanged** — `WebhookSignature.verify(...)` continues to work without code changes.

---

### Webhook Pull Queue

Alternative to push webhooks -- poll for events.

#### `webhooks().queue().pull(limit, eventType)` -- Fetch pending events

```java
WebhookQueueResponse queue = client.webhooks().queue().pull(50, "document.received");
for (var item : queue.items()) {
    System.out.println(item.id() + " " + item.type() + " " + item.payload());
}
// queue.hasMore()
```

#### `webhooks().queue().ack(eventId)` -- Acknowledge single event

```java
client.webhooks().queue().ack("event-uuid");
// void (HTTP 204)
```

#### `webhooks().queue().batchAck(eventIds)` -- Batch acknowledge

```java
List<String> ids = queue.items().stream().map(i -> i.id()).toList();
client.webhooks().queue().batchAck(ids);
// void (HTTP 204)
```

#### `webhooks().queue().pullAll(limit, since)` -- Cross-firm queue (integrator)

```java
WebhookQueueAllResponse all = client.webhooks().queue().pullAll(200, "2026-04-01T00:00:00Z");
for (var event : all.items()) {
    System.out.println(event.firmId() + " " + event.event() + " " + event.payload());
}
// all.hasMore()
```

#### `webhooks().queue().batchAckAll(eventIds)` -- Cross-firm batch ack

```java
List<String> ids = all.items().stream().map(e -> e.eventId()).toList();
var result = client.webhooks().queue().batchAckAll(ids);
System.out.println(result.acknowledged());
```

---

### Reporting

#### `reporting().statistics(from, to)` -- Aggregated stats

```java
Statistics stats = client.reporting().statistics("2026-01-01", "2026-03-31");
// stats.period(), stats.outbound(), stats.inbound()
```

---

### Account

#### `account().get()` -- Account info

```java
Account account = client.account().get();
// account.firm(), account.plan(), account.usage()
```

---

### Extract (AI OCR)

Requires Enterprise plan.

#### `extract().single(fileBytes, fileName, mimeType)` -- Single file

```java
byte[] pdf = java.nio.file.Files.readAllBytes(java.nio.file.Path.of("invoice.pdf"));
ExtractResult result = client.extract().single(pdf, "invoice.pdf", "application/pdf");
// result.extraction(), result.ublXml(), result.confidence()
```

Supported MIME types: `application/pdf`, `image/jpeg`, `image/png`, `image/webp`. Max 20 MB.

#### `extract().batch(files)` -- Batch extraction (max 10 files)

```java
BatchExtractResult result = client.extract().batch(List.of(
    new ExtractResource.FileInput(pdfBytes, "inv1.pdf", "application/pdf"),
    new ExtractResource.FileInput(imageBytes, "inv2.png", "image/png")
));
// result.batchId(), result.total(), result.successful(), result.failed(), result.results()
```

---

## Integrator Mode

Use `sk_int_*` keys to act on behalf of client firms.

```java
// Option 1: pass firmId in builder
EPostak client = EPostak.builder()
    .clientId("sk_int_xxxxx")
    .clientSecret("sk_int_xxxxx")
    .firmId("client-firm-uuid")
    .build();

// Option 2: scope at call time with withFirm()
EPostak base = EPostak.builder()
    .clientId("sk_int_xxxxx")
    .clientSecret("sk_int_xxxxx")
    .build();
EPostak clientA = base.withFirm("firm-uuid-a");
EPostak clientB = base.withFirm("firm-uuid-b");

clientA.documents().send(...);
clientB.documents().inbox().list();
```

### Integrator-only endpoints

| Method                                | Description                       |
| ------------------------------------- | --------------------------------- |
| `firms().assign(ico)`                 | Link a firm to the integrator     |
| `firms().assignBatch(icos)`           | Batch link firms (max 50)         |
| `documents().inbox().listAll(...)`    | Cross-firm inbox with firm filter |
| `webhooks().queue().pullAll(...)`     | Cross-firm event queue            |
| `webhooks().queue().batchAckAll(ids)` | Cross-firm batch acknowledge      |

---

## Error Handling

All errors are thrown as `EPostakException` (extends `RuntimeException`):

```java
import sk.epostak.sdk.EPostakException;

try {
    client.documents().send(...);
} catch (EPostakException e) {
    System.err.println(e.getStatus());   // HTTP status code (0 for network errors)
    System.err.println(e.getCode());     // Machine-readable code, e.g. "VALIDATION_FAILED"
    System.err.println(e.getMessage());  // Human-readable message
    System.err.println(e.getDetails());  // Validation details (for 422 errors)
}
```

### Common error codes

| Status | Code                   | Meaning                                          |
| ------ | ---------------------- | ------------------------------------------------ |
| 400    | `BAD_REQUEST`          | Invalid request body or parameters               |
| 401    | `UNAUTHORIZED`         | Missing or invalid API key                       |
| 403    | `FORBIDDEN`            | Insufficient permissions or wrong plan           |
| 404    | `NOT_FOUND`            | Resource not found                               |
| 409    | `CONFLICT`             | Duplicate operation (e.g. firm already assigned) |
| 422    | `UNPROCESSABLE_ENTITY` | Validation failed (check `getDetails()`)         |
| 429    | `RATE_LIMITED`         | Too many requests                                |
| 503    | `SERVICE_UNAVAILABLE`  | Extraction service not configured                |

---

## Full API Endpoint Map

| SDK Method                            | HTTP   | Path                                 |
| ------------------------------------- | ------ | ------------------------------------ |
| `documents().get(id)`                 | GET    | `/documents/{id}`                    |
| `documents().update(id, body)`        | PATCH  | `/documents/{id}`                    |
| `documents().send(body)`              | POST   | `/documents/send`                    |
| `documents().sendBatch(items)`        | POST   | `/documents/send/batch`              |
| `documents().status(id)`              | GET    | `/documents/{id}/status`             |
| `documents().statusBatch(ids)`        | POST   | `/documents/status/batch`            |
| `documents().evidence(id)`            | GET    | `/documents/{id}/evidence`           |
| `documents().evidenceBundle(id)`      | GET    | `/documents/{id}/evidence-bundle`    |
| `documents().envelope(id)`            | GET    | `/documents/{id}/envelope`           |
| `documents().pdf(id)`                 | GET    | `/documents/{id}/pdf`                |
| `documents().ubl(id)`                 | GET    | `/documents/{id}/ubl`                |
| `documents().respond(id, body)`       | POST   | `/documents/{id}/respond`            |
| `documents().mark(id, state, note)`   | POST   | `/documents/{id}/mark`               |
| `documents().validate(body)`          | POST   | `/documents/validate`                |
| `documents().preflight(...)`          | POST   | `/documents/preflight`               |
| `documents().convert(...)`            | POST   | `/documents/convert`                 |
| `documents().parse(xml)`              | POST   | `/documents/parse`                   |
| `documents().outbox(...)`             | GET    | `/documents/outbox`                  |
| `documents().responses(id)`           | GET    | `/documents/{id}/responses`          |
| `documents().events(id, ...)`         | GET    | `/documents/{id}/events`             |
| `documents().peppolDocuments(...)`    | GET    | `/peppol-documents`                  |
| `documents().inbox().list(...)`       | GET    | `/documents/inbox`                   |
| `documents().inbox().get(id)`         | GET    | `/documents/inbox/{id}`              |
| `documents().inbox().acknowledge(id)` | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents().inbox().listAll(...)`    | GET    | `/documents/inbox/all`               |
| `peppol().lookup(scheme, id)`         | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol().directory().search(...)`    | GET    | `/peppol/directory/search`           |
| `peppol().companyLookup(ico)`         | GET    | `/company/lookup/{ico}`              |
| `peppol().companySearch(q, limit)`    | GET    | `/company/search`                    |
| `peppol().resolve(params)`            | GET    | `/peppol/participants/resolve`       |
| `peppol().capabilities(request)`      | POST   | `/peppol/capabilities`               |
| `peppol().lookupBatch(participants)`  | POST   | `/peppol/participants/batch`         |
| `firms().list()`                      | GET    | `/firms`                             |
| `firms().get(id)`                     | GET    | `/firms/{id}`                        |
| `firms().documents(id, ...)`          | GET    | `/firms/{id}/documents`              |
| `firms().registerPeppolId(id, ...)`   | POST   | `/firms/{id}/peppol-identifiers`     |
| `firms().assign(ico)`                 | POST   | `/firms/assign`                      |
| `firms().assignBatch(icos)`           | POST   | `/firms/assign/batch`                |
| `webhooks().create(url, events)`      | POST   | `/webhooks`                          |
| `webhooks().list()`                   | GET    | `/webhooks`                          |
| `webhooks().get(id)`                  | GET    | `/webhooks/{id}`                     |
| `webhooks().update(id, ...)`          | PATCH  | `/webhooks/{id}`                     |
| `webhooks().delete(id)`               | DELETE | `/webhooks/{id}`                     |
| `webhooks().test(id, params)`         | POST   | `/webhooks/{id}/test`                |
| `webhooks().deliveries(id, params)`   | GET    | `/webhooks/{id}/deliveries`          |
| `webhooks().deadLetters(params)`      | GET    | `/webhook-dead-letter`               |
| `webhooks().replayDeadLetter(id)`     | POST   | `/webhook-dead-letter/{id}/replay`   |
| `webhooks().resolveDeadLetter(id, reason)` | POST | `/webhook-dead-letter/{id}/resolve` |
| `webhooks().queue().pull(...)`        | GET    | `/webhook-queue`                     |
| `webhooks().queue().ack(eventId)`     | DELETE | `/webhook-queue/{eventId}`           |
| `webhooks().queue().batchAck(ids)`    | POST   | `/webhook-queue/batch-ack`           |
| `webhooks().queue().pullAll(...)`     | GET    | `/webhook-queue/all`                 |
| `webhooks().queue().batchAckAll(ids)` | POST   | `/webhook-queue/all/batch-ack`       |
| `reporting().statistics(...)`         | GET    | `/reporting/statistics`              |
| `reporting().submissions(...)`        | GET    | `/reporting/submissions`             |
| `account().get()`                     | GET    | `/account`                           |
| `extract().single(...)`               | POST   | `/extract`                           |
| `extract().batch(...)`                | POST   | `/extract/batch`                     |
| `EPostak.validate(xml)`               | POST   | `https://epostak.sk/api/validate`    |
| `connector().preflight(request)`      | POST   | `/connector/preflight`               |
| `connector().send(body, idempotencyKey)` | POST | `/connector/send`                    |
| `connector().stageOutbox(request)`    | POST   | `/connector/outbox`                  |
| `connector().listOutbox(params)`      | GET    | `/connector/outbox`                  |
| `connector().getOutboxItem(outboxId)` | GET    | `/connector/outbox/{outboxId}`       |
| `connector().sendOutboxItem(outboxId, options)` | POST | `/connector/outbox/{outboxId}/send` |
| `connector().sendOutboxBatch(request)` | POST  | `/connector/outbox/send`             |
| `connector().cancelOutboxItem(outboxId)` | DELETE | `/connector/outbox/{outboxId}`     |
| `connector().status(documentId)`      | GET    | `/connector/status/{documentId}`     |
| `connector().inbox(params)`           | GET    | `/connector/inbox`                   |
| `connector().getInboxDocument(documentId)` | GET | `/connector/inbox/{documentId}`      |
| `connector().ack(documentId)`         | POST   | `/connector/inbox/{documentId}/ack`  |
| `connector().events(params)`          | GET    | `/connector/events`                  |
| `inbound().list(params)`              | GET    | `/inbound/documents`                 |
| `inbound().get(id)`                   | GET    | `/inbound/documents/{id}`            |
| `inbound().getUbl(id)`                | GET    | `/inbound/documents/{id}/ubl`        |
| `inbound().ack(id, params)`           | POST   | `/inbound/documents/{id}/ack`        |
| `outbound().list(params)`             | GET    | `/outbound/documents`                |
| `outbound().get(id)`                  | GET    | `/outbound/documents/{id}`           |
| `outbound().getUbl(id)`               | GET    | `/outbound/documents/{id}/ubl`       |
| `outbound().getMdn(id)`               | GET    | `/outbound/documents/{id}/mdn`       |
| `outbound().events(params)`           | GET    | `/outbound/events`                   |
| `account().licenseInfo()`             | GET    | `/licenses/info`                     |
| `integrator().keys().list()`          | GET    | `/integrator/keys`                   |
| `integrator().keys().deactivateByKeyId(id)` | DELETE | `/integrator/keys`             |
| `integrator().keys().deactivateByClientId(clientId)` | DELETE | `/integrator/keys`       |
| `integrator().licenses().info()`      | GET    | `/integrator/licenses/info`          |
| `sapi().send(body, participantId, idempotencyKey)` | POST | `/sapi/v1/document/send`      |
| `sapi().receive(participantId, ...)`  | GET    | `/sapi/v1/document/receive`          |
| `sapi().get(id, participantId)`       | GET    | `/sapi/v1/document/receive/{id}`     |
| `sapi().acknowledge(id, participantId)` | POST | `/sapi/v1/document/receive/{id}/acknowledge` |

Production Enterprise paths are relative to `https://epostak.sk/api/v1`; test Enterprise paths use `https://dev.epostak.sk/api/v1`. SAPI uses the same host, for example `https://epostak.sk/sapi/v1` or `https://dev.epostak.sk/sapi/v1`.

---

## Full API Documentation

https://epostak.sk/api/docs/enterprise
