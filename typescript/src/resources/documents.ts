import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  SendDocumentRequest,
  SendDocumentResponse,
  UpdateDocumentRequest,
  InboxDocument,
  InboxListParams,
  InboxListResponse,
  InboxDocumentDetailResponse,
  AcknowledgeResponse,
  InboxAllParams,
  InboxAllResponse,
  DocumentStatusResponse,
  DocumentEvidenceResponse,
  InvoiceRespondRequest,
  InvoiceRespondResponse,
  ValidateDocumentRequest,
  ValidationResult,
  PreflightRequest,
  PreflightResult,
  ConvertRequest,
  ConvertResult,
  BatchSendItem,
  BatchSendResponse,
  ParsedUblDocument,
  DocumentMarkState,
  MarkDocumentResponse,
  OutboxParams,
  OutboxListResponse,
  InvoiceResponsesListResponse,
  DocumentEventsParams,
  DocumentEventsResponse,
} from "../types.js";

/**
 * Resource for managing received (inbound) documents in your inbox.
 * Provides methods to list, retrieve, and acknowledge incoming invoices.
 */
export class InboxResource extends BaseResource {
  /**
   * List documents in your inbox with optional filtering and pagination.
   *
   * @param params - Optional query parameters for filtering and pagination
   * @returns Paginated list of inbox documents
   *
   * @example
   * ```typescript
   * // Get unprocessed documents
   * const { documents, total } = await client.documents.inbox.list({
   *   status: 'received',
   *   limit: 50,
   * });
   * ```
   */
  list(params?: InboxListParams): Promise<InboxListResponse> {
    return this.request(
      "GET",
      `/documents/inbox${buildQuery({
        offset: params?.offset,
        limit: params?.limit,
        status: params?.status,
        since: params?.since,
      })}`,
    );
  }

  /**
   * Retrieve a single inbox document by ID, including the raw UBL XML payload.
   *
   * @param id - Document UUID
   * @returns Document details with the UBL XML payload
   *
   * @example
   * ```typescript
   * const { document, payload } = await client.documents.inbox.get('doc-uuid');
   * console.log(payload); // UBL XML string
   * ```
   */
  get(id: string): Promise<InboxDocumentDetailResponse> {
    return this.request("GET", `/documents/inbox/${encodeURIComponent(id)}`);
  }

  /**
   * Acknowledge (mark as processed) a received inbox document.
   * Requires the document to be in `"received"` state — already-acknowledged
   * documents return 422.
   *
   * @param id - Document UUID to acknowledge
   * @returns Acknowledgment confirmation with timestamp
   *
   * @example
   * ```typescript
   * const ack = await client.documents.inbox.acknowledge('doc-uuid');
   * console.log(ack.acknowledgedAt); // "2026-04-11T12:00:00Z"
   * ```
   */
  acknowledge(id: string): Promise<AcknowledgeResponse> {
    return this.request(
      "POST",
      `/documents/inbox/${encodeURIComponent(id)}/acknowledge`,
    );
  }

  /**
   * List documents across all managed firms (integrator endpoint).
   * Only available with integrator API keys (`sk_int_*`).
   *
   * @param params - Optional query parameters including firm_id filter
   * @returns Paginated list of documents across all firms
   *
   * @example
   * ```typescript
   * // Poll for new documents across all clients
   * const { documents } = await client.documents.inbox.listAll({
   *   since: '2026-04-01T00:00:00Z',
   *   status: 'received',
   * });
   * ```
   */
  listAll(params?: InboxAllParams): Promise<InboxAllResponse> {
    return this.request(
      "GET",
      `/documents/inbox/all${buildQuery({
        offset: params?.offset,
        limit: params?.limit,
        status: params?.status,
        since: params?.since,
        firm_id: params?.firm_id,
      })}`,
    );
  }
}

/**
 * Resource for sending, receiving, and managing Peppol e-invoicing documents.
 * This is the primary resource for document operations — sending invoices,
 * checking delivery status, downloading PDFs, and responding to received invoices.
 *
 * @example
 * ```typescript
 * const client = new EPostak({ apiKey: 'sk_live_xxxxx' });
 *
 * // Send an invoice
 * const result = await client.documents.send({
 *   receiverPeppolId: '0245:1234567890',
 *   items: [{ description: 'Consulting', quantity: 10, unitPrice: 100, vatRate: 23 }],
 * });
 *
 * // Check delivery status
 * const status = await client.documents.status(result.documentId);
 * ```
 */
export class DocumentsResource extends BaseResource {
  /** Sub-resource for managing received (inbound) documents */
  inbox: InboxResource;

  constructor(config: ClientConfig) {
    super(config);
    this.inbox = new InboxResource(config);
  }

  /**
   * Retrieve a single document by ID.
   *
   * @param id - Document UUID
   * @returns Full document object including parties, lines, and totals
   *
   * @example
   * ```typescript
   * const doc = await client.documents.get('doc-uuid');
   * console.log(doc.totals.withVat); // 1230.00
   * ```
   */
  get(id: string): Promise<InboxDocument> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}`);
  }

  /**
   * Update a draft document. Only documents in `"draft"` status can be updated.
   * Pass `null` to clear optional fields, or omit them to leave unchanged.
   *
   * @param id - Document UUID of the draft to update
   * @param body - Fields to update
   * @returns The updated document
   *
   * @example
   * ```typescript
   * const updated = await client.documents.update('doc-uuid', {
   *   dueDate: '2026-05-15',
   *   note: 'Updated payment terms',
   * });
   * ```
   */
  update(id: string, body: UpdateDocumentRequest): Promise<InboxDocument> {
    return this.request("PATCH", `/documents/${encodeURIComponent(id)}`, body);
  }

  /**
   * Send an invoice via the Peppol network. Accepts either structured JSON
   * (the API generates UBL XML) or pre-built UBL XML. Body cap 25 MB
   * (attachments are base64-embedded).
   *
   * **Document types (`docType`):** `invoice`, `credit_note`, `correction`,
   * `self_billing`, `reverse_charge`, `self_billing_credit_note`. Defaults to
   * `invoice` when omitted.
   *
   * **Supplier-party pinning (XML mode).** When submitting raw UBL via `xml`,
   * the server pins the seller identity (Name, IČO, IČ DPH, Postal Address,
   * Legal Entity name) to the authenticated firm. Caller-supplied values in
   * `cac:AccountingSupplierParty/cac:Party` are silently overwritten before
   * forwarding to Peppol. The Peppol `EndpointID` is the only supplier-party
   * field still validated against the firm's registered Peppol ID; mismatched
   * EndpointIDs are rejected with `422`. BG-24 attachments, line items,
   * payment terms, and custom note fields are preserved as-is. For
   * self-billing document types (UBL typecodes `261`/`389`), the customer
   * party (which is the authenticated firm) is rewritten instead.
   *
   * @param body - Invoice data as JSON fields or raw UBL XML
   * @returns Document ID, Peppol message ID, and status confirmation
   * @throws {EPostakError} 422 `VALIDATION_FAILED` — the document failed Peppol BIS 3.0
   *   Schematron validation, or the submitted UBL `EndpointID` does not match
   *   the firm's registered Peppol ID. `err.details` contains the list of
   *   validation errors. Use `documents.validate()` to pre-check before sending.
   * @throws {EPostakError} 502 `SEND_FAILED` — Peppol network temporarily unavailable. Retryable.
   *
   * @example
   * ```typescript
   * // Send using JSON (API generates UBL)
   * const result = await client.documents.send({
   *   receiverPeppolId: '0245:1234567890',
   *   invoiceNumber: 'FV-2026-042',
   *   items: [
   *     { description: 'Web development', quantity: 40, unit: 'HUR', unitPrice: 80, vatRate: 23 },
   *   ],
   * });
   *
   * // Send using raw UBL XML
   * const result = await client.documents.send({
   *   receiverPeppolId: '0245:1234567890',
   *   xml: '<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">...</Invoice>',
   * });
   * ```
   */
  send(
    body: SendDocumentRequest,
    options?: { idempotencyKey?: string },
  ): Promise<SendDocumentResponse> {
    const headers: Record<string, string> = {};
    if (options?.idempotencyKey) {
      headers["x-idempotency-key"] = options.idempotencyKey;
    }
    return this.request("POST", "/documents/send", body, { headers });
  }

  /**
   * Get the current delivery status and full status history of a document.
   * Use this to track whether a sent invoice was delivered, acknowledged, or failed.
   *
   * @param id - Document UUID
   * @returns Status details including history timeline and delivery timestamps
   *
   * @example
   * ```typescript
   * const status = await client.documents.status('doc-uuid');
   * console.log(status.status); // "delivered"
   * console.log(status.deliveredAt); // "2026-04-11T12:30:00Z"
   * ```
   */
  status(id: string): Promise<DocumentStatusResponse> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}/status`);
  }

  /**
   * Retrieve delivery evidence for a sent document, including AS4 receipts,
   * Message Level Response (MLR), Invoice Response from the buyer, and
   * SK TDD reporting state when applicable.
   *
   * @param id - Document UUID
   * @returns Evidence records (AS4 receipt, MLR, Invoice Response, TDD)
   *
   * @example
   * ```typescript
   * const evidence = await client.documents.evidence('doc-uuid');
   * if (evidence.invoiceResponse?.status === 'AP') {
   *   console.log('Invoice was accepted by the buyer');
   * }
   * ```
   */
  evidence(id: string): Promise<DocumentEvidenceResponse> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}/evidence`);
  }

  /**
   * Download the PDF visualization of a document.
   *
   * @param id - Document UUID
   * @returns PDF file content as a Buffer
   *
   * @example
   * ```typescript
   * import { writeFileSync } from 'fs';
   *
   * const pdfBuffer = await client.documents.pdf('doc-uuid');
   * writeFileSync('invoice.pdf', pdfBuffer);
   * ```
   */
  async pdf(id: string): Promise<Buffer> {
    const res = await this.request<Response>(
      "GET",
      `/documents/${encodeURIComponent(id)}/pdf`,
      undefined,
      { rawResponse: true },
    );
    return Buffer.from(await res.arrayBuffer());
  }

  /**
   * Download the UBL XML of a document.
   *
   * @param id - Document UUID
   * @returns UBL 2.1 XML content as a string
   *
   * @example
   * ```typescript
   * const xml = await client.documents.ubl('doc-uuid');
   * console.log(xml); // "<?xml version="1.0"?><Invoice ...>...</Invoice>"
   * ```
   */
  async ubl(id: string): Promise<string> {
    const res = await this.request<Response>(
      "GET",
      `/documents/${encodeURIComponent(id)}/ubl`,
      undefined,
      { rawResponse: true },
    );
    return res.text();
  }

  /**
   * Download the signed AS4 envelope of a document from the 10-year WORM
   * archive (S3 Object Lock COMPLIANCE mode). Returns the raw multipart AS4
   * bytes exactly as they were transmitted on the Peppol network — signed,
   * timestamped, and tamper-evident.
   *
   * Available on the `api-enterprise` plan. Every document that ever flowed
   * through our AP is retrievable for 10 years. Freshly-sent documents may
   * briefly return 404 while the archive cron catches up — retry after a few
   * seconds.
   *
   * @param id - Document UUID
   * @returns Raw AS4 envelope bytes as a Buffer
   * @throws {EPostakError} 403 — when the firm's plan does not include envelope access
   * @throws {EPostakError} 404 — when the document was not found or the envelope has not yet been archived
   *
   * @example
   * ```typescript
   * import { writeFileSync } from 'fs';
   *
   * const envelope = await client.documents.envelope('doc-uuid');
   * // Persist for long-term legal archival (signed AS4 bytes, XML-DSIG).
   * writeFileSync('doc-uuid.as4', envelope);
   * ```
   */
  async envelope(id: string): Promise<Buffer> {
    const res = await this.request<Response>(
      "GET",
      `/documents/${encodeURIComponent(id)}/envelope`,
      undefined,
      { rawResponse: true },
    );
    return Buffer.from(await res.arrayBuffer());
  }

  /**
   * Send an Invoice Response (Peppol Application Response) for a received
   * document. Routes a signed UBL Invoice Response back to the original
   * supplier over AS4.
   *
   * Valid status codes: `"AB"` (accepted billing), `"IP"` (in process),
   * `"UQ"` (under query), `"CA"` (conditionally accepted), `"RE"` (rejected),
   * `"AP"` (accepted), `"PD"` (paid). State rule: once a final status
   * (AP/RE/PD) has been recorded the endpoint refuses further changes; after
   * `UQ` one more update is permitted.
   *
   * HTTP 200 when the response was dispatched to Peppol, 202 when the
   * dispatch failed and the response is queued for retry (check
   * `dispatchStatus` and `dispatchError` to tell the two apart).
   *
   * @param id - Document UUID of the received invoice
   * @param body - Response status and optional note (max 500 chars)
   * @returns Confirmation with the response status, timestamp, Peppol message ID and dispatch status
   *
   * @example
   * ```typescript
   * // Accept an invoice
   * await client.documents.respond('doc-uuid', { status: 'AP' });
   *
   * // Reject with a reason
   * await client.documents.respond('doc-uuid', {
   *   status: 'RE',
   *   note: 'Incorrect VAT rate applied',
   * });
   * ```
   */
  respond(
    id: string,
    body: InvoiceRespondRequest,
  ): Promise<InvoiceRespondResponse> {
    return this.request(
      "POST",
      `/documents/${encodeURIComponent(id)}/respond`,
      body,
    );
  }

  /**
   * Validate a document without sending it. Runs UBL XSD + EN 16931 + Peppol
   * BIS 3.0 schematron against the payload and returns the findings list.
   *
   * The endpoint responds 200 when `valid === true` and 422 when `valid ===
   * false`; the SDK converts the 422 into an `EPostakError` by default, so
   * catch it when you want to read the failing findings.
   *
   * @param body - `{format: "json" | "ubl", document}` — `document` is a
   *   structured invoice object for `"json"`, a UBL XML string for `"ubl"`
   * @returns Validation result with per-layer errors and warnings
   *
   * @example
   * ```typescript
   * const result = await client.documents.validate({
   *   format: 'ubl',
   *   document: ublXml,
   * });
   * if (!result.valid) {
   *   console.error('Peppol errors:', result.errors);
   * }
   * ```
   */
  validate(body: ValidateDocumentRequest): Promise<ValidationResult> {
    return this.request("POST", "/documents/validate", body);
  }

  /**
   * Pre-flight check before sending — combines a Peppol participant lookup
   * with optional document validation and returns a tri-state go/no-go.
   *
   * The tri-state booleans (`recipientAcceptsDocumentType`,
   * `validationPassed`) may be `null` when the corresponding layer couldn't
   * be evaluated (e.g. SMP couldn't confirm support for a specific doctype,
   * or the validator was temporarily unreachable).
   *
   * @param body - Receiver Peppol ID, optional document type, optional document
   * @returns Preflight result with `canSend`, recipient and validation flags
   *
   * @example
   * ```typescript
   * const check = await client.documents.preflight({
   *   receiverPeppolId: '0245:1234567890',
   *   invoice: { ... },
   * });
   * if (!check.canSend) {
   *   console.error('Blocked:', check.validationErrors, check.warnings);
   * }
   * ```
   */
  preflight(body: PreflightRequest): Promise<PreflightResult> {
    return this.request("POST", "/documents/preflight", body);
  }

  /**
   * Convert between JSON and UBL XML formats without sending.
   * Useful for previewing the UBL output or parsing received XML into structured data.
   *
   * @param body - Conversion request with direction and input data
   * @returns Converted output (UBL XML string or parsed JSON object)
   *
   * @example
   * ```typescript
   * // JSON to UBL
   * const { document: ublXml, warnings } = await client.documents.convert({
   *   input_format: 'json',
   *   output_format: 'ubl',
   *   document: { invoiceNumber: 'FV-001', items: [...] },
   * });
   *
   * // UBL to JSON
   * const { document: parsed, warnings } = await client.documents.convert({
   *   input_format: 'ubl',
   *   output_format: 'json',
   *   document: '<Invoice>...</Invoice>',
   * });
   * ```
   */
  convert(body: ConvertRequest): Promise<ConvertResult> {
    return this.request("POST", "/documents/convert", body);
  }

  /**
   * Send up to 50 invoices in a single call. The endpoint always returns
   * HTTP 200 — each item carries its own HTTP status on `results[i].status`
   * (e.g. `201` when sent, `422` on validation failure, `502` on transport
   * failure). A partial failure does not abort the remaining items.
   *
   * Each item accepts the same fields as {@link DocumentsResource.send},
   * plus an optional `idempotencyKey` for per-item replay safety.
   *
   * @param items - Array of 1–50 send requests
   * @returns Aggregate counts plus per-item results in request order
   *
   * @example
   * ```typescript
   * const batch = await client.documents.sendBatch([
   *   {
   *     receiverPeppolId: '0245:1234567890',
   *     items: [{ description: 'A', quantity: 1, unitPrice: 100, vatRate: 23 }],
   *     idempotencyKey: 'order-001',
   *   },
   * ]);
   *
   * console.log(`${batch.succeeded}/${batch.total} sent`);
   * for (const r of batch.results) {
   *   if (r.status >= 400) {
   *     console.error(`Item ${r.index} failed (${r.status}):`, r.result);
   *   }
   * }
   * ```
   */
  sendBatch(items: BatchSendItem[]): Promise<BatchSendResponse> {
    return this.request(
      "POST",
      "/documents/send/batch",
      { items },
      { retry: true },
    );
  }

  /**
   * Parse a UBL 2.1 XML invoice into the normalized JSON shape used by
   * the ePošťák send pipeline plus `extras` and `allowances` for fields
   * that don't fit the normalized shape (e.g. Peppol extensions, custom
   * allowance/charge lines).
   *
   * Max 10 MB per request.
   *
   * @param xml - The UBL XML string
   * @returns `{invoice, extras, allowances}`
   *
   * @example
   * ```typescript
   * const { invoice, extras, allowances } = await client.documents.parse(ublXml);
   * ```
   */
  parse(xml: string): Promise<ParsedUblDocument> {
    return this.request("POST", "/documents/parse", { xml });
  }

  /**
   * Apply a granular state transition to a document. Use this when the
   * document's lifecycle extends beyond Peppol — e.g. an ERP marks it as
   * `"processed"` after accounting review, or as `"read"` once a human
   * opens it.
   *
   * Allowed states: `"delivered"`, `"processed"`, `"failed"`, `"read"`.
   * Invalid states return 422 (not 400).
   *
   * @param id - Document UUID
   * @param state - Target state to transition into
   * @param note - Optional human-readable note recorded with the transition
   * @returns The updated state, status, and associated timestamps
   *
   * @example
   * ```typescript
   * await client.documents.mark('doc-uuid', 'processed', 'Approved by CFO');
   * ```
   */
  outbox(params?: OutboxParams): Promise<OutboxListResponse> {
    return this.request(
      "GET",
      `/documents/outbox${buildQuery({
        offset: params?.offset,
        limit: params?.limit,
        status: params?.status,
        peppolMessageId: params?.peppolMessageId,
        since: params?.since,
      })}`,
    );
  }

  responses(id: string): Promise<InvoiceResponsesListResponse> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}/responses`);
  }

  events(id: string, params?: DocumentEventsParams): Promise<DocumentEventsResponse> {
    return this.request(
      "GET",
      `/documents/${encodeURIComponent(id)}/events${buildQuery({
        limit: params?.limit,
        cursor: params?.cursor,
      })}`,
    );
  }

  mark(
    id: string,
    state: DocumentMarkState,
    note?: string,
  ): Promise<MarkDocumentResponse> {
    const body: { state: DocumentMarkState; note?: string } = { state };
    if (note !== undefined) body.note = note;
    return this.request(
      "POST",
      `/documents/${encodeURIComponent(id)}/mark`,
      body,
    );
  }
}
