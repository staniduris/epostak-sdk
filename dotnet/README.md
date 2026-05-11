# ePostak .NET SDK

Official .NET SDK for the [ePostak](https://epostak.sk) Enterprise API. Send and receive Peppol e-invoices in Slovakia.

## Installation

```shell
dotnet add package EPostak
```

Or add to your `.csproj`:

```xml
<PackageReference Include="EPostak" Version="0.9.0" />
```

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
var result = await client.Documents.SendAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
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

    // Optional: override base URL for staging/local testing
    BaseUrl = "https://epostak.sk/api/v1",

    // Optional: firm ID for integrator keys (sk_int_*)
    FirmId = "firm-uuid-here"
});
```

### Integrator (multi-tenant) usage

If you use an integrator key (`sk_int_*`), scope requests to a specific firm:

```csharp
var client = new EPostakClient(new EPostakConfig { ClientId = "sk_int_xxxxx", ClientSecret = "sk_int_xxxxx" });

// Create a firm-scoped client (shares the underlying HttpClient)
var firmClient = client.WithFirm("firm-uuid-here");
var inbox = await firmClient.Documents.Inbox.ListAsync();
```

### Shared HttpClient

For best performance in server applications, share an `HttpClient`:

```csharp
var httpClient = new HttpClient();
var client = new EPostakClient(new EPostakConfig { ClientId = "sk_live_xxxxx", ClientSecret = "sk_live_xxxxx" }, httpClient);
```

## Resources

### Documents

```csharp
// Get a document
var doc = await client.Documents.GetAsync("doc-id");

// Update a draft
var updated = await client.Documents.UpdateAsync("doc-id", new UpdateDocumentRequest
{
    InvoiceNumber = "INV-2026-002"
});

// Send a document
var sent = await client.Documents.SendAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    Items = [ new LineItem { Description = "Item", Quantity = 1, UnitPrice = 100, VatRate = 23 } ]
});

// Send raw UBL XML
var sentXml = await client.Documents.SendAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    Xml = "<Invoice xmlns='urn:oasis:names:specification:ubl:schema:xsd:Invoice-2'>...</Invoice>"
});

// Get delivery status
var status = await client.Documents.StatusAsync("doc-id");

// Get delivery evidence (AS4 receipt, MLR, invoice response)
var evidence = await client.Documents.EvidenceAsync("doc-id");

// Download PDF
byte[] pdf = await client.Documents.PdfAsync("doc-id");
File.WriteAllBytes("invoice.pdf", pdf);

// Download UBL XML
string ubl = await client.Documents.UblAsync("doc-id");

// Respond to a received document
var response = await client.Documents.RespondAsync("doc-id", new InvoiceRespondRequest
{
    Status = InvoiceResponseCode.AP,
    Note = "Invoice accepted"
});

// Validate without sending
var validation = await client.Documents.ValidateAsync(new SendDocumentRequest
{
    ReceiverPeppolId = "0245:12345678",
    Items = [ new LineItem { Description = "Item", Quantity = 1, UnitPrice = 100, VatRate = 23 } ]
});

// Preflight check (can receiver accept this document type?)
var preflight = await client.Documents.PreflightAsync(new PreflightRequest
{
    ReceiverPeppolId = "0245:12345678"
});

// Convert between JSON and UBL
var converted = await client.Documents.ConvertAsync(new ConvertRequest
{
    InputFormat = ConvertInputFormat.Ubl,
    OutputFormat = ConvertOutputFormat.Json,
    Document = "<Invoice>...</Invoice>"
});
```

### Inbox

```csharp
// List inbox documents
var inbox = await client.Documents.Inbox.ListAsync(new InboxListParams
{
    Limit = 20,
    Status = InboxStatus.RECEIVED
});

// Get a single inbox document with UBL payload
var detail = await client.Documents.Inbox.GetAsync("doc-id");
Console.WriteLine(detail.Payload); // UBL XML

// Acknowledge receipt
var ack = await client.Documents.Inbox.AcknowledgeAsync("doc-id");

// List across all firms (integrator only)
var all = await client.Documents.Inbox.ListAllAsync(new InboxAllParams
{
    Limit = 50,
    FirmId = "specific-firm-id" // optional filter
});
```

### Peppol

```csharp
// SMP lookup
var participant = await client.Peppol.LookupAsync("0245", "12345678");
Console.WriteLine($"Name: {participant.Name}, Capabilities: {participant.Capabilities.Count}");

// Directory search
var results = await client.Peppol.Directory.SearchAsync(new DirectorySearchParams
{
    Q = "Bratislava",
    Country = "SK",
    PageSize = 10
});

// Company lookup by ICO
var company = await client.Peppol.CompanyLookupAsync("12345678");
Console.WriteLine($"{company.Name}, DIC: {company.Dic}");
```

### Firms

```csharp
// List firms
var firms = await client.Firms.ListAsync();

// Get firm details
var firm = await client.Firms.GetAsync("firm-id");

// List firm documents
var docs = await client.Firms.DocumentsAsync("firm-id", new FirmDocumentsParams
{
    Limit = 20,
    Direction = DocumentDirection.Inbound
});

// Register Peppol ID
var peppolId = await client.Firms.RegisterPeppolIdAsync("firm-id", "0245", "12345678");

// Assign firm by ICO (integrator)
var assigned = await client.Firms.AssignAsync("12345678");

// Batch assign (integrator, max 50)
var batch = await client.Firms.AssignBatchAsync(["12345678", "87654321"]);
```

### Webhooks

```csharp
// Create a webhook
var webhook = await client.Webhooks.CreateAsync(new CreateWebhookRequest
{
    Url = "https://example.com/webhook",
    Events = [WebhookEvents.DocumentReceived, WebhookEvents.DocumentSent]
});
Console.WriteLine($"Secret: {webhook.Secret}"); // save this for signature verification

// List webhooks
var webhooks = await client.Webhooks.ListAsync();

// Get webhook with deliveries
var detail = await client.Webhooks.GetAsync("webhook-id");

// Update webhook
var updated = await client.Webhooks.UpdateAsync("webhook-id", new UpdateWebhookRequest
{
    IsActive = false
});

// Delete webhook
await client.Webhooks.DeleteAsync("webhook-id");
```

### Webhook Queue (polling)

```csharp
// Pull events
var events = await client.Webhooks.Queue.PullAsync(new WebhookQueueParams
{
    Limit = 50,
    EventType = WebhookEvents.DocumentReceived
});

foreach (var item in events.Items)
{
    Console.WriteLine($"Event: {item.Type}, ID: {item.Id}");
    // Process the event...

    // Acknowledge it
    await client.Webhooks.Queue.AckAsync(item.Id);
}

// Batch acknowledge
await client.Webhooks.Queue.BatchAckAsync(events.Items.Select(e => e.Id));

// Pull across all firms (integrator)
var allEvents = await client.Webhooks.Queue.PullAllAsync(new WebhookQueueAllParams
{
    Limit = 100,
    Since = "2026-04-01T00:00:00Z"
});

// Batch ack all (integrator)
var result = await client.Webhooks.Queue.BatchAckAllAsync(
    allEvents.Events.Select(e => e.EventId));
Console.WriteLine($"Acknowledged: {result.Acknowledged}");
```

### Reporting

```csharp
var stats = await client.Reporting.StatisticsAsync(new StatisticsParams
{
    Period = ReportingPeriod.Month
});

Console.WriteLine($"Sent: {stats.Sent.Total} total");
Console.WriteLine($"Received: {stats.Received.Total} total");
Console.WriteLine($"Delivery rate: {stats.DeliveryRate:P1}");
foreach (var top in stats.TopRecipients)
    Console.WriteLine($"  {top.Name} ({top.PeppolId}): {top.Count}");
```

### Account

```csharp
var account = await client.Account.GetAsync();
Console.WriteLine($"Firm: {account.Firm.Name}");
Console.WriteLine($"Plan: {account.Plan.Name} ({account.Plan.Status})");
Console.WriteLine($"Usage: {account.Usage.Outbound} outbound, {account.Usage.Inbound} inbound");
```

### Extract (OCR)

```csharp
// Extract from a single file
using var stream = File.OpenRead("invoice.pdf");
var extracted = await client.Extract.SingleAsync(stream, "application/pdf", "invoice.pdf");
Console.WriteLine($"Confidence: {extracted.Confidence}");
Console.WriteLine($"UBL: {extracted.UblXml}");

// Batch extraction
var batchResult = await client.Extract.BatchAsync(
[
    new ExtractFile { Stream = File.OpenRead("inv1.pdf"), MimeType = "application/pdf", FileName = "inv1.pdf" },
    new ExtractFile { Stream = File.OpenRead("inv2.png"), MimeType = "image/png", FileName = "inv2.png" }
]);

Console.WriteLine($"Batch: {batchResult.Successful}/{batchResult.Total} successful");
```

## Recent changes

**v0.9.0** — Pull API resources (`client.Inbound`, `client.Outbound`), `UblValidationException` with `UblRule` constants, typed `WebhookTestParams` + `WebhookEvent` enum, `WebhookDelivery.IdempotencyKey`, `client.LastRateLimit`.

## Error handling

All API errors throw `EPostakException`:

```csharp
try
{
    await client.Documents.GetAsync("nonexistent");
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
var doc = await client.Documents.GetAsync("doc-id", cts.Token);
```

## License

MIT
