# Migration Guide: Workflow-First SDK Release

This guide covers the breaking public API naming change for TypeScript `4.0.0`
and Python, PHP, Ruby, Java, and .NET `1.0.0`.

## What Changed

Enterprise `/api/v1/*` calls are documented under `enterprise`. SAPI-SK
`/sapi/v1/*` document calls are separate and require participant scoping before
send, receive, get, or acknowledge.

| Old public shape | New public shape |
|-|-|
| `client.documents.send(...)` | `client.enterprise.documents.send(...)` |
| `client.documents.inbox.list(...)` | `client.enterprise.documents.inbox.list(...)` |
| `client.inbound.list(...)` | `client.enterprise.pull.inbound.list(...)` |
| `client.outbound.events(...)` | `client.enterprise.pull.outbound.events(...)` |
| `client.connector.autopilot({ customerRef, ... })` | `client.enterprise.connector.customers.for(customerRef).submitDocument(...)` |
| `client.sapi.send(body, { participantId })` | `client.sapi.participants.for(participantId).documents.send(body)` |

The old top-level resource objects may still exist as adapters while the SDKs
settle, but new code should use the workflow-first namespace.

## Cross-Cutting Rules

- OAuth/JWT minting still uses the SAPI auth endpoints internally.
- Enterprise `firmId` only affects firm-scoped Enterprise calls.
- Connector customer-scoped calls inject `customerRef` and omit `X-Firm-Id`.
- SAPI participant-scoped calls always send `X-Peppol-Participant-Id`.
- Idempotency stays in `Idempotency-Key`.

## TypeScript

```typescript
const result = await client.enterprise.documents.send(invoice);

const run = await client.enterprise.connector.customers
  .for("erp-customer-1")
  .submitDocument({
    externalId: "FA-2026-001",
    idempotencyKey: "erp-fa-2026-001",
    payload: invoice,
  });

await client.sapi.participants
  .for("0245:1234567890")
  .documents.send(sapiDocument, { idempotencyKey: "sapi-fa-2026-001" });
```

## Python

```python
result = client.enterprise.documents.send(invoice)

run = client.enterprise.connector.customers.for_customer(
    "erp-customer-1"
).submit_document({
    "externalId": "FA-2026-001",
    "idempotencyKey": "erp-fa-2026-001",
    "payload": invoice,
})

client.sapi.participants.for_participant(
    "0245:1234567890"
).documents.send(sapi_document, idempotency_key="sapi-fa-2026-001")
```

## PHP

```php
$result = $client->enterprise->documents->send($invoice);

$run = $client->enterprise->connector->customers
    ->for('erp-customer-1')
    ->submitDocument([
        'externalId' => 'FA-2026-001',
        'idempotencyKey' => 'erp-fa-2026-001',
        'payload' => $invoice,
    ]);

$client->sapi->participants
    ->for('0245:1234567890')
    ->documents
    ->send($sapiDocument, 'sapi-fa-2026-001');
```

## Ruby

```ruby
result = client.enterprise.documents.send(invoice)

run = client.enterprise.connector.customers
  .for_customer("erp-customer-1")
  .submit_document(
    externalId: "FA-2026-001",
    idempotencyKey: "erp-fa-2026-001",
    payload: invoice
  )

client.sapi.participants
  .for_participant("0245:1234567890")
  .documents
  .send(sapi_document, idempotency_key: "sapi-fa-2026-001")
```

## Java

```java
SendDocumentResponse result = client.enterprise().documents().send(invoice);

ConnectorAutopilotRunResponse run = client.enterprise()
    .connector()
    .customers()
    .forCustomer("erp-customer-1")
    .submitDocument(new ConnectorSubmitDocumentRequest(
        "FA-2026-001",
        "erp-fa-2026-001",
        invoicePayload
    ));

client.sapi()
    .participants()
    .forParticipant("0245:1234567890")
    .documents()
    .send(sapiDocument, "sapi-fa-2026-001");
```

## .NET

```csharp
var result = await client.Enterprise.Documents.SendAsync(invoice);

var run = await client.Enterprise.Connector.Customers
    .For("erp-customer-1")
    .SubmitDocumentAsync(new ConnectorSubmitDocumentRequest
    {
        ExternalId = "FA-2026-001",
        IdempotencyKey = "erp-fa-2026-001",
        Payload = invoicePayload
    });

await client.Sapi.Participants
    .For("0245:1234567890")
    .Documents
    .SendAsync(sapiDocument, "sapi-fa-2026-001");
```
