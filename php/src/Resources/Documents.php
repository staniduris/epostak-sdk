<?php

declare(strict_types=1);

namespace EPostak\Resources;

use EPostak\HttpClient;
use EPostak\EPostakError;

/**
 * Send, receive, and manage Peppol e-invoices and other documents.
 *
 * Access via `$client->documents`. Received documents are available
 * through the nested `$client->documents->inbox` resource.
 */
class Documents
{
    /** @var Inbox Received document operations (list, get, acknowledge). */
    public Inbox $inbox;

    /**
     * @param HttpClient $http Shared HTTP transport instance.
     */
    public function __construct(private HttpClient $http)
    {
        $this->inbox = new Inbox($http);
    }

    /**
     * Get a document by ID.
     *
     * @param string $id Document UUID.
     * @return array Document object with metadata, status, and line items.
     * @throws EPostakError On API error.
     */
    public function get(string $id): array
    {
        return $this->http->request('GET', '/documents/' . urlencode($id));
    }

    /**
     * Update a draft document.
     *
     * @param string $id   Document UUID.
     * @param array  $data Fields to update (partial patch).
     * @return array Updated document object.
     * @throws EPostakError On API error.
     */
    public function update(string $id, array $data): array
    {
        return $this->http->request('PATCH', '/documents/' . urlencode($id), [
            'json' => $data,
        ]);
    }

    /**
     * Send a document via Peppol.
     *
     * Document types (`docType`): `invoice`, `credit_note`, `correction`,
     * `self_billing`, `reverse_charge`, `self_billing_credit_note`. Defaults
     * to `invoice` when omitted.
     *
     * **Supplier-party pinning (XML mode).** When submitting raw UBL via
     * the `xml` field, the server pins the seller identity (Name, IČO,
     * IČ DPH, Postal Address, Legal Entity name) to the authenticated firm.
     * Caller-supplied values in `cac:AccountingSupplierParty/cac:Party` are
     * silently overwritten before forwarding to Peppol. The Peppol
     * `EndpointID` is the only supplier-party field still validated against
     * the firm's registered Peppol ID; mismatched EndpointIDs are rejected
     * with HTTP 422. BG-24 attachments, line items, payment terms, and
     * custom note fields are preserved as-is. For self-billing document
     * types (UBL typecodes 261/389), the customer party (which is the
     * authenticated firm) is rewritten instead.
     *
     * @param array       $body           Document payload including supplier, customer, line items, and Peppol IDs.
     * @param string|null $idempotencyKey Optional `Idempotency-Key` header value.
     *                                    Replaying the same key while the original
     *                                    request is still in flight returns 409
     *                                    `idempotency_conflict` (surfaced as
     *                                    EPostakError).
     * @return array{documentId: string, messageId: string, status: string, payloadSha256?: string} Send confirmation.
     *         Includes `payloadSha256` (hex SHA-256 over the canonical UBL XML wire
     *         payload) on `201` send responses.
     * @throws EPostakError On validation or delivery error.
     *
     * @example
     *   $result = $client->documents->send([
     *       'type' => 'invoice',
     *       'invoiceNumber' => 'INV-2026-001',
     *       'issueDate' => '2026-04-11',
     *       'supplier' => ['peppolId' => '0192:12345678'],
     *       'customer' => ['peppolId' => '0192:87654321'],
     *       'lines' => [
     *           ['description' => 'Consulting', 'quantity' => 1, 'unitPrice' => 100.00],
     *       ],
     *   ]);
     *   echo $result['documentId'];
     *
     * @example JSON mode with attachments (BG-24). Embedded into the generated
     *          UBL XML as base64. Allowed MIME: application/pdf, image/png,
     *          image/jpeg, text/csv, xlsx, ods. Max 20 files, 10 MB each, 15 MB total.
     *   $result = $client->documents->send([
     *       'receiverPeppolId' => '0245:12345678',
     *       'items' => [['description' => 'Consulting', 'quantity' => 10, 'unitPrice' => 50, 'vatRate' => 23]],
     *       'attachments' => [[
     *           'fileName'    => 'invoice-detail.pdf',
     *           'mimeType'    => 'application/pdf',
     *           'content'     => base64_encode(file_get_contents('invoice-detail.pdf')),
     *           'description' => 'Timesheet breakdown',
     *       ]],
     *   ]);
     */
    public function send(array $body, ?string $idempotencyKey = null): array
    {
        $options = ['json' => $body];
        if ($idempotencyKey !== null) {
            $options['headers'] = ['Idempotency-Key' => $idempotencyKey];
        }
        return $this->http->request('POST', '/documents/send', $options);
    }

    /**
     * Get full document status with delivery history.
     *
     * @param string $id Document UUID.
     * @return array Status object including current state and timestamped history entries.
     * @throws EPostakError On API error.
     */
    public function status(string $id): array
    {
        return $this->http->request('GET', '/documents/' . urlencode($id) . '/status');
    }

    /**
     * Get delivery evidence for a document.
     *
     * @param string $id Document UUID.
     * @return array Delivery evidence object (AS4 receipt, timestamps).
     * @throws EPostakError On API error.
     */
    public function evidence(string $id): array
    {
        return $this->http->request('GET', '/documents/' . urlencode($id) . '/evidence');
    }

    /**
     * Download the document as a PDF.
     *
     * @param string $id Document UUID.
     * @return string Raw PDF binary content. Write to a file with file_put_contents().
     * @throws EPostakError On API error.
     *
     * @example
     *   $pdf = $client->documents->pdf('doc_abc123');
     *   file_put_contents('/tmp/invoice.pdf', $pdf);
     */
    public function pdf(string $id): string
    {
        return $this->http->requestRaw('GET', '/documents/' . urlencode($id) . '/pdf');
    }

    /**
     * Download the document as UBL XML.
     *
     * @param string $id Document UUID.
     * @return string UBL 2.1 XML string.
     * @throws EPostakError On API error.
     */
    public function ubl(string $id): string
    {
        return $this->http->requestRaw('GET', '/documents/' . urlencode($id) . '/ubl');
    }

    /**
     * Download the signed AS4 envelope for a document from the 10-year WORM archive.
     *
     * Streams the raw multipart AS4 payload as transmitted on the Peppol network —
     * signed, timestamped and tamper-evident. Useful for compliance audits and
     * third-party dispute resolution where the original transport wrapper is
     * required rather than just the UBL payload.
     *
     * Returns 404 if the envelope has not yet been archived (the archive cron
     * runs on a short interval, so brand-new documents may briefly miss).
     *
     * @param string $id Document UUID.
     * @return string Raw AS4 envelope bytes. Write to a file with file_put_contents().
     * @throws EPostakError On API error (404 if envelope not yet archived).
     *
     * @example
     *   $envelope = $client->documents->envelope('doc_abc123');
     *   file_put_contents('/tmp/doc_abc123.as4', $envelope);
     */
    public function envelope(string $id): string
    {
        return $this->http->requestRaw('GET', '/documents/' . urlencode($id) . '/envelope');
    }

    public function evidenceBundle(string $id): string
    {
        return $this->http->requestRaw('GET', '/documents/' . urlencode($id) . '/evidence-bundle');
    }

    /**
     * Send an invoice response (BIS Invoice Response per Peppol BIS 3.0).
     *
     * Allowed status codes (UNCL 4343 subset):
     *   - 'AB' — accepted billing
     *   - 'IP' — in process
     *   - 'UQ' — under query
     *   - 'CA' — conditionally accepted
     *   - 'RE' — rejected
     *   - 'AP' — accepted
     *   - 'PD' — paid
     *
     * Once a response other than 'UQ' has been submitted, the invoice
     * response is final and this endpoint returns 400.
     *
     * @param string      $id     Document UUID of the received invoice.
     * @param string      $status Response code: one of AB, IP, UQ, CA, RE, AP, PD.
     * @param string|null $note   Optional free-text note (max 500 characters).
     * @return array Response confirmation.
     * @throws EPostakError On API error.
     */
    public function respond(string $id, string $status, ?string $note = null): array
    {
        $body = ['status' => $status];
        if ($note !== null) {
            $body['note'] = $note;
        }
        return $this->http->request('POST', '/documents/' . urlencode($id) . '/respond', [
            'json' => $body,
        ]);
    }

    /**
     * Validate a document payload without sending it.
     *
     * Returns validation results including any UBL or business-rule errors.
     * Requires `documents:read` scope.
     *
     * @param array $body Document payload (same structure as send()).
     * @return array Validation result with `valid` boolean and `errors` array.
     * @throws EPostakError On API error.
     */
    public function validate(array $body): array
    {
        return $this->http->request('POST', '/documents/validate', [
            'json' => $body,
        ]);
    }

    /**
     * Check receiver capability before sending.
     *
     * Verifies that the receiver Peppol ID is registered and can accept
     * the given document type.
     *
     * @param string      $receiverPeppolId Receiver's Peppol participant ID (e.g. '0192:12345678').
     * @param string|null $documentTypeId   Optional Peppol document type identifier to check.
     * @return array Preflight result with `canReceive` boolean and supported document types.
     * @throws EPostakError On API error.
     */
    public function preflight(string $receiverPeppolId, ?string $documentTypeId = null): array
    {
        $body = ['receiverPeppolId' => $receiverPeppolId];
        if ($documentTypeId !== null) {
            $body['documentTypeId'] = $documentTypeId;
        }
        return $this->http->request('POST', '/documents/preflight', [
            'json' => $body,
        ]);
    }

    /**
     * Convert between JSON and UBL XML formats.
     *
     * Requires `documents:read` scope.
     *
     * @param string       $inputFormat  Source format: 'json' or 'ubl'.
     * @param string       $outputFormat Target format: 'ubl' or 'json'.
     * @param array|string $document     Document to convert — array for JSON input, XML string for UBL input.
     * @return array { output_format: string, document: array|string, warnings: string[] } Conversion result.
     * @throws EPostakError On API or conversion error.
     *
     * @example
     *   // JSON to UBL
     *   $result = $client->documents->convert('json', 'ubl', ['invoiceNumber' => 'FV-001', 'items' => [...]]);
     *   echo $result['document']; // UBL XML string
     *
     *   // UBL to JSON
     *   $result = $client->documents->convert('ubl', 'json', '<Invoice xmlns="...">...</Invoice>');
     *   print_r($result['document']); // associative array
     */
    public function convert(string $inputFormat, string $outputFormat, array|string $document): array
    {
        return $this->http->request('POST', '/documents/convert', [
            'json' => [
                'input_format' => $inputFormat,
                'output_format' => $outputFormat,
                'document' => $document,
            ],
        ]);
    }

    /**
     * Send up to 100 documents in a single request.
     *
     * Each item uses the same body format as {@see send()} and may carry
     * an optional `idempotencyKey` for safe retries. Partial failures do
     * not fail the whole request -- inspect `results` and `failed` per item.
     *
     * @param array $items Array of send request bodies (same shape as send()),
     *                     each optionally containing an `idempotencyKey`.
     * @return array{
     *   total: int,
     *   succeeded: int,
     *   failed: int,
     *   results: list<array{index: int, status: string, result: array}>
     * } Batch result with per-item status.
     * @throws EPostakError On API error.
     *
     * @example
     *   $batch = $client->documents->sendBatch([
     *       [
     *           'receiverPeppolId' => '0245:1234567890',
     *           'invoiceNumber' => 'FV-2026-010',
     *           'items' => [['description' => 'Audit', 'quantity' => 1, 'unitPrice' => 500, 'vatRate' => 23]],
     *           'idempotencyKey' => 'batch-2026-04-22-001',
     *       ],
     *       [
     *           'receiverPeppolId' => '0245:0987654321',
     *           'invoiceNumber' => 'FV-2026-011',
     *           'items' => [['description' => 'Consulting', 'quantity' => 2, 'unitPrice' => 300, 'vatRate' => 23]],
     *       ],
     *   ]);
     *   echo $batch['succeeded'], '/', $batch['total'];
     */
    public function sendBatch(array $items, ?string $idempotencyKey = null): array
    {
        $options = ['json' => ['items' => $items]];
        if ($idempotencyKey !== null) {
            $options['headers'] = ['Idempotency-Key' => $idempotencyKey];
        }
        return $this->http->request('POST', '/documents/send/batch', $options);
    }

    /**
     * Parse a UBL XML invoice into a structured JSON representation.
     *
     * Requires `documents:read` scope.
     *
     * Streams the XML as the raw request body with `Content-Type: application/xml`.
     *
     * @param string $xml UBL 2.1 XML invoice or credit note.
     * @return array Parsed invoice with supplier, customer, lines, totals, etc.
     * @throws EPostakError On API error.
     *
     * @example
     *   $parsed = $client->documents->parse(file_get_contents('invoice.xml'));
     *   echo $parsed['invoiceNumber'];
     */
    public function parse(string $xml): array
    {
        return $this->http->request('POST', '/documents/parse', [
            'headers' => ['Content-Type' => 'application/xml'],
            'body' => $xml,
        ]);
    }

    /**
     * List sent documents in the outbox with optional filtering and pagination.
     *
     * @param array{offset?: int, limit?: int, status?: string, peppolMessageId?: string, since?: string} $params Optional filters.
     * @return array Paginated list of outbox documents.
     * @throws EPostakError On API error.
     */
    public function outbox(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'offset' => $params['offset'] ?? null,
            'limit' => $params['limit'] ?? null,
            'status' => $params['status'] ?? null,
            'peppolMessageId' => $params['peppolMessageId'] ?? null,
            'since' => $params['since'] ?? null,
        ]);
        return $this->http->request('GET', '/documents/outbox' . $qs);
    }

    public function peppolDocuments(array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'offset' => $params['offset'] ?? null,
            'limit' => $params['limit'] ?? null,
            'direction' => $params['direction'] ?? null,
            'doctypeKey' => $params['doctypeKey'] ?? null,
            'status' => $params['status'] ?? null,
            'peppolMessageId' => $params['peppolMessageId'] ?? null,
            'since' => $params['since'] ?? null,
        ]);
        return $this->http->request('GET', '/peppol-documents' . $qs);
    }

    /**
     * List all Invoice Response documents received for a sent document.
     *
     * @param string $id Document UUID.
     * @return array List of invoice responses.
     * @throws EPostakError On API error.
     */
    public function responses(string $id): array
    {
        return $this->http->request('GET', '/documents/' . urlencode($id) . '/responses');
    }

    /**
     * List lifecycle events for a document with cursor-based pagination.
     *
     * @param string $id     Document UUID.
     * @param array{limit?: int, cursor?: string} $params Optional pagination params.
     * @return array Event list with optional next cursor.
     * @throws EPostakError On API error.
     */
    public function events(string $id, array $params = []): array
    {
        $qs = HttpClient::buildQuery([
            'limit' => $params['limit'] ?? null,
            'cursor' => $params['cursor'] ?? null,
        ]);
        return $this->http->request('GET', '/documents/' . urlencode($id) . '/events' . $qs);
    }

    /**
     * Manually mark a document's lifecycle state.
     *
     * Use for documents delivered out-of-band (e.g. a receiver confirms
     * over email) or to flag a failed/processed document in your own
     * workflow.
     *
     * @param string      $id    Document UUID.
     * @param string      $state One of 'delivered', 'processed', 'failed', 'read'.
     * @param string|null $note  Optional free-text note attached to the state change.
     * @return array{
     *   id: string,
     *   state: string,
     *   status: string,
     *   deliveredAt: ?string,
     *   acknowledgedAt: ?string,
     *   readAt: ?string
     * } Mark result.
     * @throws EPostakError On API error.
     *
     * @example
     *   $r = $client->documents->mark('doc-uuid', 'delivered', 'Confirmed by email');
     *   echo $r['deliveredAt'];
     */
    public function mark(string $id, string $state, ?string $note = null): array
    {
        $body = ['state' => $state];
        if ($note !== null) {
            $body['note'] = $note;
        }
        return $this->http->request('POST', '/documents/' . urlencode($id) . '/mark', [
            'json' => $body,
        ]);
    }
}
