# epostak/sdk

Official PHP SDK for the [ePostak Enterprise API](https://epostak.sk/api/docs/enterprise) -- Peppol e-invoicing for Slovakia and the EU.

Requires PHP 8.1+ and Guzzle 7.

---

## Installation

```bash
composer require epostak/sdk
```

---

## Quick Start

```php
use EPostak\EPostak;

$client = new EPostak(['apiKey' => 'sk_live_xxxxx']);

// Send an invoice
$result = $client->documents->send([
    'receiverPeppolId' => '0245:1234567890',
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

---

## Authentication

| Key prefix  | Use case                                            |
| ----------- | --------------------------------------------------- |
| `sk_live_*` | Direct access -- acts on behalf of your own firm    |
| `sk_int_*`  | Integrator access -- acts on behalf of client firms |

Generate keys in your ePostak firm settings or via the dashboard.

### Constructor options

```php
$client = new EPostak([
    'apiKey' => 'sk_live_xxxxx',           // Required
    'baseUrl' => 'https://...',            // Optional, defaults to https://epostak.sk/api/enterprise
    'firmId' => 'uuid',                    // Optional, required for integrator keys
]);
```

---

## API Reference

### Documents

#### `documents->send($body)` -- Send a document via Peppol

**JSON mode** -- structured data, UBL XML is auto-generated:

```php
$result = $client->documents->send([
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
$result = $client->documents->send([
    'receiverPeppolId' => '0245:1234567890',
    'xml' => '<?xml version="1.0" encoding="UTF-8"?>...',
]);
```

#### `documents->get($id)` -- Get a document by ID

```php
$doc = $client->documents->get('doc-uuid');
// => ['id', 'number', 'status', 'direction', 'docType', 'issueDate', ...]
```

#### `documents->update($id, $data)` -- Update a draft document

Only documents with status `draft` can be updated. All fields are optional.

```php
$updated = $client->documents->update('doc-uuid', [
    'invoiceNumber' => 'FV-2026-002',
    'dueDate' => '2026-05-01',
    'items' => [['description' => 'Development', 'quantity' => 20, 'unitPrice' => 75, 'vatRate' => 23]],
]);
```

#### `documents->status($id)` -- Full status with history

```php
$status = $client->documents->status('doc-uuid');
// => ['id', 'status', 'statusHistory' => [['status', 'timestamp', 'detail']], ...]
```

#### `documents->evidence($id)` -- Delivery evidence

```php
$evidence = $client->documents->evidence('doc-uuid');
// => ['documentId', 'as4Receipt', 'mlrDocument', 'invoiceResponse', 'deliveredAt', 'sentAt']
```

#### `documents->pdf($id)` -- Download PDF as raw bytes

```php
$pdf = $client->documents->pdf('doc-uuid');
file_put_contents('invoice.pdf', $pdf);
```

#### `documents->ubl($id)` -- Download UBL XML as string

```php
$ubl = $client->documents->ubl('doc-uuid');
```

#### `documents->respond($id, $status, $note)` -- Send invoice response

```php
$response = $client->documents->respond('doc-uuid', 'AP', 'Invoice accepted');
// $status: 'AP' = accepted, 'RE' = rejected, 'UQ' = under query
// => ['documentId', 'responseStatus', 'respondedAt']
```

#### `documents->validate($body)` -- Validate without sending

```php
$validation = $client->documents->validate([
    'receiverPeppolId' => '0245:1234567890',
    'items' => [['description' => 'Test', 'quantity' => 1, 'unitPrice' => 100, 'vatRate' => 23]],
]);
// => ['valid' => true/false, 'warnings' => [...], 'ubl' => '...' or null]
```

#### `documents->preflight($receiverPeppolId, $documentTypeId)` -- Check receiver capability

```php
$check = $client->documents->preflight('0245:1234567890');
// => ['receiverPeppolId', 'registered', 'supportsDocumentType', 'smpUrl']
```

#### `documents->convert($direction, $data, $xml)` -- Convert between JSON and UBL

```php
// JSON to UBL
$result = $client->documents->convert('json_to_ubl', ['invoiceNumber' => 'FV-001', 'items' => [...]]);

// UBL to JSON
$result = $client->documents->convert('ubl_to_json', null, '<Invoice xmlns="...">...</Invoice>');
```

---

### Inbox

Access via `$client->documents->inbox`.

#### `documents->inbox->list($params)` -- List received documents

```php
$inbox = $client->documents->inbox->list([
    'limit' => 20,            // 1-100, default 20
    'offset' => 0,
    'status' => 'RECEIVED',   // 'RECEIVED' | 'ACKNOWLEDGED'
    'since' => '2026-04-01T00:00:00Z',
]);
// => ['documents' => [...], 'total', 'limit', 'offset']
```

#### `documents->inbox->get($id)` -- Full detail with UBL XML

```php
$detail = $client->documents->inbox->get('doc-uuid');
// $detail['document'] -- InboxDocument
// $detail['payload']  -- UBL XML string or null
```

#### `documents->inbox->acknowledge($id)` -- Mark as processed

```php
$ack = $client->documents->inbox->acknowledge('doc-uuid');
// => ['documentId', 'status' => 'ACKNOWLEDGED', 'acknowledgedAt']
```

#### `documents->inbox->listAll($params)` -- Cross-firm inbox (integrator)

```php
$all = $client->documents->inbox->listAll([
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
$participant = $client->peppol->lookup('0245', '12345678');
// => ['peppolId', 'name', 'country', 'capabilities' => [...]]
```

#### `peppol->directory->search($params)` -- Business Card directory

```php
$results = $client->peppol->directory->search([
    'q' => 'Telekom',
    'country' => 'SK',
    'page' => 0,
    'page_size' => 20,
]);
// => ['results' => [...], 'total', 'page', 'page_size']
```

#### `peppol->companyLookup($ico)` -- Slovak company lookup

```php
$company = $client->peppol->companyLookup('12345678');
// => ['ico', 'name', 'dic', 'icDph', 'address', 'peppolId']
```

---

### Firms (integrator keys)

#### `firms->list()` -- List all accessible firms

```php
$firms = $client->firms->list();
// => ['firms' => [['id', 'name', 'ico', 'peppolId', 'peppolStatus'], ...]]
```

#### `firms->get($id)` -- Firm detail

```php
$firm = $client->firms->get('firm-uuid');
// => ['id', 'name', 'ico', 'peppolId', 'peppolStatus', 'dic', 'icDph', 'address', ...]
```

#### `firms->documents($id, $params)` -- List firm documents

```php
$docs = $client->firms->documents('firm-uuid', [
    'limit' => 20,
    'direction' => 'inbound',  // 'inbound' | 'outbound'
]);
```

#### `firms->registerPeppolId($id, $scheme, $identifier)` -- Register Peppol ID

```php
$result = $client->firms->registerPeppolId('firm-uuid', '0245', '12345678');
// => ['peppolId', 'scheme', 'identifier', 'registeredAt']
```

#### `firms->assign($ico)` -- Assign firm to integrator

```php
$result = $client->firms->assign('12345678');
// => ['firm' => ['id', 'name', 'ico', ...], 'status' => 'active']
```

#### `firms->assignBatch($icos)` -- Batch assign firms (max 50)

```php
$result = $client->firms->assignBatch(['12345678', '87654321', '11223344']);
// => ['results' => [['ico', 'firm' => ..., 'status' => ..., 'error' => ...], ...]]
```

---

### Webhooks

#### `webhooks->create($url, $events)` -- Register a webhook

```php
$webhook = $client->webhooks->create(
    'https://example.com/webhook',
    ['document.received', 'document.sent']
);
// Store $webhook['secret'] for HMAC-SHA256 signature verification
```

#### `webhooks->list()` -- List webhooks

```php
$webhooks = $client->webhooks->list();
```

#### `webhooks->get($id)` -- Webhook detail with deliveries

```php
$detail = $client->webhooks->get('webhook-uuid');
// => [...webhook, 'deliveries' => [...]]
```

#### `webhooks->update($id, $data)` -- Update webhook

```php
$client->webhooks->update('webhook-uuid', [
    'url' => 'https://example.com/new-webhook',
    'events' => ['document.received'],
    'isActive' => true,
]);
```

#### `webhooks->delete($id)` -- Delete webhook

```php
$client->webhooks->delete('webhook-uuid');
```

---

### Webhook Pull Queue

Alternative to push webhooks -- poll for events. Access via `$client->webhooks->queue`.

#### `webhooks->queue->pull($params)` -- Fetch pending events

```php
$queue = $client->webhooks->queue->pull([
    'limit' => 50,                          // 1-100, default 20
    'event_type' => 'document.received',    // Optional filter
]);

foreach ($queue['items'] as $item) {
    echo $item['id'] . ' ' . $item['type'] . "\n";
    // Process $item['payload']
}
// $queue['has_more'] -- bool
```

#### `webhooks->queue->ack($eventId)` -- Acknowledge single event

```php
$client->webhooks->queue->ack('event-uuid');
// Returns null (HTTP 204)
```

#### `webhooks->queue->batchAck($eventIds)` -- Batch acknowledge

```php
$ids = array_column($queue['items'], 'id');
$client->webhooks->queue->batchAck($ids);
// Returns null (HTTP 204)
```

#### `webhooks->queue->pullAll($params)` -- Cross-firm queue (integrator)

```php
$queue = $client->webhooks->queue->pullAll([
    'limit' => 200,   // 1-500, default 100
    'since' => '2026-04-01T00:00:00Z',
]);

foreach ($queue['events'] as $event) {
    echo $event['firm_id'] . ' ' . $event['event'] . "\n";
}
// => ['events' => [...], 'count' => ...]
```

#### `webhooks->queue->batchAckAll($eventIds)` -- Cross-firm batch ack (integrator)

```php
$ids = array_column($queue['events'], 'event_id');
$result = $client->webhooks->queue->batchAckAll($ids);
echo $result['acknowledged'];  // number of events acknowledged
```

---

### Reporting

#### `reporting->statistics($params)` -- Aggregated stats

```php
$stats = $client->reporting->statistics([
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
$account = $client->account->get();
// => ['firm' => ['name', 'ico', 'peppolId', 'peppolStatus'],
//     'plan' => ['name', 'status'],
//     'usage' => ['outbound', 'inbound']]
```

---

### Extract (AI OCR)

Requires Enterprise plan.

#### `extract->single($filePath, $mimeType, $fileName)` -- Single file

```php
$result = $client->extract->single(
    '/path/to/invoice.pdf',
    'application/pdf',
    'invoice.pdf'    // Optional, defaults to basename
);
// => ['extraction' => [...], 'ubl_xml' => '...', 'confidence' => 0.95, 'file_name' => '...']
```

Supported MIME types: `application/pdf`, `image/jpeg`, `image/png`, `image/webp`. Max 20 MB.

#### `extract->batch($files)` -- Batch extraction (up to 10 files)

```php
$result = $client->extract->batch([
    ['filePath' => '/path/to/inv1.pdf', 'mimeType' => 'application/pdf', 'fileName' => 'inv1.pdf'],
    ['filePath' => '/path/to/inv2.png', 'mimeType' => 'image/png'],
]);
// => ['batch_id', 'total', 'successful', 'failed',
//     'results' => [['file_name', 'extraction', 'ubl_xml', 'confidence', 'error'], ...]]
```

---

## Integrator Mode

Use `sk_int_*` keys to act on behalf of client firms. Integrator keys unlock multi-tenant endpoints.

```php
// Option 1: pass firmId in constructor
$client = new EPostak(['apiKey' => 'sk_int_xxxxx', 'firmId' => 'client-firm-uuid']);

// Option 2: scope at call time with withFirm()
$base = new EPostak(['apiKey' => 'sk_int_xxxxx']);
$clientA = $base->withFirm('firm-uuid-a');
$clientB = $base->withFirm('firm-uuid-b');

$clientA->documents->send([...]);
$clientB->documents->inbox->list();
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
    $client->documents->send([...]);
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

| SDK Method                                    | HTTP   | Path                                 |
| --------------------------------------------- | ------ | ------------------------------------ |
| `documents->get($id)`                         | GET    | `/documents/{id}`                    |
| `documents->update($id, $data)`               | PATCH  | `/documents/{id}`                    |
| `documents->send($body)`                      | POST   | `/documents/send`                    |
| `documents->status($id)`                      | GET    | `/documents/{id}/status`             |
| `documents->evidence($id)`                    | GET    | `/documents/{id}/evidence`           |
| `documents->pdf($id)`                         | GET    | `/documents/{id}/pdf`                |
| `documents->ubl($id)`                         | GET    | `/documents/{id}/ubl`                |
| `documents->respond($id, $status, $note)`     | POST   | `/documents/{id}/respond`            |
| `documents->validate($body)`                  | POST   | `/documents/validate`                |
| `documents->preflight($receiverPeppolId)`     | POST   | `/documents/preflight`               |
| `documents->convert($direction, $data, $xml)` | POST   | `/documents/convert`                 |
| `documents->inbox->list($params)`             | GET    | `/documents/inbox`                   |
| `documents->inbox->get($id)`                  | GET    | `/documents/inbox/{id}`              |
| `documents->inbox->acknowledge($id)`          | POST   | `/documents/inbox/{id}/acknowledge`  |
| `documents->inbox->listAll($params)`          | GET    | `/documents/inbox/all`               |
| `peppol->lookup($scheme, $id)`                | GET    | `/peppol/participants/{scheme}/{id}` |
| `peppol->directory->search($params)`          | GET    | `/peppol/directory/search`           |
| `peppol->companyLookup($ico)`                 | GET    | `/company/lookup/{ico}`              |
| `firms->list()`                               | GET    | `/firms`                             |
| `firms->get($id)`                             | GET    | `/firms/{id}`                        |
| `firms->documents($id, $params)`              | GET    | `/firms/{id}/documents`              |
| `firms->registerPeppolId($id, $scheme, $id)`  | POST   | `/firms/{id}/peppol-identifiers`     |
| `firms->assign($ico)`                         | POST   | `/firms/assign`                      |
| `firms->assignBatch($icos)`                   | POST   | `/firms/assign/batch`                |
| `webhooks->create($url, $events)`             | POST   | `/webhooks`                          |
| `webhooks->list()`                            | GET    | `/webhooks`                          |
| `webhooks->get($id)`                          | GET    | `/webhooks/{id}`                     |
| `webhooks->update($id, $data)`                | PATCH  | `/webhooks/{id}`                     |
| `webhooks->delete($id)`                       | DELETE | `/webhooks/{id}`                     |
| `webhooks->queue->pull($params)`              | GET    | `/webhook-queue`                     |
| `webhooks->queue->ack($eventId)`              | DELETE | `/webhook-queue/{eventId}`           |
| `webhooks->queue->batchAck($ids)`             | POST   | `/webhook-queue/batch-ack`           |
| `webhooks->queue->pullAll($params)`           | GET    | `/webhook-queue/all`                 |
| `webhooks->queue->batchAckAll($ids)`          | POST   | `/webhook-queue/all/batch-ack`       |
| `reporting->statistics($params)`              | GET    | `/reporting/statistics`              |
| `account->get()`                              | GET    | `/account`                           |
| `extract->single($path, $mime)`               | POST   | `/extract`                           |
| `extract->batch($files)`                      | POST   | `/extract/batch`                     |

All paths are relative to `https://epostak.sk/api/enterprise`.

---

## Full API Documentation

https://epostak.sk/api/docs/enterprise
