# ePostak .NET SDK

Official .NET SDK for the [ePošťák APIs](https://epostak.sk/api/docs): managed
Connector workflows, the Enterprise API, and SAPI-SK interoperability.

## Installation

The .NET SDK is currently distributed as reviewed source, not through NuGet.
Clone this repository and add a project reference:

```shell
git clone https://github.com/staniduris/epostak-sdk.git
dotnet add /path/to/your-project.csproj reference /path/to/epostak-sdk/dotnet/src/EPostak/EPostak.csproj
```

## Major release API shape

.NET `1.1.0` is the current workflow-first source release with the managed
Connector surface:

- Enterprise direct firm flow: `client.Enterprise.Documents.SendAsync(...)`
- Connector ERP/integrator flow: `client.Connector.Customers.For("erp-customer").Documents.SendAsync(...)`
- Enterprise API facade flow: `client.Enterprise.Payloads.ValidateAsync(...)`, `client.Enterprise.Events.PullAsync(...)`, `client.Enterprise.Documents.SupportPacketAsync(...)`
- SAPI-SK interoperable flow: `client.Sapi.Participants.For("0245:1234567890").Documents.SendAsync(...)`

Connector is the recommended ERP path; Enterprise is the granular firm-scoped
API; SAPI-SK is the strict participant-scoped profile. ePošťák approves the
integrator, approves and Peppol-registers its firms, and issues Connector
credentials. The integrator then chooses and stores a stable `CustomerRef` for
each approved firm in the dashboard. The SDK cannot create or discover firms.

Request Connector access and Peppol firm approval at `integracie@epostak.sk`,
then set `CustomerRef` in the integrator dashboard. Connector omits
`X-Firm-Id`; SAPI always sends `X-Peppol-Participant-Id`.

Reference: [Connector guide](https://epostak.sk/api/docs/connector) and
[Connector OpenAPI](https://epostak.sk/api/openapi.connector.json).

## Connector quickstart

```csharp
var connectorClient = new EPostakClient(new EPostakConfig
{
    ClientId = Environment.GetEnvironmentVariable("EPOSTAK_CONNECTOR_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("EPOSTAK_CONNECTOR_CLIENT_SECRET")!,
});
var customer = connectorClient.Connector.Customers.For("erp-acme");
var document = await customer.Documents.SendAsync(new ConnectorBusinessDocumentRequest
{
    ExternalId = "invoice-2026-0042",
    Type = "invoice",
    Number = "2026-0042",
    Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
    Lines = [new ConnectorBusinessLine { Description = "Monthly licence", Quantity = 1, UnitPrice = 100, VatRate = 23 }],
});
var detail = await customer.Documents.GetBusinessDocumentAsync(document.Id);
var events = await customer.Events.ListAsync(new ConnectorListParams { Limit = 50 });
var ubl = await customer.Advanced.Documents.UblAsync(document.Id);
var evidence = await customer.Advanced.Documents.EvidenceAsync(document.Id);
var evidenceBundle = await customer.Advanced.Documents.EvidenceBundleAsync(document.Id);
var supportPacket = await customer.Advanced.Documents.SupportPacketAsync(document.Id);
```

`GetBusinessDocumentAsync(id)` is the typed detail API for new integrations.
The inherited `GetAsync(id)` remains available with its original
`Task<Dictionary<string, object?>>` return type for source and assembly
compatibility; both calls are customer-scoped.

Every customer-scoped get, acknowledge, lifecycle, UBL, and evidence request
appends the URL-encoded `customerRef` query. The server verifies the document
against the approved firm mapped to that integrator-owned reference.

The integrator owns the stable `CustomerRef` after ePošťák has approved and
Peppol-registered the firm. The SDK cannot create or discover firms. It injects `CustomerRef`, sends
immediately by default, and applies the backend ECMAScript `TrimString` set
(`0009-000D`, `0020`, `00A0`, `1680`, `2000-200A`, `2028-2029`, `202F`,
`205F`, `3000`, `FEFF`) to `CustomerRef` and `ExternalId`; `U+0085` is
preserved. It then derives `connector:v1:` plus lowercase SHA-256 over both
UTF-8 values, each prefixed by its four-byte big-endian byte length. Generated
keys are 77 ASCII characters; explicit keys must be 1-255 UTF-8 bytes and are
otherwise unchanged.
For approval queues, call `Documents.StageAsync(request)` and then either
`SendDocumentAsync(staged.Id)` or `CancelDocumentAsync(staged.Id)`.

Managed Connector credentials support customer documents/events, one global
webhook, audited document evidence, and customer-scoped Mapper preview. Legacy
helpers and `Enterprise.Connector` remain silently supported for source
compatibility.

```csharp
var polled = await customer.Events.ListAsync(new ConnectorListParams { Limit = 50 });
var configuredWebhook = await connectorClient.Connector.Webhook.ConfigureAsync(
    "https://erp.example.com/webhooks/epostak",
    ["document.received", "document.delivered"]);
if (configuredWebhook.Secret is not null)
{
    // Persist this value in your server-side secret manager now. It is returned only once.
    var oneTimeWebhookSecret = configuredWebhook.Secret;
}
await connectorClient.Connector.Webhook.TestAsync("erp-acme");
```

Push uses the same canonical event item as polling with `CustomerRef` at the
root. `WebhookSignature.Verify(...)` verifies HMAC-SHA256 over
`timestamp + "." + rawBody`.

### Acknowledge locally or respond to the supplier

```csharp
var receivedDocumentId = "document-id-from-list-or-event";
await customer.Documents.AcknowledgeAsync(receivedDocumentId, $"erp:{receivedDocumentId}");
var response = await customer.Documents.RespondAsync(
    receivedDocumentId,
    new ConnectorInvoiceResponseRequest
    {
        Status = "accepted",
        Note = "Imported and accepted",
    });

// Direct alternative (do not call both); CustomerRef is still mandatory:
// await connectorClient.Connector.Documents.RespondAsync(
//     receivedDocumentId, "erp-acme",
//     new ConnectorInvoiceResponseRequest { Status = "accepted" });
```

`AcknowledgeAsync` only records that the inbound document was processed
locally; it does not notify the supplier. `RespondAsync` sends the network
business response via
`POST /connector/documents/{documentId}/respond?customerRef=...`. Use only the
business statuses `received`, `in_process`, `under_query`,
`conditionally_accepted`, `rejected`, `accepted`, or `paid`, with an optional
`Note`. The Connector handles Peppol response codes and XML; do not send either.
The result reports `Response.Delivery` as `sent` or `queued` and marks safe
replays with `Idempotent`.

## Enterprise API facade flow

Use this as the default low-friction path for ERP integrations that do not want
to receive webhooks on day one:

```csharp
await client.Enterprise.Payloads.ValidateAsync(request);

await client.Enterprise.Documents.PreflightAsync(new PreflightRequest
{
    ReceiverPeppolId = request.ReceiverPeppolId
});

var sent = await client.Enterprise.Documents.SendAsync(
    request,
    idempotencyKey: "erp-fa-2026-001");

var events = await client.Enterprise.Events.PullAsync(new WebhookQueueParams { Limit = 50 });
await client.Enterprise.Events.BatchAckAsync(events.Items.Select(e => e.EventId));

var supportPacket = await client.Enterprise.Documents.SupportPacketAsync(sent.DocumentId);
```

Deprecated SDK resource and method names remain available as
source-compatibility adapters. They already call the canonical `Payloads`,
`Events`, and `SupportPacketAsync` routes; they do not call the retired URLs.

The nine unused pre-launch alias URLs were removed on 20 July 2026. Raw HTTP
clients must use `/payloads/*`, `/events/*`, and
`/documents/{id}/support-packet`. Existing SDK calls through deprecated names
keep working because those adapters already delegate to the canonical routes.

## Recent changes

### Unreleased — 2026-07-19

- Enterprise 1.6.0 JSON sends support `ProcessId`, JSON self-billing and credit
  notes through `DocumentType`, `Supplier*`, and `PrecedingInvoiceRef`.
- Send responses include replay/link metadata. Document events now deserialize
  the live `process_id` and nested `pagination` object.

### Included in v1.1.0 — 2026-07-14

- Connector is canonical at `client.Connector`; the Enterprise namespace alias
  is supported compatibility only.
- Connector credentials are approved by ePošťák; the integrator chooses each
  stable `CustomerRef` in the dashboard after firm approval. Enterprise keys
  and OAuth do not grant Connector access.
- Customer document creation snapshots the request, validates explicit
  1-255-byte idempotency keys, and retries network errors, `429`, and all `5xx`
  only when safe. Lifecycle calls are server-idempotent; `409` is never retried.
- `EPostakException` exposes `Field`, `NextAction`, `Retryable`, `RequestId`,
  and `RetryAfter`.
- Identity hashing uses the exact backend ECMAScript `TrimString` code points;
  `U+0085` is preserved.
- `document.cancelled` is a first-class business event.

### Included in v1.1.0 — 2026-07-12

- Connector now leads with
  `client.Connector.Customers.For(customerRef).Documents/Events`. Lower-level
  workflows live under `client.Connector.Advanced`; existing top-level methods
  remain supported compatibility aliases. There is no customer creation API in
  the SDK.
- JSON billing payloads now expose the live receiver address, `PrepaidAmount`,
  `Prepayments`, and advanced line-item VAT/classification fields from the
  Enterprise OpenAPI.

### Included in v1.1.0 — 2026-07-01

- `client.Enterprise.Payloads.ValidateAsync(...)`,
  `client.Enterprise.Events.PullAsync(...)`, and
  `client.Enterprise.Documents.SupportPacketAsync(...)` cover the Enterprise
  API facade flow for validation, event pull/ack, and support packets.

### Included in v1.1.0 — 2026-06-30

- Customer-scoped
  `client.Connector.Customers.For(customerRef).Advanced.MapperAsync(...)` is
  the managed, preview-only Mapper flow. Top-level
  `client.Connector.Advanced.MapperAsync(...)` remains a supported
  compatibility alias and is unavailable with managed Connector credentials.
- `client.Box` / `client.Enterprise.Box` covers ePošťák Box list, create with
  `BoxCreateRequest.PayloadXml`, detail, schedule, send-now, retry, and cancel
  over `/box/items`.

### v1.0.0 — 2026-06-14

- `client.Enterprise` is the documented namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces.
- `client.Sapi.Participants.For(participantId).Documents` requires participant
  scoping before SAPI document send/receive/get/acknowledge.
- `client.Connector.Customers.For(customerRef)` injects
  `CustomerRef` and keeps `X-Firm-Id` off customer-managed Connector calls.
- The Connector compatibility surface includes preflight, Zen input, Autopilot,
  reconcile, mailbox, sync, document evidence, and event polling.
- Managed Connector calls use approved Connector credentials and integrator-
  chosen `CustomerRef` values and omit `X-Firm-Id`. Enterprise integrator
  credentials and OAuth do not grant Connector access; compatibility helpers
  are not available with Connector credentials.
- Docs: added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence.
- `client.Enterprise.Documents.StatusBatchAsync(ids)` covers `POST /documents/status/batch` for up to 100 document IDs.
- `client.Enterprise.Reporting.SubmissionsAsync(...)` covers `GET /reporting/submissions`.
- `client.Enterprise.Integrator.Keys.ListAsync()` and `DeactivateAsync(...)` cover the production `GET`/`DELETE /integrator/keys` surface.
- README environment data now lists production (`https://epostak.sk`) and test (`https://dev.epostak.sk`) Enterprise, SAPI, and OAuth origins.

### v0.10.1 — 2026-05-22

- `OAuth.ExchangeCodeAsync(...)` now returns `OAuthTokenResponse`, matching `POST /api/oauth/token`: the issued `sk_int_*` `client_id`/`client_secret` plus consented firm metadata. Use those credentials with `client.Enterprise.Auth.TokenAsync(...)` to mint short-lived JWTs for `/api/v1/*` calls.

### v0.10.0 — 2026-05-18

- `client.Sapi.Participants.For(id).Documents` covers SAPI-SK 1.0 document send, receive list/detail, and acknowledge.
- `client.Enterprise.Webhooks.TestAsync(id, new WebhookTestParams { Count = 250, Mode = "queued" })` supports direct and queued webhook tests.
- `client.Enterprise.Webhooks.DeadLettersAsync()`, `ReplayDeadLetterAsync(id)`, and `ResolveDeadLetterAsync(id, reason)` cover webhook DLQ operations.
- `client.Enterprise.Peppol.ResolveAsync(...)` resolves ERP identifiers to Peppol participant + routing capability.
- Added evidence bundle, outbound MDN, company search, Peppol document listing, and license info helpers.

## Quick start

```csharp
using EPostak;
using EPostak.Models;

var client = new EPostakClient(new EPostakConfig
{
    ClientId = "sk_live_xxxxx",
    ClientSecret = "sk_live_xxxxx"
});

// Send an invoice
var result = await client.Enterprise.Documents.SendAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    ReceiverName = "Firma s.r.o.",
    InvoiceNumber = "INV-2026-001",
    IssueDate = "2026-04-11",
    DueDate = "2026-05-11",
    Currency = "EUR",
    Items =
    [
        new LineItem
        {
            Description = "Consulting services",
            Quantity = 10,
            Unit = "HUR",
            UnitPrice = 150.00m,
            VatRate = 23
        }
    ]
});

Console.WriteLine($"Sent! Document ID: {result.DocumentId}");
```

## Configuration

```csharp
var client = new EPostakClient(new EPostakConfig
{
    // Required: OAuth client ID (your API key)
    ClientId = "sk_live_xxxxx",
    // Required: OAuth client secret
    ClientSecret = "sk_live_xxxxx",

    // Optional: override base URL for the test environment; omit for production
    BaseUrl = "https://dev.epostak.sk/api/v1",

    // Optional: firm ID for Enterprise integrator credentials only
    FirmId = "firm-uuid-here"
});
```

Production is the SDK default: Enterprise `https://epostak.sk/api/v1`, SAPI
`https://epostak.sk/sapi/v1`, OAuth origin `https://epostak.sk`. For test
calls, set `BaseUrl` to `https://dev.epostak.sk/api/v1`; SAPI derives
`https://dev.epostak.sk/sapi/v1`, and OAuth helpers need
`origin: "https://dev.epostak.sk"` because OAuth is outside `/api/v1`.

### Enterprise Integrator (multi-tenant) usage

This section is only for Enterprise integrator credentials and firm-scoped
Enterprise calls. It does not provision Connector. Scope requests to a
specific firm:

```csharp
var client = new EPostakClient(new EPostakConfig { ClientId = "sk_int_xxxxx", ClientSecret = "sk_int_xxxxx" });

// Create a firm-scoped client (shares the underlying HttpClient)
var firmClient = client.WithFirm("firm-uuid-here");
var inbox = await firmClient.Enterprise.Documents.Inbox.ListAsync();
```

### Shared HttpClient

For best performance in server applications, share an `HttpClient`:

```csharp
var httpClient = new HttpClient();
var client = new EPostakClient(new EPostakConfig { ClientId = "sk_live_xxxxx", ClientSecret = "sk_live_xxxxx" }, httpClient);
```

## Resources

### Connector

The primary Connector surface is customer-scoped and uses business data.
ePošťák approves and Peppol-registers the firm; the integrator then chooses its
stable `CustomerRef` in the dashboard. The SDK does not expose signup or
customer creation.

```csharp
var connectorClient = new EPostakClient(new EPostakConfig
{
    ClientId = Environment.GetEnvironmentVariable("EPOSTAK_CONNECTOR_CLIENT_ID")!,
    ClientSecret = Environment.GetEnvironmentVariable("EPOSTAK_CONNECTOR_CLIENT_SECRET")!,
});
var customer = connectorClient.Connector.Customers.For("erp-customer-1");

var sent = await customer.Documents.SendAsync(new ConnectorBusinessDocumentRequest
{
    ExternalId = "FA-2026-001",
    Number = "FA-2026-001",
    Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
    Lines = [new ConnectorBusinessLine { Description = "Služby", Quantity = 1, UnitPrice = 100, VatRate = 23 }],
});
var detail = await customer.Documents.GetBusinessDocumentAsync(sent.Id);

var inbox = await customer.Documents.ListAsync(new ConnectorBusinessDocumentListParams
{
    Direction = "inbound",
    Limit = 20,
});
foreach (var document in inbox.Documents)
{
    await customer.Documents.AcknowledgeAsync(document.Id, $"erp-import-{document.Id}");
}

var events = await customer.Events.ListAsync(new ConnectorListParams { Limit = 50 });
```

For a stage-first workflow, the lifecycle transitions are also customer-scoped
and send no request body:

```csharp
var staged = await customer.Documents.StageAsync(request);
var sentLater = await customer.Documents.SendDocumentAsync(staged.Id);
// Or, before delivery: await customer.Documents.CancelDocumentAsync(staged.Id);
```

Mapper is a customer-scoped preview/normalization helper; it never stages or
sends a document:

```csharp
var preview = await customer.Advanced.MapperAsync(new Dictionary<string, object?>
{
    ["templateKey"] = "pohoda-csv-v1",
    ["sourceType"] = "csv",
    ["sourceText"] = csv,
});
```

Legacy preflight, raw send/status, inbox, outbox, Autopilot, Zen, reconcile,
mailbox, sync, and action helpers remain supported source-compatibility aliases
but are not callable with managed Connector credentials.

#### Connector errors and retries

```csharp
try
{
    await customer.Documents.SendAsync(request);
}
catch (EPostakException error)
{
    Console.Error.WriteLine($"{error.Code} {error.Message} {error.Field} {error.NextAction}");
    Console.Error.WriteLine($"{error.Retryable} {error.RequestId} {error.RetryAfter}");
}
```

Keyed document creation retries network failures, `429`, and every `5xx` while
reusing the exact body and key. Lifecycle send/cancel/acknowledge calls are
server-idempotent and use the same policy. `Retry-After` is honored; `409` is
always surfaced once and never retried automatically.

### SAPI-SK participant flow

```csharp
var sapiDocument = new Dictionary<string, object?>
{
    ["metadata"] = new Dictionary<string, object?>
    {
        ["documentId"] = "FA-2026-001",
        ["documentTypeId"] = "invoice",
        ["processId"] = "billing",
        ["senderParticipantId"] = "0245:1234567890",
        ["receiverParticipantId"] = "0245:0987654321",
        ["creationDateTime"] = "2026-06-14T10:00:00Z"
    },
    ["payload"] = "<Invoice/>",
    ["payloadFormat"] = "XML"
};

await client.Sapi.Participants.For("0245:1234567890")
    .Documents
    .SendAsync(sapiDocument, "sapi-fa-2026-001");
```

### Documents

```csharp
// Get a document
var doc = await client.Enterprise.Documents.GetAsync("doc-id");

// Update a draft
var updated = await client.Enterprise.Documents.UpdateAsync("doc-id", new UpdateDocumentRequest
{
    InvoiceNumber = "INV-2026-002"
});

// Send a document
var sent = await client.Enterprise.Documents.SendAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    ReceiverName = "Firma s.r.o.",
    Items = [ new LineItem { Description = "Item", Quantity = 1, UnitPrice = 100, VatRate = 23 } ]
});

// Send raw UBL XML
var sentXml = await client.Enterprise.Documents.SendAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    Xml = "<Invoice xmlns='urn:oasis:names:specification:ubl:schema:xsd:Invoice-2'>...</Invoice>"
});

// Get delivery status
var status = await client.Enterprise.Documents.StatusAsync("doc-id");

// Batch status check for up to 100 documents
var batch = await client.Enterprise.Documents.StatusBatchAsync(["doc-1", "doc-2"]);

// Get delivery evidence (AS4 receipt, MLR, invoice response)
var evidence = await client.Enterprise.Documents.EvidenceAsync("doc-id");

// Download PDF
byte[] pdf = await client.Enterprise.Documents.PdfAsync("doc-id");
File.WriteAllBytes("invoice.pdf", pdf);

// Download UBL XML
string ubl = await client.Enterprise.Documents.UblAsync("doc-id");

// Respond to a received document
var response = await client.Enterprise.Documents.RespondAsync("doc-id", new InvoiceRespondRequest
{
    Status = InvoiceResponseCode.AP,
    Note = "Invoice accepted"
});

// Validate without sending
var validation = await client.Enterprise.Documents.ValidateAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    ReceiverName = "Firma s.r.o.",
    Items = [ new LineItem { Description = "Item", Quantity = 1, UnitPrice = 100, VatRate = 23 } ]
});

// Preflight check (can receiver accept this document type?)
var preflight = await client.Enterprise.Documents.PreflightAsync(new PreflightRequest
{
    ReceiverPeppolId = "0245:12345678",
    UblDocument = "<Invoice>...</Invoice>"
});
if (!preflight.CanSend)
    Console.WriteLine($"Preflight decision: {preflight.Decision}");

// Convert between JSON and UBL
var converted = await client.Enterprise.Documents.ConvertAsync(new ConvertRequest
{
    InputFormat = ConvertInputFormat.Ubl,
    OutputFormat = ConvertOutputFormat.Json,
    Document = "<Invoice>...</Invoice>"
});
```

### Inbox

```csharp
// List inbox documents
var inbox = await client.Enterprise.Documents.Inbox.ListAsync(new InboxListParams
{
    Limit = 20,
    Status = InboxStatus.RECEIVED
});

// Get a single inbox document with UBL payload
var detail = await client.Enterprise.Documents.Inbox.GetAsync("doc-id");
Console.WriteLine(detail.Payload); // UBL XML

// Acknowledge receipt
var ack = await client.Enterprise.Documents.Inbox.AcknowledgeAsync("doc-id");

// List across all firms (integrator only)
var all = await client.Enterprise.Documents.Inbox.ListAllAsync(new InboxAllParams
{
    Limit = 50,
    FirmId = "specific-firm-id" // optional filter
});
```

### Peppol

```csharp
// SMP lookup
var participant = await client.Enterprise.Peppol.LookupAsync("0245", "12345678");
if (participant.Accepts && participant.RoutingStatus == "ready")
{
    // Receiver is registered and routable for the default BIS Billing invoice.
}

var caps = await client.Enterprise.Peppol.CapabilitiesAsync(new CapabilitiesRequest
{
    Scheme = "0245",
    Identifier = "12345678",
    DocumentType = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."
});

// Directory search
var results = await client.Enterprise.Peppol.Directory.SearchAsync(new DirectorySearchParams
{
    Q = "Bratislava",
    Country = "SK",
    PageSize = 10
});

// Company lookup by ICO
var company = await client.Enterprise.Peppol.CompanyLookupAsync("12345678");
Console.WriteLine($"{company.Name}, DIC: {company.Dic}");

var matches = await client.Enterprise.Peppol.CompanySearchAsync("Demo", limit: 10);

var resolved = await client.Enterprise.Peppol.ResolveErpInfoAsync(new Dictionary<string, string?>
{
    ["ico"] = "12345678",
    ["documentTypeId"] = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."
});
if (resolved.Capability?.NetworkReady == true)
    Console.WriteLine($"Ready: {resolved.Participant?.PeppolId}");
```

### Firms

```csharp
// List firms
var firms = await client.Enterprise.Firms.ListAsync();

// Get firm details
var firm = await client.Enterprise.Firms.GetAsync("firm-id");

// List firm documents
var docs = await client.Enterprise.Firms.DocumentsAsync("firm-id", new FirmDocumentsParams
{
    Limit = 20,
    Direction = DocumentDirection.Inbound
});

// Register Peppol ID
var peppolId = await client.Enterprise.Firms.RegisterPeppolIdAsync("firm-id", "0245", "12345678");

// Assign firm by ICO (integrator)
var assigned = await client.Enterprise.Firms.AssignAsync("12345678");

// Batch assign (integrator, max 50)
var batch = await client.Enterprise.Firms.AssignBatchAsync(["12345678", "87654321"]);
```

### Webhooks

```csharp
// Create a webhook
var webhook = await client.Enterprise.Webhooks.CreateAsync(new CreateWebhookRequest
{
    Url = "https://example.com/webhook",
    Events = [WebhookEvents.DocumentReceived, WebhookEvents.DocumentSent]
});
Console.WriteLine($"Secret: {webhook.Secret}"); // save this for signature verification

// List webhooks
var webhooks = await client.Enterprise.Webhooks.ListAsync();

// Get webhook with deliveries
var detail = await client.Enterprise.Webhooks.GetAsync("webhook-id");

// Update webhook
var updated = await client.Enterprise.Webhooks.UpdateAsync("webhook-id", new UpdateWebhookRequest
{
    IsActive = false
});

// Delete webhook
await client.Enterprise.Webhooks.DeleteAsync("webhook-id");

var queued = await client.Enterprise.Webhooks.TestAsync("webhook-id", new WebhookTestParams
{
    Event = WebhookEvent.DocumentReceived,
    Count = 250,
    Mode = "queued"
});

var deliveries = await client.Enterprise.Webhooks.DeliveriesAsync("webhook-id", new WebhookDeliveriesParams
{
    TestRunId = queued.TestRunId,
    IncludeResponseBody = true
});

var dlq = await client.Enterprise.Webhooks.DeadLettersAsync(includeResponseBody: true);
await client.Enterprise.Webhooks.ReplayDeadLetterAsync("delivery-id");
await client.Enterprise.Webhooks.ResolveDeadLetterAsync("delivery-id", "Handled in ERP");
```

#### Dedup + retry headers (server v1.1 — 2026-05-12)

Three new headers on every push delivery:

| Header | Value | Use |
|-|-|-|
| `X-Webhook-Event-Id` | UUID, stable across retries | **Primary dedup key.** Body also carries it as `webhook_event_id`. |
| `X-Webhook-Attempt` | 1-based attempt number | Telemetry / logging. |
| `X-Webhook-Max-Attempts` | Total attempts in the retry window (10) | Telemetry / logging. |

Recommended receiver pattern (ASP.NET Core minimal API):

```csharp
// INSERT ON CONFLICT DO NOTHING on the event id is enough — every retry
// of the same logical event carries the SAME X-Webhook-Event-Id.
app.MapPost("/webhooks/epostak", async (HttpRequest req, NpgsqlConnection db) =>
{
    var eventId = req.Headers["X-Webhook-Event-Id"].ToString();
    var inserted = await db.ExecuteAsync(
        @"INSERT INTO processed_webhooks (event_id) VALUES (@id)
          ON CONFLICT (event_id) DO NOTHING",
        new { id = eventId });
    if (inserted == 0)
        return Results.Ok(); // duplicate — ack and skip
    // process event for the first time...
    return Results.NoContent();
});
```

**Retry policy (server-side, as of 2026-05-12):** retries fire only for `408`, `425`, `429`, `502`, `503`, `504` and network errors (~44h bounded backoff). Returning any other 4xx/5xx — including `500` — terminates the retry loop immediately. If your handler wants a retry on a transient failure, return `503` (not `500`).

The signature contract is **unchanged** (HMAC-SHA256 over `${timestamp}.${body}`).

### Events pull facade

```csharp
// Pull events
var events = await client.Enterprise.Events.PullAsync(new WebhookQueueParams
{
    Limit = 50,
    EventType = WebhookEvents.DocumentReceived
});

foreach (var item in events.Items)
{
    Console.WriteLine($"Event: {item.Event}, ID: {item.EventId}");
    // Process the event...

    // Acknowledge it
    await client.Enterprise.Events.AckAsync(item.EventId);
}

// Batch acknowledge
await client.Enterprise.Events.BatchAckAsync(events.Items.Select(e => e.EventId));

// Legacy cross-firm queue remains available for integrator drain jobs.
var allEvents = await client.Enterprise.Webhooks.Queue.PullAllAsync(new WebhookQueueAllParams
{
    Limit = 100,
    Since = "2026-04-01T00:00:00Z"
});

// Batch ack all (integrator)
var result = await client.Enterprise.Webhooks.Queue.BatchAckAllAsync(
    allEvents.Items.Select(e => e.EventId));
Console.WriteLine($"Acknowledged: {result.Acknowledged}");
```

### Reporting

```csharp
var stats = await client.Enterprise.Reporting.StatisticsAsync(new StatisticsParams
{
    Period = ReportingPeriod.Month
});

Console.WriteLine($"Sent: {stats.Sent.Total} total");
Console.WriteLine($"Received: {stats.Received.Total} total");
Console.WriteLine($"Delivery rate: {stats.DeliveryRate:P1}");
foreach (var top in stats.TopRecipients)
    Console.WriteLine($"  {top.Name} ({top.PeppolId}): {top.Count}");

var submissions = await client.Enterprise.Reporting.SubmissionsAsync(
    new ReportingSubmissionsParams { Limit = 20, ReportType = "EUSR" });
```

### Account

```csharp
var account = await client.Enterprise.Account.GetAsync();
Console.WriteLine($"Firm: {account.Firm.Name}");
Console.WriteLine($"Plan: {account.Plan.Name} ({account.Plan.Status})");
Console.WriteLine($"Usage: {account.Usage.Outbound} outbound, {account.Usage.Inbound} inbound");
```

### Integrator keys

```csharp
var keys = await client.Enterprise.Integrator.Keys.ListAsync();
await client.Enterprise.Integrator.Keys.DeactivateAsync(clientId: "sk_int_xxxxx...abcd");
var usage = await client.Enterprise.Integrator.Licenses.InfoAsync(limit: 100);
```

### Payload Assistant OCR

```csharp
// Extract from a single file
using var stream = File.OpenRead("invoice.pdf");
var extracted = await client.Enterprise.Payloads.ExtractAsync(stream, "application/pdf", "invoice.pdf");
Console.WriteLine($"Confidence: {extracted.Confidence}");
Console.WriteLine($"UBL: {extracted.UblXml}");
// Outbound invoices can include the response field send_payload for review + validate + send.

// Batch extraction
var batchResult = await client.Enterprise.Payloads.ExtractBatchAsync(
[
    new ExtractFile { Stream = File.OpenRead("inv1.pdf"), MimeType = "application/pdf", FileName = "inv1.pdf" },
    new ExtractFile { Stream = File.OpenRead("inv2.png"), MimeType = "image/png", FileName = "inv2.png" }
]);

Console.WriteLine($"Batch: {batchResult.Successful}/{batchResult.Total} successful");
```

`client.Enterprise.Extract` remains available for compatibility; new
integrations should use `client.Enterprise.Payloads.ExtractAsync`.

## Error handling

All API errors throw `EPostakException`:

```csharp
try
{
    await client.Enterprise.Documents.GetAsync("nonexistent");
}
catch (EPostakException ex)
{
    Console.WriteLine($"Status: {ex.Status}");   // HTTP status code (e.g. 404)
    Console.WriteLine($"Message: {ex.Message}");  // Human-readable message
    Console.WriteLine($"Code: {ex.Code}");        // Machine-readable code (e.g. "NOT_FOUND")
    Console.WriteLine($"Details: {ex.Details}");   // Additional details (validation errors, etc.)
}
```

Network errors (DNS failure, timeout) also throw `EPostakException` with `Status = 0`.

## CancellationToken support

All async methods accept an optional `CancellationToken`:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var doc = await client.Enterprise.Documents.GetAsync("doc-id", cts.Token);
```

## Connector webhook debugger

Inspect the exact signed body and attempt timeline, then use idempotent replay
after fixing the receiver. `RunTestSuiteAsync` exercises all receiver scenarios.

```csharp
var failed = await connectorClient.Connector.Webhook.ListDeliveriesAsync(status: "FAILED");
var detail = await connectorClient.Connector.Webhook.GetDeliveryAsync(failed.Deliveries[0].Id);
await connectorClient.Connector.Webhook.ReplayDeliveryAsync(detail.Delivery.Id, "erp:replay:1");
await connectorClient.Connector.Webhook.RunTestSuiteAsync("erp-acme", "erp:suite:1");
```

## License

MIT
