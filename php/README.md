# epostak/sdk

Official PHP SDK for the [ePošťák APIs](https://epostak.sk/api/docs) -- managed
Connector workflows, the Enterprise API, and SAPI-SK interoperability.

Requires PHP 8.1+ and Guzzle 7.

---

## Major release API shape

PHP `1.1.1` is the current workflow-first source release with the managed
Connector surface:

- Enterprise direct firm flow: `$client->enterprise->documents->send(...)`
- Connector ERP/integrator flow: `$client->connector->customers->for("erp-customer")->documents->send(...)`
- Enterprise API facade flow: `$client->enterprise->payloads->validate(...)`, `$client->enterprise->events->pull(...)`, `$client->enterprise->documents->supportPacket(...)`
- SAPI-SK interoperable flow: `$client->sapi->participants->for("0245:1234567890")->documents->send(...)`

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

```php
use EPostak\EPostak;

$connectorClient = new EPostak([
    'clientId' => getenv('EPOSTAK_CONNECTOR_CLIENT_ID'),
    'clientSecret' => getenv('EPOSTAK_CONNECTOR_CLIENT_SECRET'),
]);
$customer = $connectorClient->connector->customers->for('erp-acme');
$document = $customer->documents->send([
    'externalId' => 'invoice-2026-0042',
    'type' => 'invoice',
    'number' => '2026-0042',
    'recipient' => ['country' => 'SK', 'taxId' => '2120123456'],
    'lines' => [['description' => 'Monthly licence', 'quantity' => 1, 'unitPrice' => 100, 'vatRate' => 23]],
]);
```

The integrator owns the stable `customerRef` after ePošťák has approved and
Peppol-registered the firm. The SDK cannot create or discover firms. It injects `customerRef`, sends
immediately by default, and derives a stable idempotency key from `customerRef`
and `externalId`.

```php
$events = $customer->events->list(['limit' => 50]);
$configuredWebhook = $connectorClient->connector->webhook->configure(
    'https://erp.example.com/webhooks/epostak',
    ['document.received', 'document.delivered']
);
if (isset($configuredWebhook['secret'])) {
    // Persist this value in your server-side secret manager now. It is returned only once.
    $oneTimeWebhookSecret = $configuredWebhook['secret'];
}
$connectorClient->connector->webhook->test('erp-acme');
```

Push uses the same canonical event item as polling with `customerRef` at the
root. `WebhookSignature::verify(...)` verifies HMAC-SHA256 over
`timestamp . '.' . rawBody`.

### Acknowledge locally or respond to the supplier

```php
$receivedDocumentId = 'document-id-from-list-or-event';
$customer->documents->acknowledge($receivedDocumentId, 'erp:' . $receivedDocumentId);
$response = $customer->documents->respond($receivedDocumentId, [
    'status' => 'accepted',
    'note' => 'Imported and accepted',
]);

// Direct alternative (do not call both); customerRef is still mandatory:
// $connectorClient->connector->documents->respond(
//     $receivedDocumentId, 'erp-acme', ['status' => 'accepted']
// );
```

`acknowledge` only records that the inbound document was processed locally; it
does not notify the supplier. `respond` sends the network business response via
`POST /connector/documents/{documentId}/respond?customerRef=...`. Use only the
business statuses `received`, `in_process`, `under_query`,
`conditionally_accepted`, `rejected`, `accepted`, or `paid`, with an optional
`note`. The Connector handles Peppol response codes and XML; do not send either.
The result reports `response.delivery` as `sent` or `queued` and marks safe
replays with `idempotent`.

---

## Enterprise API facade flow

Use this as the default low-friction path for ERP integrations that do not want
to receive webhooks on day one:

```php
$client->enterprise->payloads->validate($invoice);

$client->enterprise->documents->preflight($invoice['receiverPeppolId']);

$sent = $client->enterprise->documents->send(
    $invoice,
    'erp-fa-2026-001'
);

$events = $client->enterprise->events->pull(['limit' => 50]);
$client->enterprise->events->batchAck(array_column($events['items'], 'event_id'));

$supportPacket = $client->enterprise->documents->supportPacket($sent['documentId']);
```

The older `$client->enterprise->webhooks->queue` resource remains available for
compatibility. New pull-based integrations should prefer
`$client->enterprise->events`.

Non-breaking adoption: facade helpers are additive. Existing
`$client->enterprise->extract`, `$client->enterprise->documents->validate`,
`$client->enterprise->webhooks->queue`, and
`$client->enterprise->documents->evidenceBundle` integrations can keep running;
migrate to `payloads`, `events`, and `supportPacket` when your release window
allows.

---

## Recent changes

### Included in v1.1.1 — 2026-07-16

- Fixed Enterprise inbound pull pagination so the cursor returned as
  `next_cursor` is sent on the next request through the documented `since`
  query parameter. The old PHP input key remains a compatibility alias.

### Included in v1.1.0 — 2026-07-14

- Connector is canonical at `$client->connector`; the existing Enterprise
  namespace alias remains silently supported.
- ePošťák approves the integrator and Peppol firms; the integrator stores its
  own stable `customerRef` in the dashboard.
- Customer document creation snapshots the request, validates explicit
  1-255-byte idempotency keys, and retries network errors, `429`, and all `5xx`
  only when safe. Lifecycle calls are server-idempotent; `409` is never retried.
- `EPostakError` exposes `getField()`, `getNextAction()`, `isRetryable()`,
  `getRequestId()`, and `getRetryAfter()`.
- `customerRef` and `externalId` use the backend ECMAScript `TrimString` code
  points before hashing; `U+0085` is preserved.
- `document.cancelled` is a first-class business event.

### Included in v1.1.0 — 2026-07-12

- JSON billing payloads now expose the live receiver address, `prepaidAmount`,
  `prepayments`, and advanced line-item VAT/classification fields from the
  Enterprise OpenAPI.

### Included in v1.1.0 — 2026-07-01

- Enterprise API facade helpers for Payload Assistant validation, pull/ack
  event handling, and document support packets:
  `$client->enterprise->payloads->validate(...)`,
  `$client->enterprise->events->pull(...)`, and
  `$client->enterprise->documents->supportPacket(...)`.

### Included in v1.1.0 — 2026-06-30

- Customer-scoped
  `$client->connector->customers->for($customerRef)->advanced->mapper(...)`
  is the managed, preview-only Mapper flow. Top-level
  `$client->connector->advanced->mapper(...)` remains a legacy compatibility
  alias and is unavailable with managed Connector credentials.
- `$client->box` / `$client->enterprise->box` covers ePošťák Box list, create
  with `payloadXml`, detail, schedule, send-now, retry, and cancel over
  `/box/items`.

### v1.0.0 — 2026-06-14

- `$client->enterprise` is the documented namespace for Documents, Inbox, Pull
  APIs, Connector, Peppol, Firms, Webhooks, Reporting, Auth, Account, Extract,
  Audit, and Integrator surfaces.
- `$client->sapi->participants->for($participantId)->documents` requires
  participant scoping before SAPI document send/receive/get/acknowledge.
- `$client->connector->customers->for($customerRef)` injects
  `customerRef` and keeps `X-Firm-Id` off customer-managed Connector calls.
- The Connector compatibility surface includes preflight, Zen input, Autopilot,
  reconcile, mailbox, sync, document evidence, and event polling.
- Docs: added the Connector golden path for ERP developers: auth, preflight, stage, send, status, inbox, ACK, and evidence.

### v0.10.0 — 2026-05-18

- `$client->sapi` covers SAPI-SK 1.0 document send, receive list/detail, and acknowledge.
- `$client->enterprise->webhooks->test($id, ['count' => 250, 'mode' => 'queued'])` supports direct and queued webhook tests.
- `$client->enterprise->webhooks->deadLetters()`, `replayDeadLetter($id)`, and `resolveDeadLetter($id, $reason)` cover webhook DLQ operations.
- `$client->enterprise->peppol->resolve([...])` resolves ERP identifiers to Peppol participant + routing capability.
- Added evidence bundle, outbound MDN, company search, Peppol document listing, and license info helpers.

---

## Installation

The PHP SDK is currently distributed as reviewed source, not through Packagist.
Clone this repository, then add its `php/` directory as a Composer path
repository in your application:

```bash
git clone https://github.com/staniduris/epostak-sdk.git
cd /path/to/your-project
composer config repositories.epostak path /path/to/epostak-sdk/php
composer require epostak/sdk:^1.1
```

---

## Quick Start

```php
use EPostak\EPostak;
use EPostak\EPostakError;

$client = new EPostak(['clientId' => 'sk_live_xxxxx', 'clientSecret' => 'your-client-secret']);

// Send an invoice
$result = $client->enterprise->documents->send([
    'receiverPeppolId' => '0245:1234567890',
    'receiverName' => 'Firma s.r.o.',
    'invoiceNumber' => 'FV-2026-001',
    'issueDate' => '2026-04-04',
    'dueDate' => '2026-04-18',
    'items' => [
        ['description' => 'Consulting', 'quantity' => 10, 'unitPrice' => 50, 'vatRate' => 23],
    ],
]);

echo $result['documentId'];  // Document UUID
echo $result['messageId'];   // Peppol message ID
echo $result['status'];      // 'SENT'
```

### Connector golden path for ERP developers

Start from the stable `customerRef` configured by the integrator in the
dashboard after ePošťák approves and Peppol-registers the firm. There is no SDK
method for creating or discovering firms.

```php
$connectorClient = new EPostak([
    'clientId' => getenv('EPOSTAK_CONNECTOR_CLIENT_ID'),
    'clientSecret' => getenv('EPOSTAK_CONNECTOR_CLIENT_SECRET'),
]);
$customer = $connectorClient->connector->customers->for('erp-customer-1');
$document = $customer->documents->send([
    'externalId' => 'FA-2026-001',
    'type' => 'invoice',
    'number' => 'FA-2026-001',
    'issueDate' => '2026-07-14',
    'dueDate' => '2026-07-28',
    'currency' => 'EUR',
    'recipient' => ['country' => 'SK', 'taxId' => '2120123456'],
    'lines' => [
        ['description' => 'Služby', 'quantity' => 1, 'unitPrice' => 100, 'vatRate' => 23],
    ],
]);

$page = $customer->documents->list(['state' => 'needs_attention']);
$events = $customer->events->list(['limit' => 50]);
foreach ($page['documents'] as $received) {
    if ($received['direction'] === 'inbound') {
        $customer->documents->acknowledge($received['id'], 'erp:' . $received['id']);
    }
}
$ubl = $customer->advanced->documents->ubl($document['id']);
$evidence = $customer->advanced->documents->evidence($document['id']);
$evidenceBundle = $customer->advanced->documents->evidenceBundle($document['id']);
$supportPacket = $customer->advanced->documents->supportPacket($document['id']);
echo $document['id'] . ' ' . $events['nextCursor'];
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
`$customer->documents->stage(...)` instead of `send(...)` when the ERP must hold
a document for review, then call `$customer->documents->sendDocument($id)` or
`cancelDocument($id)`.

Mapper is a customer-scoped preview/normalization helper; it never stages or
sends a document:

```php
$preview = $customer->advanced->mapper([
    'templateKey' => 'pohoda-csv-v1',
    'sourceType' => 'csv',
    'sourceText' => $csv,
]);
$normalized = $preview['document'];
```

Managed Connector credentials support customer documents/events, one global
webhook, audited document evidence, and Mapper preview. Legacy preflight, raw send, Outbox,
Autopilot, reconciliation, mailbox, sync, and action helpers remain supported
source-compatibility aliases; availability depends on the credential's product
entitlement. The Connector property under the Enterprise namespace also
remains silently supported.

### Connector errors and retries

```php
try {
    $customer->documents->send($invoice);
} catch (EPostakError $error) {
    error_log((string) $error->getErrorCode() . ' ' . $error->getMessage());
    error_log((string) $error->getField() . ' ' . (string) $error->getNextAction());
    error_log(json_encode([$error->isRetryable(), $error->getRequestId(), $error->getRetryAfter()]));
}
```

Keyed document creation retries network failures, `429`, and every `5xx` while
reusing the exact body and key. Lifecycle send/cancel/acknowledge calls are
server-idempotent and use the same policy. `Retry-After` is honored; `409` is
always surfaced once and never retried automatically.

---

## SAPI-SK participant flow

```php
$participant = $client->sapi->participants->for('0245:1234567890');
$participant->documents->send([
    'metadata' => [
        'documentId' => 'FA-2026-001',
        'documentTypeId' => 'invoice',
        'processId' => 'billing',
        'senderParticipantId' => '0245:1234567890',
        'receiverParticipantId' => '0245:0987654321',
        'creationDateTime' => '2026-06-14T10:00:00Z',
    ],
    'payload' => '<Invoice/>',
    'payloadFormat' => 'XML',
], 'sapi-fa-2026-001');
```

---

## Authentication

| Key prefix  | Use case                                            |
| ----------- | --------------------------------------------------- |
| `sk_live_*` | Direct access -- acts on behalf of your own firm    |
| `sk_int_*`  | Product-scoped integrator access; ePošťák approves Connector firms and the integrator configures `customerRef` |

Generate Enterprise keys in ePošťák firm Settings or via the dashboard.
ePošťák issues Connector credentials after approval; the integrator configures
`customerRef` for each approved Peppol firm in the dashboard.

### Constructor options

```php
$client = new EPostak([
    'clientId'     => 'sk_live_xxxxx',     // Required
    'clientSecret' => 'your-secret',       // Required
    'baseUrl'      => 'https://dev.epostak.sk/api/v1', // Optional test env; omit for prod
    'firmId'       => 'uuid',             // Optional, for legacy firm-scoped calls
]);
```

Managed Connector calls use Connector credentials and the integrator's stored
`customerRef` for an ePošťák-approved Peppol firm and do not need `firmId`.
Ordinary Enterprise credentials cannot provision or scout Connector firms. Use
`firmId` or `withFirm(...)` only for Enterprise calls
that target one firm directly.

Production is the SDK default: Enterprise `https://epostak.sk/api/v1`, SAPI
`https://epostak.sk/sapi/v1`, OAuth origin `https://epostak.sk`. For test
calls, set `baseUrl` to `https://dev.epostak.sk/api/v1`; SAPI derives
`https://dev.epostak.sk/sapi/v1`, and OAuth helpers need
`origin => "https://dev.epostak.sk"` because OAuth is outside `/api/v1`.

---

## API Reference

### Documents

#### `documents->send($body)` -- Send a document via Peppol

**JSON mode** -- structured data, UBL XML is auto-generated:

```php
$result = $client->enterprise->documents->send([
    'receiverPeppolId' => '0245:1234567890',
    'receiverName' => 'Firma s.r.o.',
    'receiverIco' => '12345678',
    'receiverCountry' => 'SK',
    'invoiceNumber' => 'FV-2026-001',
    'issueDate' => '2026-04-04',
    'dueDate' => '2026-04-18',
    'currency' => 'EUR',
    'iban' => 'SK1234567890123456789012',
    'items' => [
        ['description' => 'Consulting', 'quantity' => 10, 'unitPrice' => 50, 'vatRate' => 23],
    ],
]);
// => ['documentId' => '...', 'messageId' => '...', 'status' => 'SENT']
```

**XML mode** -- send a pre-built UBL XML document:

```php
$result = $client->enterprise->documents->send([
    'receiverPeppolId' => '0245:1234567890',
    'xml' => '<?xml version="1.0" encoding="UTF-8"?>...',
]);
```

#### `documents->get($id)` -- Get a document by ID

```php
$doc = $client->enterprise->documents->get('doc-uuid');
// => ['id', 'number', 'status', 'direction', 'docType', 'issueDate', ...]
```

#### `documents->update($id, $data)` -- Update a draft document

Only documents with status `draft` can be updated. All fields are optional.

```php
$updated = $client->enterprise->documents->update('doc-uuid', [
    'invoiceNumber' => 'FV-2026-002',
    'dueDate' => '2026-05-01',
    'items' => [['description' => 'Development', 'quantity' => 20, 'unitPrice' => 75, 'vatRate' => 23]],
]);
```

#### `documents->status($id)` -- Full status with history

```php
$status = $client->enterprise->documents->status('doc-uuid');
// => ['id', 'status', 'statusHistory' => [['status', 'timestamp', 'detail']], ...]
```

#### `documents->evidence($id)` -- Delivery evidence

```php
$evidence = $client->enterprise->documents->evidence('doc-uuid');
// => ['documentId', 'as4Receipt', 'mlrDocument', 'invoiceResponse', 'deliveredAt', 'sentAt']
```

#### `documents->pdf($id)` -- Download PDF as raw bytes

```php
$pdf = $client->enterprise->documents->pdf('doc-uuid');
file_put_contents('invoice.pdf', $pdf);
```

#### `documents->ubl($id)` -- Download UBL XML as string

```php
$ubl = $client->enterprise->documents->ubl('doc-uuid');
```

#### `documents->respond($id, $status, $note)` -- Send invoice response

```php
$response = $client->enterprise->documents->respond('doc-uuid', 'AP', 'Invoice accepted');
// $status: 'AP' = accepted, 'RE' = rejected, 'UQ' = under query
// => ['documentId', 'responseStatus', 'respondedAt']
```

#### `documents->validate($body)` -- Validate without sending

```php
$validation = $client->enterprise->documents->validate([
    'receiverPeppolId' => '0245:1234567890',
    'receiverName' => 'Firma s.r.o.',
    'items' => [['description' => 'Test', 'quantity' => 1, 'unitPrice' => 100, 'vatRate' => 23]],
]);
// => ['valid' => true/false, 'warnings' => [...], 'ubl' => '...' or null]
```

#### `documents->preflight($receiverPeppolId, $documentTypeId)` -- Check receiver capability

```php
$check = $client->enterprise->documents->preflight('0245:1234567890');
// => ['receiverPeppolId', 'registered', 'supportsDocumentType', 'smpUrl']
```

#### `documents->convert($inputFormat, $outputFormat, $document)` -- Convert between JSON and UBL

```php
// JSON to UBL
$result = $client->enterprise->documents->convert('json', 'ubl', ['receiverName' => 'Firma s.r.o.', 'invoiceNumber' => 'FV-001', 'items' => [...]]);
echo $result['document']; // UBL XML string

// UBL to JSON
$result = $client->enterprise->documents->convert('ubl', 'json', '<Invoice xmlns="...">...</Invoice>');
print_r($result['document']); // associative array
// $result['output_format'], $result['warnings']
```

---

### Inbox

Access via `$client->enterprise->documents->inbox`.

#### `documents->inbox->list($params)` -- List received documents

```php
$inbox = $client->enterprise->documents->inbox->list([
    'limit' => 20,            // 1-100, default 20
    'offset' => 0,
    'status' => 'RECEIVED',   // 'RECEIVED' | 'ACKNOWLEDGED'
    'since' => '2026-04-01T00:00:00Z',
]);
// => ['documents' => [...], 'total', 'limit', 'offset']
```

#### `documents->inbox->get($id)` -- Full detail with UBL XML

```php
$detail = $client->enterprise->documents->inbox->get('doc-uuid');
// $detail['document'] -- InboxDocument
// $detail['payload']  -- UBL XML string or null
```

#### `documents->inbox->acknowledge($id)` -- Mark as processed

```php
$ack = $client->enterprise->documents->inbox->acknowledge('doc-uuid');
// => ['documentId', 'status' => 'ACKNOWLEDGED', 'acknowledgedAt']
```

#### `documents->inbox->listAll($params)` -- Cross-firm inbox (integrator)

```php
$all = $client->enterprise->documents->inbox->listAll([
    'limit' => 50,
    'status' => 'RECEIVED',
    'firm_id' => 'specific-firm-uuid',  // Optional filter
]);
// Each document includes firm_id and firm_name
```

---

### Peppol

#### `peppol->lookup($scheme, $identifier)` -- SMP participant lookup

```php
$participant = $client->enterprise->peppol->lookup('0245', '12345678');
if ($participant['accepts'] && $participant['routingStatus'] === 'ready') {
    // Receiver is registered and routable for the default BIS Billing invoice.
}

$caps = $client->enterprise->peppol->capabilities(
    '0245',
    '12345678',
    'urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##...'
);
```

#### `peppol->directory->search($params)` -- Business Card directory

```php
$results = $client->enterprise->peppol->directory->search([
    'q' => 'Telekom',
    'country' => 'SK',
    'page' => 0,
    'page_size' => 20,
]);
// => ['results' => [...], 'total', 'page', 'page_size']
```

#### `peppol->companyLookup($ico)` -- Slovak company lookup

```php
$company = $client->enterprise->peppol->companyLookup('12345678');
// => ['ico', 'name', 'dic', 'icDph', 'address', 'peppolId']

$matches = $client->enterprise->peppol->companySearch('Demo', 10);

$resolved = $client->enterprise->peppol->resolve([
    'ico' => '12345678',
    'documentTypeId' => 'urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##...',
]);
```

---

### Firms (integrator keys)

#### `firms->list()` -- List all accessible firms

```php
$firms = $client->enterprise->firms->list();
// => ['firms' => [['id', 'name', 'ico', 'peppolId', 'peppolStatus'], ...]]
```

#### `firms->get($id)` -- Firm detail

```php
$firm = $client->enterprise->firms->get('firm-uuid');
// => ['id', 'name', 'ico', 'peppolId', 'peppolStatus', 'dic', 'icDph', 'address', ...]
```

#### `firms->documents($id, $params)` -- List firm documents

```php
$docs = $client->enterprise->firms->documents('firm-uuid', [
    'limit' => 20,
    'direction' => 'inbound',  // 'inbound' | 'outbound'
]);
```

#### `firms->registerPeppolId($id, $scheme, $identifier)` -- Register Peppol ID

```php
$result = $client->enterprise->firms->registerPeppolId('firm-uuid', '0245', '12345678');
// => ['peppolId', 'scheme', 'identifier', 'registeredAt']
```

#### `firms->assign($ico)` -- Assign firm to integrator

```php
$result = $client->enterprise->firms->assign('12345678');
// => ['firm' => ['id', 'name', 'ico', ...], 'status' => 'active']
```

#### `firms->assignBatch($icos)` -- Batch assign firms (max 50)

```php
$result = $client->enterprise->firms->assignBatch(['12345678', '87654321', '11223344']);
// => ['results' => [['ico', 'firm' => ..., 'status' => ..., 'error' => ...], ...]]
```

---

### Webhooks

#### `webhooks->create($url, $events)` -- Register a webhook

```php
$webhook = $client->enterprise->webhooks->create(
    'https://example.com/webhook',
    ['document.received', 'document.sent']
);
// Store $webhook['secret'] for HMAC-SHA256 signature verification
```

#### `webhooks->list()` -- List webhooks

```php
$webhooks = $client->enterprise->webhooks->list();
```

#### `webhooks->get($id)` -- Webhook detail with deliveries

```php
$detail = $client->enterprise->webhooks->get('webhook-uuid');
// => [...webhook, 'deliveries' => [...]]

$queued = $client->enterprise->webhooks->test('webhook-uuid', [
    'event' => 'document.received',
    'count' => 250,
    'mode' => 'queued',
]);

$deliveries = $client->enterprise->webhooks->deliveries('webhook-uuid', [
    'testRunId' => $queued['testRunId'],
    'includeResponseBody' => true,
]);

$dlq = $client->enterprise->webhooks->deadLetters(['includeResponseBody' => true]);
foreach ($dlq['items'] as $failed) {
    $client->enterprise->webhooks->replayDeadLetter($failed['id']);
    // or: $client->enterprise->webhooks->resolveDeadLetter($failed['id'], 'Handled in ERP');
}
```

#### `webhooks->update($id, $data)` -- Update webhook

```php
$client->enterprise->webhooks->update('webhook-uuid', [
    'url' => 'https://example.com/new-webhook',
    'events' => ['document.received'],
    'isActive' => true,
]);
```

#### `webhooks->delete($id)` -- Delete webhook

```php
$client->enterprise->webhooks->delete('webhook-uuid');
```

#### Verifying webhook signatures

The server sends `X-Webhook-Signature: sha256=<hex>` and `X-Webhook-Timestamp: <unix>`.
Use `\EPostak\WebhookSignature::verify()` to validate deliveries:

```php
use EPostak\WebhookSignature;

$raw = file_get_contents('php://input');
$result = WebhookSignature::verify(
    signature:  $_SERVER['HTTP_X_WEBHOOK_SIGNATURE'] ?? '',
    timestamp:  $_SERVER['HTTP_X_WEBHOOK_TIMESTAMP'] ?? '',
    body:       $raw,
    secret:     getenv('EPOSTAK_WEBHOOK_SECRET'),
);

if (!$result['valid']) {
    http_response_code(400);
    exit('bad signature: ' . $result['reason']);
}

$event = json_decode($raw, true);
```

#### Dedup + retry headers (server v1.1 — 2026-05-12)

Three new headers on every push delivery:

| Header | Value | Use |
|-|-|-|
| `X-Webhook-Event-Id` | UUID, stable across retries | **Primary dedup key.** Body also carries it as `webhook_event_id`. |
| `X-Webhook-Attempt` | 1-based attempt number | Telemetry / logging. |
| `X-Webhook-Max-Attempts` | Total attempts in the retry window (10) | Telemetry / logging. |

Recommended receiver pattern:

```php
// INSERT ON CONFLICT DO NOTHING on the event id is enough — every retry
// of the same logical event carries the SAME X-Webhook-Event-Id.
$eventId = $_SERVER['HTTP_X_WEBHOOK_EVENT_ID'] ?? '';
$stmt = $pdo->prepare(
    'INSERT INTO processed_webhooks (event_id) VALUES (:id) '
    . 'ON CONFLICT (event_id) DO NOTHING'
);
$stmt->execute(['id' => $eventId]);
if ($stmt->rowCount() === 0) {
    http_response_code(200);
    exit; // duplicate — ack and skip
}
// process event for the first time...
```

**Retry policy (server-side, as of 2026-05-12):** retries fire only for `408`, `425`, `429`, `502`, `503`, `504` and network errors (~44h bounded backoff). Returning any other 4xx/5xx — including `500` — terminates the retry loop immediately. If your handler wants a retry on a transient failure, return `503` (not `500`).

The signature contract is **unchanged** — `WebhookSignature::verify()` continues to work without code changes.

---

### Events pull facade

Alternative to push webhooks -- poll for events. Access via
`$client->enterprise->events`.

#### `events->pull($params)` -- Fetch pending events

```php
$queue = $client->enterprise->events->pull([
    'limit' => 50,                          // 1-100, default 20
    'event_type' => 'document.received',    // Optional filter
]);

foreach ($queue['items'] as $item) {
    echo $item['id'] . ' ' . $item['type'] . "\n";
    // Process $item['payload']
}
// $queue['has_more'] -- bool
```

#### `events->ack($eventId)` -- Acknowledge single event

```php
$client->enterprise->events->ack('event-uuid');
// Returns ['acknowledged' => true]
```

#### `events->batchAck($eventIds)` -- Batch acknowledge

```php
$ids = array_column($queue['items'], 'event_id');
$client->enterprise->events->batchAck($ids);
// Returns ['acknowledged' => count($ids)]
```

`$client->enterprise->webhooks->queue` remains available for legacy queue and
cross-firm drain jobs.

#### `webhooks->queue->pullAll($params)` -- Cross-firm queue (integrator)

```php
$queue = $client->enterprise->webhooks->queue->pullAll([
    'limit' => 200,   // 1-500, default 100
    'since' => '2026-04-01T00:00:00Z',
]);

foreach ($queue['items'] as $event) {
    echo $event['firm_id'] . ' ' . $event['event'] . "\n";
}
// => ['items' => [...], 'has_more' => false]
```

#### `webhooks->queue->batchAckAll($eventIds)` -- Cross-firm batch ack (integrator)

```php
$ids = array_column($queue['items'], 'event_id');
$result = $client->enterprise->webhooks->queue->batchAckAll($ids);
echo $result['acknowledged'];  // number of events acknowledged
```

---

### Reporting

#### `reporting->statistics($params)` -- Aggregated stats

```php
$stats = $client->enterprise->reporting->statistics([
    'from' => '2026-01-01',
    'to' => '2026-03-31',
]);
// => ['period' => ['from', 'to'],
//     'outbound' => ['total', 'delivered', 'failed'],
//     'inbound' => ['total', 'acknowledged', 'pending']]
```

---

### Account

#### `account->get()` -- Account info

```php
$account = $client->enterprise->account->get();
// => ['firm' => ['name', 'ico', 'peppolId', 'peppolStatus'],
//     'plan' => ['name', 'status'],
//     'usage' => ['outbound', 'inbound']]
```

---

### Payload Assistant OCR

Requires Enterprise plan.

#### `payloads->extract($filePath, $mimeType, $fileName)` -- Single file

```php
$result = $client->enterprise->payloads->extract(
    '/path/to/invoice.pdf',
    'application/pdf',
    'invoice.pdf'    // Optional, defaults to basename
);
// => ['extraction' => [...], 'ubl_xml' => '...', 'confidence' => 0.95, 'file_name' => '...']
// Outbound invoices can include $result['send_payload'] for review + validate + send.
```

Supported MIME types: `application/pdf`, `image/jpeg`, `image/png`, `image/webp`. Max 20 MB.

#### `payloads->extractBatch($files)` -- Batch extraction (up to 50 files)

```php
$result = $client->enterprise->payloads->extractBatch([
    ['filePath' => '/path/to/inv1.pdf', 'mimeType' => 'application/pdf', 'fileName' => 'inv1.pdf'],
    ['filePath' => '/path/to/inv2.png', 'mimeType' => 'image/png'],
]);
// => ['batch_id', 'total', 'successful', 'failed',
//     'results' => [['file_name', 'extraction', 'ubl_xml', 'confidence', 'error'], ...]]
```

`$client->enterprise->extract` remains available for compatibility; new
integrations should use `$client->enterprise->payloads->extract`.

---

## Enterprise Integrator Mode

This section describes Enterprise `sk_int_*` credentials and multi-tenant
Enterprise endpoints only. It does not provision Connector. Managed Connector
uses its own approved credentials; the integrator chooses each stable
`customerRef` in the dashboard after ePošťák approves the firm. Setting
`firmId` does not change the Connector entitlement.

```php
// Option 1: pass firmId in constructor
$client = new EPostak(['clientId' => 'sk_int_xxxxx', 'clientSecret' => 'your-secret', 'firmId' => 'client-firm-uuid']);

// Option 2: scope at call time with withFirm()
$base = new EPostak(['clientId' => 'sk_int_xxxxx', 'clientSecret' => 'your-secret']);
$clientA = $base->withFirm('firm-uuid-a');
$clientB = $base->withFirm('firm-uuid-b');

$clientA->enterprise->documents->send([...]);
$clientB->enterprise->documents->inbox->list();
```

### Integrator-only endpoints

| Method                               | Description                          |
| ------------------------------------ | ------------------------------------ |
| `firms->assign($ico)`                | Link a firm to the integrator        |
| `firms->assignBatch($icos)`          | Batch link firms (max 50)            |
| `documents->inbox->listAll()`        | Cross-firm inbox with firm_id filter |
| `webhooks->queue->pullAll()`         | Cross-firm event queue               |
| `webhooks->queue->batchAckAll($ids)` | Cross-firm batch acknowledge         |

---

## Error Handling

All API errors are thrown as `EPostakError`:

```php
use EPostak\EPostak;
use EPostak\EPostakError;

try {
    $client->enterprise->documents->send([...]);
} catch (EPostakError $e) {
    echo $e->getStatus();     // HTTP status code (0 for network errors)
    echo $e->getErrorCode();  // Machine-readable code, e.g. 'VALIDATION_FAILED'
    echo $e->getMessage();    // Human-readable message
    var_dump($e->getDetails()); // Validation details (for 422 errors)
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
| 422    | `UNPROCESSABLE_ENTITY` | Validation failed (check `$e->getDetails()`)     |
| 429    | `RATE_LIMITED`         | Too many requests                                |
| 503    | `SERVICE_UNAVAILABLE`  | Extraction service not configured                |

---

## Full API Endpoint Map

| SDK Method                                                   | HTTP   | Path                                 |
| ------------------------------------------------------------ | ------ | ------------------------------------ |
| `documents->get($id)`                                        | GET    | `/documents/{id}`                    |
| `documents->update($id, $data)`                              | PATCH  | `/documents/{id}`                    |
| `documents->send($body)`                                     | POST   | `/documents/send`                    |
| `documents->sendBatch($items, $idempotencyKey)`               | POST   | `/documents/send/batch`              |
| `documents->status($id)`                                     | GET    | `/documents/{id}/status`             |
| `documents->statusBatch($ids)`                               | POST   | `/documents/status/batch`            |
| `documents->evidence($id)`                                   | GET    | `/documents/{id}/evidence`           |
| `documents->evidenceBundle($id)`                             | GET    | `/documents/{id}/evidence-bundle`    |
| `documents->supportPacket($id)`                              | GET    | `/documents/{id}/support-packet`     |
| `documents->envelope($id)`                                   | GET    | `/documents/{id}/envelope`           |
| `documents->pdf($id)`                                        | GET    | `/documents/{id}/pdf`                |
| `documents->ubl($id)`                                        | GET    | `/documents/{id}/ubl`                |
| `documents->respond($id, $status, $note)`                    | POST   | `/documents/{id}/respond`            |
| `documents->mark($id, $state, $note)`                        | POST   | `/documents/{id}/mark`               |
| `documents->validate($body)`                                 | POST   | `/documents/validate`                |
| `documents->preflight($receiverPeppolId)`                    | POST   | `/documents/preflight`               |
| `documents->convert($inputFormat, $outputFormat, $document)` | POST   | `/documents/convert`                 |
| `documents->parse($xml)`                                     | POST   | `/documents/parse`                   |
| `documents->outbox($params)`                                 | GET    | `/documents/outbox`                  |
| `documents->responses($id)`                                  | GET    | `/documents/{id}/responses`          |
| `documents->events($id, $params)`                            | GET    | `/documents/{id}/events`             |
| `documents->peppolDocuments($params)`                        | GET    | `/peppol-documents`                  |
| `documents->inbox->list($params)`                            | GET    | `/documents/inbox`                   |
| `documents->inbox->get($id)`                                 | GET    | `/documents/inbox/{id}`              |
| `documents->inbox->acknowledge($id)`                         | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents->inbox->listAll($params)`                         | GET    | `/documents/inbox/all`               |
| `peppol->lookup($scheme, $identifier)`                       | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol->directory->search($params)`                         | GET    | `/peppol/directory/search`           |
| `peppol->companyLookup($ico)`                                | GET    | `/company/lookup/{ico}`              |
| `peppol->companySearch($q, $limit)`                          | GET    | `/company/search`                    |
| `peppol->resolve($params)`                                   | GET    | `/peppol/participants/resolve`       |
| `peppol->capabilities($scheme, $identifier, $documentType)`  | POST   | `/peppol/capabilities`               |
| `peppol->lookupBatch($participants)`                         | POST   | `/peppol/participants/batch`         |
| `firms->list()`                                              | GET    | `/firms`                             |
| `firms->get($id)`                                            | GET    | `/firms/{id}`                        |
| `firms->documents($id, $params)`                             | GET    | `/firms/{id}/documents`              |
| `firms->registerPeppolId($id, $scheme, $identifier)`         | POST   | `/firms/{id}/peppol-identifiers`     |
| `firms->assign($ico)`                                        | POST   | `/firms/assign`                      |
| `firms->assignBatch($icos)`                                  | POST   | `/firms/assign/batch`                |
| `webhooks->create($url, $events)`                            | POST   | `/webhooks`                          |
| `webhooks->list()`                                           | GET    | `/webhooks`                          |
| `webhooks->get($id)`                                         | GET    | `/webhooks/{id}`                     |
| `webhooks->update($id, $data)`                               | PATCH  | `/webhooks/{id}`                     |
| `webhooks->delete($id)`                                      | DELETE | `/webhooks/{id}`                     |
| `webhooks->test($id, $params)`                               | POST   | `/webhooks/{id}/test`                |
| `webhooks->deliveries($id, $params)`                         | GET    | `/webhooks/{id}/deliveries`          |
| `webhooks->deadLetters($params)`                             | GET    | `/webhook-dead-letter`               |
| `webhooks->replayDeadLetter($id)`                            | POST   | `/webhook-dead-letter/{id}/replay`   |
| `webhooks->resolveDeadLetter($id, $reason)`                  | POST   | `/webhook-dead-letter/{id}/resolve`  |
| `webhooks->queue->pull($params)`                             | GET    | `/webhook-queue`                     |
| `webhooks->queue->ack($eventId)`                             | DELETE | `/webhook-queue/{eventId}`           |
| `webhooks->queue->batchAck($ids)`                            | POST   | `/webhook-queue/batch-ack`           |
| `webhooks->queue->pullAll($params)`                          | GET    | `/webhook-queue/all`                 |
| `webhooks->queue->batchAckAll($ids)`                         | POST   | `/webhook-queue/all/batch-ack`       |
| `payloads->extract($filePath, $mimeType, $fileName)`         | POST   | `/payloads/extract`                  |
| `payloads->extractBatch($files)`                             | POST   | `/payloads/extract/batch`            |
| `payloads->parse($xml)`                                      | POST   | `/payloads/parse`                    |
| `payloads->convert($inputFormat, $outputFormat, $document)`  | POST   | `/payloads/convert`                  |
| `payloads->validate($body)`                                  | POST   | `/payloads/validate`                 |
| `events->pull($params)`                                      | GET    | `/events/pull`                       |
| `events->ack($eventId)`                                      | POST   | `/events/{eventId}/ack`              |
| `events->batchAck($ids)`                                     | POST   | `/events/batch-ack`                  |
| `inbound->list($params)`                                     | GET    | `/inbound/documents`                 |
| `inbound->get($id)`                                          | GET    | `/inbound/documents/{id}`            |
| `inbound->getUbl($id)`                                       | GET    | `/inbound/documents/{id}/ubl`        |
| `inbound->ack($id, $params)`                                 | POST   | `/inbound/documents/{id}/ack`        |
| `outbound->list($params)`                                    | GET    | `/outbound/documents`                |
| `outbound->get($id)`                                         | GET    | `/outbound/documents/{id}`           |
| `outbound->getUbl($id)`                                      | GET    | `/outbound/documents/{id}/ubl`       |
| `outbound->getMdn($id)`                                      | GET    | `/outbound/documents/{id}/mdn`       |
| `outbound->events($params)`                                  | GET    | `/outbound/events`                   |
| `reporting->statistics($params)`                             | GET    | `/reporting/statistics`              |
| `reporting->submissions($params)`                            | GET    | `/reporting/submissions`             |
| `account->get()`                                             | GET    | `/account`                           |
| `account->licenseInfo()`                                     | GET    | `/licenses/info`                     |
| `integrator->keys->list()`                                   | GET    | `/integrator/keys`                   |
| `integrator->keys->deactivate($params)`                      | DELETE | `/integrator/keys`                   |
| `integrator->licenses->info($params)`                        | GET    | `/integrator/licenses/info`          |
| `extract->single($path, $mime)`                              | POST   | `/extract`                           |
| `extract->batch($files)`                                     | POST   | `/extract/batch`                     |
| `EPostak::validate($xml)`                                    | POST   | `https://epostak.sk/api/validate`    |
| `box->list($params)`                                         | GET    | `/box/items`                         |
| `box->create(['payloadXml' => ...])`                         | POST   | `/box/items`                         |
| `box->get($itemId)`                                          | GET    | `/box/items/{itemId}`                |
| `box->schedule($itemId, $scheduledFor)`                      | POST   | `/box/items/{itemId}/schedule`       |
| `box->sendNow($itemId)`                                      | POST   | `/box/items/{itemId}/send-now`       |
| `box->retry($itemId)`                                        | POST   | `/box/items/{itemId}/retry`          |
| `box->cancel($itemId)`                                       | POST   | `/box/items/{itemId}/cancel`         |
| `connector->customers->for($ref)->documents->send($body)`    | POST   | `/connector/documents`               |
| `connector->customers->for($ref)->documents->stage($body)`   | POST   | `/connector/documents`               |
| `connector->customers->for($ref)->documents->list($params)`  | GET    | `/connector/documents`               |
| `connector->customers->for($ref)->documents->get($id)`       | GET    | `/connector/documents/{documentId}`  |
| `connector->customers->for($ref)->documents->acknowledge($id, $ref)` | POST | `/connector/documents/{documentId}/acknowledge` |
| `connector->customers->for($ref)->documents->respond($id, $body)` | POST | `/connector/documents/{documentId}/respond` |
| `connector->customers->for($ref)->documents->sendDocument($id)` | POST | `/connector/documents/{documentId}/send` |
| `connector->customers->for($ref)->documents->cancelDocument($id)` | POST | `/connector/documents/{documentId}/cancel` |
| `connector->customers->for($ref)->events->list($params)`     | GET    | `/connector/events`                  |
| `connector->customers->for($ref)->advanced->mapper($body)`   | POST   | `/connector/mapper`                  |
| `connector->customers->for($ref)->advanced->documents->ubl($id)` | GET | `/connector/documents/{documentId}/ubl` |
| `connector->customers->for($ref)->advanced->documents->evidence($id)` | GET | `/connector/documents/{documentId}/evidence` |
| `connector->customers->for($ref)->advanced->documents->evidenceBundle($id)` | GET | `/connector/documents/{documentId}/evidence-bundle` |
| `connector->customers->for($ref)->advanced->documents->supportPacket($id)` | GET | `/connector/documents/{documentId}/support-packet` |

The remaining Connector rows are legacy compatibility helpers. They require a
separately entitled legacy/Enterprise context and are not available to managed
Connector credentials. Customer Mapper must be called through
`$customer->advanced->mapper(...)` and is preview-only.

| Legacy compatibility method | Verb | Path |
| --- | --- | --- |
| `connector->advanced->preflight($body)`                      | POST   | `/connector/preflight`               |
| `connector->advanced->send($body, $idempotencyKey)`          | POST   | `/connector/send`                    |
| `connector->advanced->outbox->stage($body)`                  | POST   | `/connector/outbox`                  |
| `connector->advanced->outbox->list($params)`                 | GET    | `/connector/outbox`                  |
| `connector->advanced->outbox->get($outboxId)`                | GET    | `/connector/outbox/{outboxId}`       |
| `connector->advanced->outbox->send($outboxId, $force)`       | POST   | `/connector/outbox/{outboxId}/send`  |
| `connector->advanced->outbox->sendBatch($body)`              | POST   | `/connector/outbox/send`             |
| `connector->advanced->outbox->cancel($outboxId)`             | DELETE | `/connector/outbox/{outboxId}`       |
| `connector->advanced->mapper($body)`                         | POST   | `/connector/mapper`                  |
| `connector->advanced->zenInput($body)`                       | POST   | `/connector/zen-input`               |
| `connector->advanced->autopilot($body)`                      | POST   | `/connector/autopilot`               |
| `connector->advanced->getAutopilotRun($autopilotId)`         | GET    | `/connector/autopilot/{autopilotId}` |
| `connector->advanced->sendAutopilotRun($autopilotId)`        | POST   | `/connector/autopilot/{autopilotId}/send` |
| `connector->advanced->reconcile($params)`                    | GET    | `/connector/reconcile`               |
| `connector->advanced->mailboxes()`                           | GET    | `/connector/mailbox`                 |
| `connector->advanced->repairMailbox($body)`                  | POST   | `/connector/mailbox/repair`          |
| `connector->advanced->updateMailboxSendPolicy($customerRef, $body)` | PATCH | `/connector/mailbox/{customerRef}/send-policy` |
| `connector->advanced->sync($params)`                         | GET    | `/connector/sync`                    |
| `connector->getDocument($documentId)`                         | GET    | `/connector/documents/{documentId}`  |
| `connector->getDocumentUbl($documentId)`                      | GET    | `/connector/documents/{documentId}/ubl` |
| `connector->getDocumentEvidence($documentId)`                 | GET    | `/connector/documents/{documentId}/evidence` |
| `connector->getDocumentEvidenceBundle($documentId)`           | GET    | `/connector/documents/{documentId}/evidence-bundle` |
| `connector->getDocumentSupportPacket($documentId)`            | GET    | `/connector/documents/{documentId}/support-packet` |
| `connector->documents->supportPacket($documentId)`            | GET    | `/connector/documents/{documentId}/support-packet` |
| `connector->advanced->runAction($actionId, $body)`           | POST   | `/connector/actions/{actionId}`      |
| `connector->advanced->status($documentId)`                   | GET    | `/connector/status/{documentId}`     |
| `connector->advanced->inbox($params)`                        | GET    | `/connector/inbox`                   |
| `connector->advanced->getInboxDocument($documentId)`         | GET    | `/connector/inbox/{documentId}`      |
| `connector->advanced->ack($documentId)`                      | POST   | `/connector/inbox/{documentId}/ack`  |
| `connector->advanced->events($params)`                       | GET    | `/connector/events`                  |
| `sapi->participants->for($id)->documents->send($body, $key)` | POST | `/sapi/v1/document/send` |
| `sapi->participants->for($id)->documents->receive($params)` | GET | `/sapi/v1/document/receive` |
| `sapi->participants->for($id)->documents->get($documentId)` | GET | `/sapi/v1/document/receive/{id}` |
| `sapi->participants->for($id)->documents->acknowledge($documentId)` | POST | `/sapi/v1/document/receive/{id}/acknowledge` |

Production Enterprise paths are relative to `https://epostak.sk/api/v1`; test Enterprise paths use `https://dev.epostak.sk/api/v1`. SAPI uses the same host, for example `https://epostak.sk/sapi/v1` or `https://dev.epostak.sk/sapi/v1`.

---

## Recent Changes

### 0.9.0 — 2026-05-12

- **Pull API** — `$client->enterprise->pull->inbound` and `$client->enterprise->pull->outbound` resources for
  the new cursor-paginated Pull API endpoints (`/inbound/documents` and
  `/outbound/documents`). Includes `ack()` with `client_reference` support
  and `outbound->events()` for the cross-document event stream.
- **`UblValidationException`** — dedicated exception for HTTP 422 +
  `UBL_VALIDATION_ERROR`. Exposes `$rule` (compare against `UblRule::*`
  constants) and `$requestId`.
- **`$client->getLastRateLimit()`** — returns a `RateLimit` DTO
  (`limit`, `remaining`, `resetAt`) populated from `X-RateLimit-*` headers.
- **`webhooks->test()`** — now accepts `['event' => '...']` array and
  forwards the event type as a query parameter (server priority per PR #114).

## Connector webhook debugger

Inspect the exact signed body and attempt timeline, then use idempotent replay
after fixing the receiver. `runTestSuite` exercises all receiver scenarios.

```php
$failed = $connectorClient->connector->webhook->listDeliveries(['status' => 'FAILED']);
$detail = $connectorClient->connector->webhook->getDelivery($failed['deliveries'][0]['id']);
$connectorClient->connector->webhook->replayDelivery($detail['delivery']['id'], 'erp:replay:1');
$connectorClient->connector->webhook->runTestSuite('erp-acme', 'erp:suite:1');
```

---

## Full API Documentation

https://epostak.sk/api/docs/enterprise
