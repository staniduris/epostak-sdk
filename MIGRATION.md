# Workflow-first SDK adoption guide

This release gives each API product a clear role without removing the existing
Enterprise or SAPI integration surfaces:

- **Connector** is the recommended ERP path: business JSON, customer documents,
  polling, and one global webhook per integrator.
- **Enterprise API** remains the granular, firm-scoped API.
- **SAPI-SK** remains the strict participant-scoped interoperability profile.

The Connector update is additive. Existing `enterprise.connector` aliases stay
available silently, and existing Enterprise and SAPI clients do not need to
migrate to deploy this release.

Reference: [Connector guide](https://epostak.sk/api/docs/connector) and
[Connector OpenAPI](https://epostak.sk/api/openapi.connector.json).

## Managed Connector onboarding

1. ePošťák approves the integrator.
2. ePošťák approves and Peppol-registers each managed firm.
3. The integrator chooses and stores one stable ERP `customerRef` for that firm
   in the integrator dashboard.
4. The SDK sends business JSON through the customer-scoped Connector surface.

There is intentionally no Connector customer-creation endpoint. Enterprise
OAuth, Enterprise `firmId`, and SAPI participant scoping do not provision
Connector access.

## Public namespace mapping

| Existing or older shape | Recommended shape | Compatibility |
|-|-|-|
| `client.documents.send(...)` | `client.enterprise.documents.send(...)` | Existing Enterprise adapters remain available where shipped |
| `client.documents.inbox.list(...)` | `client.enterprise.documents.inbox.list(...)` | Enterprise behavior is unchanged |
| `client.inbound.list(...)` | `client.enterprise.pull.inbound.list(...)` | Enterprise behavior is unchanged |
| `client.outbound.events(...)` | `client.enterprise.pull.outbound.events(...)` | Enterprise behavior is unchanged |
| `client.enterprise.connector...` | `client.connector.customers.for(customerRef)...` | Enterprise Connector alias remains silent and supported |
| `client.sapi.send(body, { participantId })` | `client.sapi.participants.for(participantId).documents.send(body)` | SAPI remains participant-scoped |

## Connector golden path

The canonical customer surface provides `documents.send`, `documents.stage`,
`documents.get`, `documents.list`, `documents.acknowledge`, `documents.respond`, lifecycle
send/cancel, and `events.list`. The SDK injects `customerRef`, omits
`X-Firm-Id`, and derives a stable idempotency key when one is not supplied.

### TypeScript

```typescript
const customer = client.connector.customers.for("erp-acme");
const document = await customer.documents.send(invoice);
const inbox = await customer.documents.list({ direction: "inbound" });
const events = await customer.events.list({ limit: 50 });
```

### Python

```python
customer = client.connector.customers.for_customer("erp-acme")
document = customer.documents.send(invoice)
inbox = customer.documents.list(direction="inbound")
events = customer.events.list(limit=50)
```

### PHP

```php
$customer = $client->connector->customers->for('erp-acme');
$document = $customer->documents->send($invoice);
$inbox = $customer->documents->list(['direction' => 'inbound']);
$events = $customer->events->list(['limit' => 50]);
```

### Ruby

```ruby
customer = client.connector.customers.for_customer("erp-acme")
document = customer.documents.send(invoice)
inbox = customer.documents.list(direction: "inbound")
events = customer.events.list(limit: 50)
```

### Java

```java
var customer = client.connector().customers().forCustomer("erp-acme");
ConnectorBusinessDocument document = customer.documents().send(invoice);
ConnectorBusinessDocumentListResponse inbox = customer.documents().list(
    new ConnectorBusinessDocumentListParams("inbound", null, null, null, null, 20)
);
ConnectorBusinessEventsResponse events = customer.events().list(
    new ConnectorListParams(null, 50)
);
```

### .NET

```csharp
var customer = client.Connector.Customers.For("erp-acme");
var document = await customer.Documents.SendAsync(invoice);
var inbox = await customer.Documents.ListAsync(
    new ConnectorBusinessDocumentListParams { Direction = "inbound", Limit = 20 });
var events = await customer.Events.ListAsync(new ConnectorListParams { Limit = 50 });
```

## Inbound invoice lifecycle

`acknowledge` and `respond` have different effects. Acknowledge after the ERP
has processed the inbound document; this only records local processing and
does not notify the supplier. Respond when the ERP wants to send a business
state back over the network:

```typescript
const receivedDocumentId = "document-id-from-list-or-event";
await customer.documents.acknowledge(receivedDocumentId, `erp:${receivedDocumentId}`);
await customer.documents.respond(receivedDocumentId, {
  status: "accepted",
  note: "Imported and accepted",
});

// Direct alternative (do not call both); customerRef is explicit:
// await client.connector.documents.respond(
//   receivedDocumentId, "erp-acme", { status: "accepted" },
// );
```

Both response forms call
`POST /connector/documents/{documentId}/respond?customerRef=...`. The request
contains only a business `status` and optional `note`. Valid statuses are
`received`, `in_process`, `under_query`, `conditionally_accepted`, `rejected`,
`accepted`, and `paid`. The Connector owns the Peppol codes and XML mapping.
The response reports delivery as `sent` or `queued` and whether the operation
was an idempotent replay.

## Polling or one global webhook

Polling is available per customer through `customer.events`. Push is configured
once per integrator through `connector.webhook`:

- `get`
- `configure(url, events?)`
- `delete`
- `rotateSecret`
- `test(customerRef)`
- `deliveries`

Capture the result of `configure` before running a test. A new secret is shown
only once and must be persisted in a server-side secret manager immediately:

```typescript
const configuredWebhook = await client.connector.webhook.configure(
  "https://erp.example.com/webhooks/epostak",
  ["document.received", "document.delivered"],
);
if (configuredWebhook.secret) {
  // Persist this value in your server-side secret manager now. It is returned only once.
  const oneTimeWebhookSecret = configuredWebhook.secret;
}
await client.connector.webhook.test("erp-acme");
```

Webhook calls omit `X-Firm-Id`. The pushed body is the same canonical event item
returned by polling and includes `customerRef` at the root. Verify the signature
as HMAC-SHA256 over `timestamp + "." + rawBody` before parsing the body.

### Connector webhook debugger

No migration is required. New SDK releases add filtered history, the
exact signed body and attempt timeline, safe diagnosis, idempotent replay, and a
seven-scenario test suite. Existing webhook configuration, Enterprise, and SAPI
calls keep their current signatures and behavior.

## Compatibility rules

- Existing Enterprise document, Inbox, pull, webhook, OAuth, and firm-scoped
  integrations keep their current routes and behavior.
- Existing SAPI calls keep their participant header and standardized routes.
- Connector unsafe-POST retry opt-in is scoped to server-idempotent Connector
  operations; Enterprise and SAPI mutating POSTs do not inherit it. Safe-method
  retry hardening is shared by the transports.
- Compatibility aliases emit no runtime or compile-time deprecation warning.
- Connector `customerRef` is owned by the integrator; ePošťák owns integrator
  approval, firm approval, and Peppol registration.
