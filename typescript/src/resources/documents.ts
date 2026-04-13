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
  ValidationResult,
  PreflightRequest,
  PreflightResult,
  ConvertRequest,
  ConvertResult,
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
   *   status: 'RECEIVED',
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
   * Once acknowledged, the document moves from `RECEIVED` to `ACKNOWLEDGED` status.
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
   *   status: 'RECEIVED',
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
   * Update a draft document. Only documents that have not been sent yet can be updated.
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
   * (the API generates UBL XML) or pre-built UBL XML.
   *
   * @param body - Invoice data as JSON fields or raw UBL XML
   * @returns Document ID, Peppol message ID, and status confirmation
   * @throws {EPostakError} 422 `VALIDATION_FAILED` — the document failed Peppol BIS 3.0
   *   Schematron validation. `err.details` contains the list of validation errors.
   *   Use `documents.validate()` to pre-check before sending.
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
  send(body: SendDocumentRequest): Promise<SendDocumentResponse> {
    return this.request("POST", "/documents/send", body);
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
   * console.log(status.status); // "DELIVERED"
   * console.log(status.deliveredAt); // "2026-04-11T12:30:00Z"
   * ```
   */
  status(id: string): Promise<DocumentStatusResponse> {
    return this.request("GET", `/documents/${encodeURIComponent(id)}/status`);
  }

  /**
   * Retrieve delivery evidence for a sent document, including AS4 receipts,
   * Message Level Response (MLR), and Invoice Response from the buyer.
   *
   * @param id - Document UUID
   * @returns Evidence records (AS4 receipt, MLR, Invoice Response)
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
   * Send an Invoice Response (accept, reject, or query) for a received document.
   * This sends a Peppol Invoice Response message back to the supplier.
   *
   * @param id - Document UUID of the received invoice
   * @param body - Response status and optional note
   * @returns Confirmation with the response status and timestamp
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
   * Validate a document without sending it. Checks Peppol BIS 3.0 compliance
   * and returns warnings. For JSON input, also returns the generated UBL XML preview.
   *
   * @param body - Document data to validate (same format as `send()`)
   * @returns Validation result with warnings and optional UBL preview
   *
   * @example
   * ```typescript
   * const result = await client.documents.validate({
   *   receiverPeppolId: '0245:1234567890',
   *   items: [{ description: 'Test', quantity: 1, unitPrice: 100, vatRate: 23 }],
   * });
   * if (!result.valid) {
   *   console.error('Validation failed:', result.warnings);
   * }
   * ```
   */
  validate(body: SendDocumentRequest): Promise<ValidationResult> {
    return this.request("POST", "/documents/validate", body);
  }

  /**
   * Check if a Peppol receiver is registered and supports the target document type
   * before sending. Use this to avoid sending to non-existent participants.
   *
   * @param body - Receiver Peppol ID and optional document type to check
   * @returns Preflight result with registration and capability info
   *
   * @example
   * ```typescript
   * const check = await client.documents.preflight({
   *   receiverPeppolId: '0245:1234567890',
   * });
   * if (!check.registered) {
   *   console.error('Receiver is not on the Peppol network');
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
   * const { result: ublXml } = await client.documents.convert({
   *   direction: 'json_to_ubl',
   *   data: { invoiceNumber: 'FV-001', items: [...] },
   * });
   *
   * // UBL to JSON
   * const { result: parsed } = await client.documents.convert({
   *   direction: 'ubl_to_json',
   *   xml: '<Invoice>...</Invoice>',
   * });
   * ```
   */
  convert(body: ConvertRequest): Promise<ConvertResult> {
    return this.request("POST", "/documents/convert", body);
  }
}
