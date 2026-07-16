# epostak-sdk-java

Official Java SDK for the [ePošťák APIs](https://epostak.sk/api/docs) -- managed
Connector workflows, the Enterprise API, and SAPI-SK interoperability.

Java 17+. Single dependency: Gson. Uses `java.net.http.HttpClient` (built-in since Java 11).

---

## Installation

The Java SDK is currently distributed as reviewed source, not through Maven
Central. Clone and install it into your local Maven repository first:

```bash
git clone https://github.com/staniduris/epostak-sdk.git
cd epostak-sdk/java
mvn clean install
```

Then use the local artifact.

### Maven

```xml
<dependency>
    <groupId>sk.epostak</groupId>
    <artifactId>epostak-sdk</artifactId>
    <version>1.1.0</version>
</dependency>
```

### Gradle

```groovy
repositories { mavenLocal() }
implementation 'sk.epostak:epostak-sdk:1.1.0'
```

## Major release API shape

Java `1.1.0` is the current workflow-first source release with the managed
Connector surface:

- Enterprise direct firm flow: `client.enterprise().documents().send(...)`
- Connector ERP/integrator flow: `client.connector().customers().forCustomer("erp-customer").documents().send(...)`
- Enterprise API facade flow: `client.enterprise().payloads().validate(...)`, `client.enterprise().events().pull(...)`, `client.enterprise().documents().supportPacket(...)`
- SAPI-SK interoperable flow: `client.sapi().participants().forParticipant("0245:1234567890").documents().send(...)`

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

```java
EPostak connectorClient = EPostak.builder()
    .clientId(System.getenv("EPOSTAK_CONNECTOR_CLIENT_ID"))
    .clientSecret(System.getenv("EPOSTAK_CONNECTOR_CLIENT_SECRET"))
    .build();
var customer = connectorClient.connector().customers().forCustomer("erp-acme");
var document = customer.documents().send(new ConnectorBusinessDocumentRequest(
    "invoice-2026-0042",
    "2026-0042",
    ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
    List.of(new ConnectorBusinessLine("Monthly licence", 1, 100, 23))
));
ConnectorBusinessDocument detail = customer.documents().getBusinessDocument(document.id());
var events = customer.events().list(new ConnectorListParams(null, 50));
```

`getBusinessDocument(id)` is the typed detail API for new integrations.
`get(id)` remains available with its original `Map<String, Object>` return type
for source and binary compatibility; both calls are customer-scoped.

The integrator owns the stable `customerRef` after ePošťák has approved and
Peppol-registered the firm. The SDK cannot create or discover firms. It injects `customerRef`, sends
immediately by default, and applies the backend ECMAScript `TrimString` set
(`0009-000D`, `0020`, `00A0`, `1680`, `2000-200A`, `2028-2029`, `202F`,
`205F`, `3000`, `FEFF`) to `customerRef` and `externalId`; `U+0085` is
preserved. It then derives `connector:v1:` plus lowercase SHA-256 over both
UTF-8 values, each prefixed by its four-byte big-endian byte length. Generated
keys are 77 ASCII characters; explicit keys must be 1-255 UTF-8 bytes and are
otherwise unchanged.
For approval queues, call `documents().stage(request)` and then either
`sendDocument(staged.id())` or `cancelDocument(staged.id())`.

Managed Connector credentials support customer documents/events, one global
webhook, audited document evidence, and customer-scoped Mapper preview. Legacy
helpers and `enterprise().connector()` remain silently supported for source
compatibility.

```java
var polled = customer.events().list(new ConnectorListParams(null, 50));
var configuredWebhook = connectorClient.connector().webhook().configure(
    "https://erp.example.com/webhooks/epostak",
    List.of("document.received", "document.delivered")
);
if (configuredWebhook.secret() != null) {
    // Persist this value in your server-side secret manager now. It is returned only once.
    String oneTimeWebhookSecret = configuredWebhook.secret();
}
connectorClient.connector().webhook().test("erp-acme");
```

Push uses the same canonical event item as polling with `customerRef` at the
root. `WebhookSignature.verify(...)` verifies HMAC-SHA256 over
`timestamp + "." + rawBody`.

### Acknowledge locally or respond to the supplier

```java
String receivedDocumentId = "document-id-from-list-or-event";
customer.documents().acknowledge(receivedDocumentId, "erp:" + receivedDocumentId);
ConnectorInvoiceResponseResult response = customer.documents().respond(
    receivedDocumentId,
    new ConnectorInvoiceResponseRequest("accepted", "Imported and accepted")
);

// Direct alternative (do not call both); customerRef is still mandatory:
// connectorClient.connector().documents().respond(
//     receivedDocumentId, "erp-acme", new ConnectorInvoiceResponseRequest("accepted")
// );
```

`acknowledge` only records that the inbound document was processed locally; it
does not notify the supplier. `respond` sends the network business response via
`POST /connector/documents/{documentId}/respond?customerRef=...`. Use only the
business statuses `received`, `in_process`, `under_query`,
`conditionally_accepted`, `rejected`, `accepted`, or `paid`, with an optional
`note`. The Connector handles Peppol response codes and XML; do not send either.
The result reports `response().delivery()` as `sent` or `queued` and marks safe
replays with `idempotent()`.

## Enterprise API facade flow

Use this as the default low-friction path for ERP integrations that do not want
to receive webhooks on day one:

```java
client.enterprise().payloads().validate(request);

client.enterprise().documents().preflight(request.getReceiverPeppolId());

SendDocumentResponse sent = client.enterprise().documents().send(
    request,
    "erp-fa-2026-001"
);

WebhookQueueResponse events = client.enterprise().events().pull(50, null);
client.enterprise().events().batchAck(
    events.items().stream()
        .map(WebhookQueueResponse.WebhookQueueItem::eventId)
        .toList()
);

byte[] supportPacket = client.enterprise().documents().supportPacket(sent.documentId());
```

The older `client.enterprise().webhooks().queue()` resource remains available
for compatibility. New pull-based integrations should prefer
`client.enterprise().events()`.

Non-breaking adoption: facade helpers are additive. Existing
`client.enterprise().extract()`, `client.enterprise().documents().validate()`,
`client.enterprise().webhooks().queue()`, and
`client.enterprise().documents().evidenceBundle()` integrations can keep
running; migrate to `payloads()`, `events()`, and `supportPacket()` when your
release window allows.

## Recent changes

### Included in v1.1.0 — 2026-07-14

- Connector is canonical at `client.connector()`; the Enterprise namespace
  alias is supported compatibility only.
- Connector credentials are approved by ePošťák; the integrator chooses each
  stable `customerRef` in the dashboard after firm approval. Enterprise keys
  and OAuth do not grant Connector access.
- Customer document creation snapshots the request, validates explicit
  1-255-byte idempotency keys, and retries network errors, `429`, and all `5xx`
  only when safe. Lifecycle calls are server-idempotent; `409` is never retried.
- `EPostakException` exposes `getField()`, `getNextAction()`, `isRetryable()`,
  `getRequestId()`, and `getRetryAfter()`.
- Identity hashing uses the exact backend ECMAScript `TrimString` code points;
  `U+0085` is preserved.
- `document.cancelled` is a first-class business event.

### Included in v1.1.0 — 2026-07-12

- Connector now leads with
  `client.connector().customers().forCustomer(customerRef).documents()/events()`.
  Lower-level workflows live under `client.connector().advanced()`; existing
  top-level methods remain supported compatibility aliases. There is no
  customer creation API in the SDK.
- JSON billing payloads now expose the live receiver address, `prepaidAmount`,
  `prepayments`, and advanced line-item VAT/classification fields from the
  Enterprise OpenAPI. `SendDocumentRequest` serializes these fields with the
  live camelCase JSON names.

### Included in v1.1.0 — 2026-07-01

- `client.enterprise().payloads().validate(...)`,
  `client.enterprise().events().pull(...)`, and
  `client.enterprise().documents().supportPacket(...)` cover the Enterprise API
  facade flow for validation, event pull/ack, and support packets.

### Included in v1.1.0 — 2026-06-30

- Customer-scoped
  `client.connector().customers().forCustomer(customerRef).advanced().mapper(...)`
  is the managed, preview-only Mapper flow. Top-level
  `client.connector().advanced().mapper(...)` remains a legacy compatibility
  alias and is unavailable with managed Connector credentials.
- `client.box()` / `client.enterprise().box()` covers ePošťák Box list, create
  with `BoxCreateRequest.payloadXml`, detail, schedule, send-now, retry, and
  cancel over `/box/items`.

### v1.0.0 — 2026-06-14

- `client.enterprise()` is the documented namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces.
- `client.sapi().participants().forParticipant(participantId).documents()`
  requires participant scoping before SAPI document send/receive/get/acknowledge.
- `client.connector().customers().forCustomer(customerRef)`
  injects `customerRef` and keeps `X-Firm-Id` off customer-managed Connector
  calls.
- The Connector compatibility surface includes preflight, Zen input, Autopilot,
  reconcile, mailbox, sync, document evidence, and event polling.
- Docs: added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence.

### v0.10.0 — 2026-05-18

- `client.sapi().participants().forParticipant(id).documents()` covers SAPI-SK 1.0 document send, receive list/detail, and acknowledge.
- `client.enterprise().webhooks().test(id, new WebhookTestParams().count(250).mode("queued"))` supports direct and queued webhook tests.
- `client.enterprise().webhooks().deadLetters(...)`, `replayDeadLetter(id)`, and `resolveDeadLetter(id, reason)` cover webhook DLQ operations.
- `client.enterprise().peppol().resolve(...)` resolves ERP identifiers to Peppol participant + routing capability.
- Added evidence bundle, outbound MDN, company search, Peppol document listing, and license info helpers.

---

## Quick start

```java
import sk.epostak.sdk.EPostak;
import sk.epostak.sdk.models.*;

import java.util.List;
import java.util.Map;

EPostak client = EPostak.builder()
    .clientId("sk_live_xxxxx")
    .clientSecret("sk_live_xxxxx")
    .build();

// Send an invoice
SendDocumentResponse result = client.enterprise().documents().send(
    SendDocumentRequest.builder("0245:1234567890")
        .receiverName("Firma s.r.o.")
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

### Connector for ERP developers

The primary Connector surface is customer-scoped and uses business data.
ePošťák approves and Peppol-registers the firm; the integrator then chooses its
stable `customerRef` in the dashboard. The SDK does not expose signup or
customer creation.

```java
EPostak connectorClient = EPostak.builder()
    .clientId(System.getenv("EPOSTAK_CONNECTOR_CLIENT_ID"))
    .clientSecret(System.getenv("EPOSTAK_CONNECTOR_CLIENT_SECRET"))
    .build();
var customer = connectorClient.connector().customers().forCustomer("erp-customer-1");

ConnectorBusinessDocument sent = customer.documents().send(
    new ConnectorBusinessDocumentRequest(
        "FA-2026-001",
        "FA-2026-001",
        ConnectorBusinessRecipient.byTaxId("SK", "2120123456"),
        List.of(new ConnectorBusinessLine("Služby", 1, 100, 23))
    )
);
ConnectorBusinessDocument detail = customer.documents().getBusinessDocument(sent.id());

ConnectorBusinessDocumentListResponse inbox = customer.documents().list(
    new ConnectorBusinessDocumentListParams("inbound", null, null, null, null, 20)
);
for (ConnectorBusinessDocument doc : inbox.documents()) {
    customer.documents().acknowledge(doc.id(), "erp-import-" + doc.id());
}

ConnectorBusinessEventsResponse events = customer.events().list(new ConnectorListParams(null, 50));
String ubl = customer.advanced().documents().ubl(sent.id());
Map<String, Object> evidence = customer.advanced().documents().evidence(sent.id());
Map<String, Object> evidenceBundle = customer.advanced().documents().evidenceBundle(sent.id());
Map<String, Object> supportPacket = customer.advanced().documents().supportPacket(sent.id());
```

Every customer-scoped get, acknowledge, lifecycle, UBL, and evidence request
appends the URL-encoded `customerRef` query. The server verifies the document
against the approved firm mapped to that integrator-owned reference.

For a stage-first workflow, the lifecycle transitions are also customer-scoped
and send no request body:

```java
ConnectorBusinessDocument staged = customer.documents().stage(request);
ConnectorBusinessDocument sentLater = customer.documents().sendDocument(staged.id());
// Or, before delivery: customer.documents().cancelDocument(staged.id());
```

Mapper is a customer-scoped preview/normalization helper; it never stages or
sends a document:

```java
Map<String, Object> preview = customer.advanced().mapper(Map.of(
    "templateKey", "pohoda-csv-v1",
    "sourceType", "csv",
    "sourceText", csvText
));
```

Legacy preflight, raw send/status, inbox, outbox, Autopilot, Zen, reconcile,
mailbox, sync, and action helpers remain supported source-compatibility aliases
but are not callable with managed Connector credentials.

### Connector errors and retries

```java
try {
    customer.documents().send(request);
} catch (EPostakException error) {
    System.err.println(error.getCode() + " " + error.getMessage());
    System.err.println(error.getField() + " " + error.getNextAction());
    System.err.println(error.isRetryable() + " " + error.getRequestId()
        + " " + error.getRetryAfter());
}
```

Keyed document creation retries network failures, `429`, and every `5xx` while
reusing the exact body and key. Lifecycle send/cancel/acknowledge calls are
server-idempotent and use the same policy. `Retry-After` is honored; `409` is
always surfaced once and never retried automatically.

---

## SAPI-SK participant flow

```java
Map<String, Object> sapiDocument = Map.of(
    "metadata", Map.of(
        "documentId", "FA-2026-001",
        "documentTypeId", "invoice",
        "processId", "billing",
        "senderParticipantId", "0245:1234567890",
        "receiverParticipantId", "0245:0987654321",
        "creationDateTime", "2026-06-14T10:00:00Z"
    ),
    "payload", "<Invoice/>",
    "payloadFormat", "XML"
);

client.sapi()
    .participants()
    .forParticipant("0245:1234567890")
    .documents()
    .send(sapiDocument, "sapi-fa-2026-001");
```

---

## Authentication

| Key prefix  | Use case                                            |
| ----------- | --------------------------------------------------- |
| `sk_live_*` | Direct access -- acts on behalf of your own firm    |
| `sk_int_*`  | Product-scoped integrator access; Connector entitlement is approved separately |

### Constructor options

```java
EPostak client = EPostak.builder()
    .clientId("sk_live_xxxxx")      // Required
    .clientSecret("sk_live_xxxxx")  // Required
    .baseUrl("https://dev.epostak.sk/api/v1") // Optional test env; omit for prod
    .firmId("uuid")                  // Optional, for legacy firm-scoped calls
    .build();
```

Connector calls do not need `firmId`; use the stable `customerRef` chosen in
the dashboard after firm approval. Use `firmId` or `withFirm(...)` only for
Enterprise calls that target one firm directly. Enterprise credentials cannot
provision Connector.

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
SendDocumentResponse result = client.enterprise().documents().send(
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
client.enterprise().documents().send(
    SendDocumentRequest.builder("0245:1234567890")
        .xml("<?xml version=\"1.0\" encoding=\"UTF-8\"?>...")
        .build()
);
```

#### `documents().get(id)` -- Get a document by ID

```java
Document doc = client.enterprise().documents().get("doc-uuid");
// doc.getId(), doc.getNumber(), doc.getStatus(), doc.getDirection(),
// doc.getSupplier(), doc.getCustomer(), doc.getLines(), doc.getTotals()
```

#### `documents().update(id, request)` -- Update a draft document

```java
Document updated = client.enterprise().documents().update("doc-uuid",
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
DocumentStatusResponse status = client.enterprise().documents().status("doc-uuid");
// status.status(), status.statusHistory(), status.deliveredAt()
```

#### `documents().evidence(id)` -- Delivery evidence

```java
DocumentEvidenceResponse evidence = client.enterprise().documents().evidence("doc-uuid");
// evidence.as4Receipt(), evidence.mlrDocument(), evidence.invoiceResponse()
```

#### `documents().pdf(id)` -- Download PDF

```java
byte[] pdf = client.enterprise().documents().pdf("doc-uuid");
java.nio.file.Files.write(java.nio.file.Path.of("invoice.pdf"), pdf);
```

#### `documents().ubl(id)` -- Download UBL XML

```java
String ubl = client.enterprise().documents().ubl("doc-uuid");
```

#### `documents().respond(id, request)` -- Send invoice response

```java
InvoiceRespondResponse response = client.enterprise().documents().respond("doc-uuid",
    new InvoiceRespondRequest("AP", "Invoice accepted"));
// "AP" = accepted, "RE" = rejected, "UQ" = under query
```

#### `documents().validate(request)` -- Validate without sending

```java
ValidationResult validation = client.enterprise().documents().validate(
    SendDocumentRequest.builder("0245:1234567890")
        .receiverName("Firma s.r.o.")
        .items(List.of(
            new SendDocumentRequest.LineItem("Test", 1, 100, 23)
        ))
        .build()
);
// validation.valid(), validation.warnings(), validation.ubl()
```

#### `documents().preflight(receiverPeppolId, documentTypeId)` -- Check receiver capability

```java
PreflightResult check = client.enterprise().documents().preflight("0245:1234567890", null);
// check.registered(), check.supportsDocumentType(), check.smpUrl()
```

#### `documents().convert(inputFormat, outputFormat, document)` -- Convert between JSON and UBL

```java
// JSON to UBL
ConvertResult result = client.enterprise().documents().convert(
    "json",
    "ubl",
    Map.of("invoiceNumber", "FV-001", "items", List.of(...))
);
// result.outputFormat() == "ubl", result.document() is a UBL XML String, result.warnings()

// UBL to JSON
ConvertResult result = client.enterprise().documents().convert(
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
InboxListResponse inbox = client.enterprise().documents().inbox().list(0, 20, "RECEIVED", null);
// inbox.documents(), inbox.total(), inbox.limit(), inbox.offset()
```

#### `documents().inbox().get(id)` -- Full detail with UBL XML

```java
InboxDocumentDetailResponse detail = client.enterprise().documents().inbox().get("doc-uuid");
// detail.document(), detail.payload() (UBL XML string or null)
```

#### `documents().inbox().acknowledge(id)` -- Mark as processed

```java
AcknowledgeResponse ack = client.enterprise().documents().inbox().acknowledge("doc-uuid");
// ack.status() == "ACKNOWLEDGED"
```

#### `documents().inbox().listAll(...)` -- Cross-firm inbox (integrator)

```java
InboxAllResponse all = client.enterprise().documents().inbox().listAll(0, 50, "RECEIVED", null, null);
// Each document includes firmId and firmName
```

---

### Peppol

#### `peppol().lookup(scheme, identifier)` -- SMP participant lookup

```java
PeppolParticipant participant = client.enterprise().peppol().lookup("0245", "12345678");
if (participant.accepts() && "ready".equals(participant.routingStatus())) {
    // Receiver is registered and routable for the default BIS Billing invoice.
}

CapabilitiesResponse caps = client.enterprise().peppol().capabilities(new CapabilitiesRequest(
    "0245",
    "12345678",
    "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."
));
```

#### `peppol().directory().search(q, country, page, pageSize)` -- Business Card directory

```java
DirectorySearchResult results = client.enterprise().peppol().directory().search(
    "Telekom", "SK", 0, 20);
// results.results(), results.total()
```

#### `peppol().companyLookup(ico)` -- Slovak company lookup

```java
CompanyLookup company = client.enterprise().peppol().companyLookup("12345678");
// company.ico(), company.name(), company.dic(), company.peppolId()

Map<String, Object> matches = client.enterprise().peppol().companySearch("Demo", 10);

Map<String, Object> resolved = client.enterprise().peppol().resolve(Map.of(
    "ico", "12345678",
    "documentTypeId", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."));
```

---

### Firms (integrator keys)

#### `firms().list()` -- List all accessible firms

```java
List<FirmSummary> firms = client.enterprise().firms().list();
// Each: id, name, ico, peppolId, peppolStatus
```

#### `firms().get(id)` -- Firm detail

```java
FirmDetail firm = client.enterprise().firms().get("firm-uuid");
// firm.peppolIdentifiers(), firm.address(), firm.createdAt()
```

#### `firms().documents(id, offset, limit, direction)` -- List firm documents

```java
InboxListResponse docs = client.enterprise().firms().documents("firm-uuid", 0, 20, "inbound");
```

#### `firms().registerPeppolId(id, scheme, identifier)` -- Register Peppol ID

```java
var result = client.enterprise().firms().registerPeppolId("firm-uuid", "0245", "12345678");
// result.peppolId(), result.registeredAt()
```

#### `firms().assign(ico)` -- Assign firm to integrator

```java
AssignFirmResponse result = client.enterprise().firms().assign("12345678");
// result.firm(), result.status()
```

#### `firms().assignBatch(icos)` -- Batch assign firms (max 50)

```java
BatchAssignResponse result = client.enterprise().firms().assignBatch(
    List.of("12345678", "87654321", "11223344"));
// result.results() -- each with ico, firm, status, error
```

---

### Webhooks

#### `webhooks().create(url, events)` -- Register a webhook

```java
WebhookDetail webhook = client.enterprise().webhooks().create(
    "https://example.com/webhook",
    List.of("document.received", "document.sent"));
// Store webhook.secret() for HMAC-SHA256 signature verification
```

#### `webhooks().list()` -- List webhooks

```java
List<Webhook> webhooks = client.enterprise().webhooks().list();
```

#### `webhooks().get(id)` -- Webhook detail with deliveries

```java
WebhookDetail detail = client.enterprise().webhooks().get("webhook-uuid");
// detail.deliveries()

WebhookTestResponse queued = client.enterprise().webhooks().test(
    "webhook-uuid",
    new WebhookTestParams()
        .event("document.received")
        .count(250)
        .mode("queued"));

WebhookDeliveriesResponse deliveries = client.enterprise().webhooks().deliveries(
    "webhook-uuid",
    Map.of("testRunId", queued.testRunId(), "includeResponseBody", true));

Map<String, Object> dlq = client.enterprise().webhooks().deadLetters(Map.of("includeResponseBody", true));
client.enterprise().webhooks().replayDeadLetter("delivery-id");
client.enterprise().webhooks().resolveDeadLetter("delivery-id", "Handled in ERP");
```

#### `webhooks().update(id, url, events, isActive)` -- Update webhook

```java
client.enterprise().webhooks().update("webhook-uuid",
    "https://example.com/new-webhook",
    List.of("document.received"),
    true);
```

#### `webhooks().delete(id)` -- Delete webhook

```java
client.enterprise().webhooks().delete("webhook-uuid");
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
> used by the server. Use `client.enterprise().webhooks().create(url, events)` to register webhook endpoints.

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

### Events pull facade

Alternative to push webhooks -- poll for events.

#### `events().pull(limit, eventType)` -- Fetch pending events

```java
WebhookQueueResponse queue = client.enterprise().events().pull(50, "document.received");
for (var item : queue.items()) {
    System.out.println(item.eventId() + " " + item.event() + " " + item.payload());
}
// queue.hasMore()
```

#### `events().ack(eventId)` -- Acknowledge single event

```java
client.enterprise().events().ack("event-uuid");
```

#### `events().batchAck(eventIds)` -- Batch acknowledge

```java
List<String> ids = queue.items().stream().map(i -> i.eventId()).toList();
client.enterprise().events().batchAck(ids);
```

The older `client.enterprise().webhooks().queue()` resource remains available
for legacy queue and cross-firm drain jobs.

#### `webhooks().queue().pullAll(limit, since)` -- Cross-firm queue (integrator)

```java
WebhookQueueAllResponse all = client.enterprise().webhooks().queue().pullAll(200, "2026-04-01T00:00:00Z");
for (var event : all.items()) {
    System.out.println(event.firmId() + " " + event.event() + " " + event.payload());
}
// all.hasMore()
```

#### `webhooks().queue().batchAckAll(eventIds)` -- Cross-firm batch ack

```java
List<String> ids = all.items().stream().map(e -> e.eventId()).toList();
var result = client.enterprise().webhooks().queue().batchAckAll(ids);
System.out.println(result.acknowledged());
```

---

### Reporting

#### `reporting().statistics(from, to)` -- Aggregated stats

```java
Statistics stats = client.enterprise().reporting().statistics("2026-01-01", "2026-03-31");
// stats.period(), stats.outbound(), stats.inbound()
```

---

### Account

#### `account().get()` -- Account info

```java
Account account = client.enterprise().account().get();
// account.firm(), account.plan(), account.usage()
```

---

### Payload Assistant OCR

Requires Enterprise plan.

#### `payloads().extract(fileBytes, fileName, mimeType)` -- Single file

```java
byte[] pdf = java.nio.file.Files.readAllBytes(java.nio.file.Path.of("invoice.pdf"));
ExtractResult result = client.enterprise().payloads().extract(pdf, "invoice.pdf", "application/pdf");
// result.extraction(), result.ublXml(), result.confidence(), response field send_payload
```

Supported MIME types: `application/pdf`, `image/jpeg`, `image/png`, `image/webp`. Max 20 MB.

#### `payloads().extractBatch(files)` -- Batch extraction (max 50 files)

```java
BatchExtractResult result = client.enterprise().payloads().extractBatch(List.of(
    new ExtractResource.FileInput(pdfBytes, "inv1.pdf", "application/pdf"),
    new ExtractResource.FileInput(imageBytes, "inv2.png", "image/png")
));
// result.batchId(), result.total(), result.successful(), result.failed(), result.results()
```

`client.enterprise().extract()` remains available for compatibility; new
integrations should use `client.enterprise().payloads().extract`.

---

## Enterprise Integrator Mode

This section describes Enterprise `sk_int_*` credentials and multi-tenant
Enterprise calls only. It does not provision Connector. Managed Connector uses
its own approved credentials; the integrator chooses each stable `customerRef`
in the dashboard after ePošťák approves the firm. Enterprise keys and OAuth
cannot provision or scout Connector customers, and setting `firmId` does not
change the Connector entitlement.

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

clientA.enterprise().documents().send(...);
clientB.enterprise().documents().inbox().list();
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
    client.enterprise().documents().send(...);
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
| `documents().supportPacket(id)`       | GET    | `/documents/{id}/support-packet`     |
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
| `box().list(params)`                  | GET    | `/box/items`                         |
| `box().create(new BoxCreateRequest(...))` | POST | `/box/items`                         |
| `box().get(itemId)`                   | GET    | `/box/items/{itemId}`                |
| `box().schedule(itemId, request)`     | POST   | `/box/items/{itemId}/schedule`       |
| `box().sendNow(itemId)`               | POST   | `/box/items/{itemId}/send-now`       |
| `box().retry(itemId)`                 | POST   | `/box/items/{itemId}/retry`          |
| `box().cancel(itemId)`                | POST   | `/box/items/{itemId}/cancel`         |
| `connector().customers().forCustomer(ref).documents().send(request)` | POST | `/connector/documents` |
| `connector().customers().forCustomer(ref).documents().stage(request)` | POST | `/connector/documents` |
| `connector().customers().forCustomer(ref).documents().list(params)` | GET | `/connector/documents` |
| `connector().customers().forCustomer(ref).documents().getBusinessDocument(id)` | GET | `/connector/documents/{id}` |
| `connector().customers().forCustomer(ref).documents().get(id)` (`Map` compatibility) | GET | `/connector/documents/{id}` |
| `connector().customers().forCustomer(ref).documents().acknowledge(id, reference)` | POST | `/connector/documents/{id}/acknowledge` |
| `connector().customers().forCustomer(ref).documents().respond(id, request)` | POST | `/connector/documents/{id}/respond` |
| `connector().customers().forCustomer(ref).documents().sendDocument(id)` | POST | `/connector/documents/{id}/send` |
| `connector().customers().forCustomer(ref).documents().cancelDocument(id)` | POST | `/connector/documents/{id}/cancel` |
| `connector().customers().forCustomer(ref).events().list(params)` | GET | `/connector/events` |
| `connector().customers().forCustomer(ref).advanced().mapper(request)` | POST | `/connector/mapper` |
| `connector().customers().forCustomer(ref).advanced().documents().ubl(id)` | GET | `/connector/documents/{id}/ubl` |
| `connector().customers().forCustomer(ref).advanced().documents().evidence(id)` | GET | `/connector/documents/{id}/evidence` |
| `connector().customers().forCustomer(ref).advanced().documents().evidenceBundle(id)` | GET | `/connector/documents/{id}/evidence-bundle` |
| `connector().customers().forCustomer(ref).advanced().documents().supportPacket(id)` | GET | `/connector/documents/{id}/support-packet` |
The remaining Connector rows are legacy compatibility helpers. They require a
separately entitled legacy/Enterprise context and are not available to managed
Connector credentials. Customer Mapper must be called through
`customer.advanced().mapper(...)` and is preview-only.

| Legacy compatibility method | Verb | Path |
| --- | --- | --- |
| `connector().documents().get(documentId)` | GET | `/connector/documents/{documentId}` |
| `connector().documents().ubl(documentId)` | GET | `/connector/documents/{documentId}/ubl` |
| `connector().documents().evidence(documentId)` | GET | `/connector/documents/{documentId}/evidence` |
| `connector().documents().evidenceBundle(documentId)` | GET | `/connector/documents/{documentId}/evidence-bundle` |
| `connector().documents().supportPacket(documentId)` | GET | `/connector/documents/{documentId}/support-packet` |
| `connector().advanced().preflight(request)` | POST | `/connector/preflight` |
| `connector().advanced().send(body, key)` | POST | `/connector/send` |
| `connector().advanced().stageOutbox(request)` | POST | `/connector/outbox` |
| `connector().advanced().listOutbox(params)` | GET | `/connector/outbox` |
| `connector().advanced().getOutboxItem(id)` | GET | `/connector/outbox/{id}` |
| `connector().advanced().sendOutboxItem(id, options)` | POST | `/connector/outbox/{id}/send` |
| `connector().advanced().sendOutboxBatch(request)` | POST | `/connector/outbox/send` |
| `connector().advanced().cancelOutboxItem(id)` | DELETE | `/connector/outbox/{id}` |
| `connector().advanced().mapper(request)` | POST | `/connector/mapper` |
| `connector().advanced().zenInput(request)` | POST | `/connector/zen-input` |
| `connector().advanced().autopilot(request)` | POST | `/connector/autopilot` |
| `connector().advanced().getAutopilotRun(id)` | GET | `/connector/autopilot/{id}` |
| `connector().advanced().sendAutopilotRun(id)` | POST | `/connector/autopilot/{id}/send` |
| `connector().advanced().reconcile(params)` | GET | `/connector/reconcile` |
| `connector().advanced().mailboxes()` | GET | `/connector/mailbox` |
| `connector().advanced().repairMailbox(request)` | POST | `/connector/mailbox/repair` |
| `connector().advanced().updateMailboxSendPolicy(ref, request)` | PATCH | `/connector/mailbox/{ref}/send-policy` |
| `connector().advanced().sync(params)` | GET | `/connector/sync` |
| `connector().advanced().runAction(id, request)` | POST | `/connector/actions/{id}` |
| `connector().advanced().status(documentId)` | GET | `/connector/status/{documentId}` |
| `connector().advanced().inbox(params)` | GET | `/connector/inbox` |
| `connector().advanced().getInboxDocument(id)` | GET | `/connector/inbox/{id}` |
| `connector().advanced().ack(id)` | POST | `/connector/inbox/{id}/ack` |
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
| `sapi().participants().forParticipant(id).documents().send(body, key)` | POST | `/sapi/v1/document/send` |
| `sapi().participants().forParticipant(id).documents().receive(...)` | GET | `/sapi/v1/document/receive` |
| `sapi().participants().forParticipant(id).documents().get(documentId)` | GET | `/sapi/v1/document/receive/{id}` |
| `sapi().participants().forParticipant(id).documents().acknowledge(documentId)` | POST | `/sapi/v1/document/receive/{id}/acknowledge` |

Production Enterprise paths are relative to `https://epostak.sk/api/v1`; test Enterprise paths use `https://dev.epostak.sk/api/v1`. SAPI uses the same host, for example `https://epostak.sk/sapi/v1` or `https://dev.epostak.sk/sapi/v1`.

## Connector webhook debugger

Inspect the exact signed body and attempt timeline, then use idempotent replay
after fixing the receiver. `runTestSuite` exercises all receiver scenarios.

```java
var failed = connectorClient.connector().webhook().listDeliveries(Map.of("status", "FAILED"));
var detail = connectorClient.connector().webhook().getDelivery(failed.deliveries().get(0).id());
connectorClient.connector().webhook().replayDelivery(detail.delivery().id(), "erp:replay:1", false);
connectorClient.connector().webhook().runTestSuite("erp-acme", null, null, "erp:suite:1");
```

---

## Full API Documentation

https://epostak.sk/api/docs/enterprise
