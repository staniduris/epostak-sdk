// ---------------------------------------------------------------------------
// Shared primitives
// ---------------------------------------------------------------------------

/**
 * Peppol Invoice Response status code per BIS 3.0 spec.
 * - `"AP"` — Accepted: the buyer accepts the invoice
 * - `"RE"` — Rejected: the buyer rejects the invoice
 * - `"UQ"` — Under Query: the buyer has questions about the invoice
 */
export type InvoiceResponseCode = "AP" | "RE" | "UQ";

/**
 * Webhook event types that can trigger notifications.
 * Subscribe to specific events when creating a webhook endpoint.
 */
export type WebhookEvent =
  | "document.created"
  | "document.sent"
  | "document.received"
  | "document.validated";

/**
 * Direction of a document relative to the authenticated firm.
 * - `"inbound"` — received from a trading partner
 * - `"outbound"` — sent to a trading partner
 */
export type DocumentDirection = "inbound" | "outbound";

/** Input format accepted by the `/documents/convert` endpoint. */
export type ConvertInputFormat = "json" | "ubl";

/** Output format produced by the `/documents/convert` endpoint. */
export type ConvertOutputFormat = "ubl" | "json";

/**
 * Processing status of an inbound document in your inbox.
 * - `"RECEIVED"` — document arrived but has not been acknowledged yet
 * - `"ACKNOWLEDGED"` — document was acknowledged (marked as processed)
 */
export type InboxStatus = "RECEIVED" | "ACKNOWLEDGED";

// ---------------------------------------------------------------------------
// Line items
// ---------------------------------------------------------------------------

/** A single invoice line item used when sending or creating a document. */
export interface LineItem {
  /** Human-readable description of the goods or service (e.g. "IT consulting") */
  description: string;
  /** Quantity of units delivered or performed */
  quantity: number;
  /** UN/CEFACT unit code, e.g. `"HUR"` = hours, `"C62"` = pieces, `"KGM"` = kilograms */
  unit?: string;
  /** Price per single unit, excluding VAT */
  unitPrice: number;
  /**
   * VAT rate as a percentage. Slovak legal rates only: `0`, `5`, `10`, `19`, `23`.
   * Any other value is rejected by the API with `422 VALIDATION_ERROR`.
   */
  vatRate: number;
  /** Optional discount as a percentage applied to this line (e.g. `10` for 10% off) */
  discount?: number;
}

/** A line item as returned by the API in document responses (includes computed totals). */
export interface LineItemResponse {
  /** Human-readable description of the goods or service */
  description: string;
  /** Quantity of units */
  quantity: number;
  /** UN/CEFACT unit code (e.g. `"HUR"`, `"C62"`, `"KGM"`) */
  unit?: string;
  /** Price per single unit, excluding VAT */
  unitPrice: number;
  /** VAT rate as a percentage */
  vatRate: number;
  /** VAT category code from UBL (e.g. `"S"` for standard rate, `"Z"` for zero-rated) */
  vatCategory?: string;
  /** Computed total for this line (quantity * unitPrice - discount), excluding VAT */
  lineTotal: number;
}

// ---------------------------------------------------------------------------
// Party
// ---------------------------------------------------------------------------

/** Postal address of a business party (supplier or customer). */
export interface PartyAddress {
  /** Street name and number (e.g. "Hlavna 42") */
  street?: string;
  /** City or municipality name */
  city?: string;
  /** Postal / ZIP code */
  zip?: string;
  /** ISO 3166-1 alpha-2 country code (e.g. `"SK"`, `"CZ"`, `"DE"`) */
  country?: string;
}

/** A business party (supplier or customer) involved in a document exchange. */
export interface Party {
  /** Legal name of the business entity */
  name?: string;
  /** Slovak business registration number (ICO) — 8-digit identifier assigned by the Statistical Office */
  ico?: string;
  /** Tax identification number (DIC) — used for income tax purposes in Slovakia */
  dic?: string;
  /** VAT identification number (IC DPH) — Slovak VAT number, prefixed with `"SK"` */
  icDph?: string;
  /** Postal address of the business */
  address?: PartyAddress;
  /** Peppol participant identifier (e.g. `"0245:1234567890"`) */
  peppolId?: string;
}

// ---------------------------------------------------------------------------
// Send document
// ---------------------------------------------------------------------------

/**
 * Request body for sending an invoice using structured JSON fields.
 * The API generates UBL XML automatically from these fields.
 */
export interface SendDocumentJsonRequest {
  /** Peppol participant ID of the receiver (e.g. `"0245:1234567890"`) */
  receiverPeppolId: string;
  /** Invoice number — auto-generated if omitted */
  invoiceNumber?: string;
  /** Issue date in `YYYY-MM-DD` format — defaults to today */
  issueDate?: string;
  /** Payment due date in `YYYY-MM-DD` format */
  dueDate?: string;
  /** ISO 4217 currency code (e.g. `"EUR"`, `"CZK"`) — defaults to `"EUR"` */
  currency?: string;
  /** Free-text note included in the invoice (e.g. payment terms, thank-you message) */
  note?: string;
  /** IBAN bank account number for payment */
  iban?: string;
  /** Payment method (e.g. `"credit_transfer"`, `"direct_debit"`) */
  paymentMethod?: string;
  /** Variable symbol (variabilny symbol) — Slovak payment reference used to match payments */
  variableSymbol?: string;
  /** Buyer reference / purchase order number required by some buyers */
  buyerReference?: string;
  /** Legal name of the receiver (overrides Peppol directory lookup) */
  receiverName?: string;
  /** Receiver's ICO — Slovak business registration number (8 digits) */
  receiverIco?: string;
  /** Receiver's DIC — tax identification number */
  receiverDic?: string;
  /** Receiver's IC DPH — VAT identification number */
  receiverIcDph?: string;
  /** Receiver's street address as a single string */
  receiverAddress?: string;
  /** Receiver's ISO 3166-1 alpha-2 country code (e.g. `"SK"`) */
  receiverCountry?: string;
  /** Invoice line items — at least one is required */
  items: LineItem[];
  /**
   * Invoice attachments (BG-24). Embedded into the generated UBL XML as
   * base64 via `AdditionalDocumentReference` / `EmbeddedDocumentBinaryObject`,
   * so the receiver sees them inline in the invoice.
   *
   * Limits: max 20 files per invoice, 10 MB per file, 15 MB total.
   */
  attachments?: DocumentAttachment[];
}

/**
 * An invoice attachment embedded as base64 into the UBL XML (BG-24).
 * MIME type is verified by magic-byte sniffing server-side.
 */
export interface DocumentAttachment {
  /** Original file name (max 255 chars) */
  fileName: string;
  /**
   * MIME type — must be one of the BR-CL-22 allowed types:
   * `application/pdf`, `image/png`, `image/jpeg`, `text/csv`,
   * `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (.xlsx),
   * `application/vnd.oasis.opendocument.spreadsheet` (.ods)
   */
  mimeType: string;
  /** Base64-encoded file content (no `data:` prefix). Max 10 MB after decoding. */
  content: string;
  /** Optional short description of the attachment (max 100 chars) */
  description?: string;
}

/**
 * Request body for sending a pre-built UBL XML invoice directly.
 * Use this when you generate your own UBL 2.1 XML.
 */
export interface SendDocumentXmlRequest {
  /** Peppol participant ID of the receiver (e.g. `"0245:1234567890"`) */
  receiverPeppolId: string;
  /** Complete UBL 2.1 Invoice XML document as a string */
  xml: string;
}

/**
 * Union type for sending a document — either structured JSON fields
 * or raw UBL XML. The API detects the variant by the presence of `xml` vs `items`.
 */
export type SendDocumentRequest =
  | SendDocumentJsonRequest
  | SendDocumentXmlRequest;

/** Response returned after successfully sending a document via Peppol. */
export interface SendDocumentResponse {
  /** Unique document ID in the ePosťak system */
  documentId: string;
  /** Peppol AS4 message ID for tracking delivery */
  messageId: string;
  /** Status is always `"SENT"` on success */
  status: "SENT";
}

// ---------------------------------------------------------------------------
// Update document (draft only)
// ---------------------------------------------------------------------------

/**
 * Request body for updating a draft document. Only draft documents can be updated.
 * Pass `null` to clear an optional field. Omit a field to leave it unchanged.
 */
export interface UpdateDocumentRequest {
  /** Invoice number */
  invoiceNumber?: string;
  /** Issue date in `YYYY-MM-DD` format */
  issueDate?: string;
  /** Payment due date in `YYYY-MM-DD` format, or `null` to clear */
  dueDate?: string | null;
  /** ISO 4217 currency code (e.g. `"EUR"`) */
  currency?: string;
  /** Free-text note, or `null` to clear */
  note?: string | null;
  /** IBAN bank account number, or `null` to clear */
  iban?: string | null;
  /** Variable symbol (variabilny symbol), or `null` to clear */
  variableSymbol?: string | null;
  /** Buyer reference / purchase order number, or `null` to clear */
  buyerReference?: string | null;
  /** Legal name of the receiver */
  receiverName?: string;
  /** Receiver's ICO (Slovak business registration number), or `null` to clear */
  receiverIco?: string | null;
  /** Receiver's DIC (tax identification number), or `null` to clear */
  receiverDic?: string | null;
  /** Receiver's IC DPH (VAT identification number), or `null` to clear */
  receiverIcDph?: string | null;
  /** Receiver's street address, or `null` to clear */
  receiverAddress?: string | null;
  /** Receiver's ISO 3166-1 alpha-2 country code, or `null` to clear */
  receiverCountry?: string | null;
  /** Receiver's Peppol participant ID, or `null` to clear */
  receiverPeppolId?: string | null;
  /** Replacement line items — replaces all existing lines when provided */
  items?: LineItem[];
}

// ---------------------------------------------------------------------------
// Document (shared response shape)
// ---------------------------------------------------------------------------

/** Aggregated monetary totals for a document. All amounts are in the document's currency. */
export interface DocumentTotals {
  /** Total amount excluding VAT */
  withoutVat: number;
  /** Total VAT amount */
  vat: number;
  /** Total amount including VAT (the payable amount) */
  withVat: number;
}

/**
 * Full document representation as returned by the API.
 * Used for both sent (outbound) and received (inbound) documents.
 */
export interface InboxDocument {
  /** Unique document ID in the ePosťak system (UUID) */
  id: string;
  /** Invoice number (e.g. `"FV-2026-0042"`) */
  number: string;
  /** Current processing status (e.g. `"SENT"`, `"DELIVERED"`, `"RECEIVED"`, `"ACKNOWLEDGED"`) */
  status: string;
  /** Whether this document was sent or received by the authenticated firm */
  direction: DocumentDirection;
  /** UBL document type (e.g. `"Invoice"`, `"CreditNote"`) */
  docType: string;
  /** Issue date in `YYYY-MM-DD` format */
  issueDate: string;
  /** Payment due date in `YYYY-MM-DD` format, or `null` if not specified */
  dueDate: string | null;
  /** ISO 4217 currency code (e.g. `"EUR"`) */
  currency: string;
  /** The party that issued the document (seller) */
  supplier: Party;
  /** The party that receives the document (buyer) */
  customer: Party;
  /** Individual line items on the document */
  lines: LineItemResponse[];
  /** Aggregated monetary totals */
  totals: DocumentTotals;
  /** Peppol AS4 message ID, or `null` for draft documents */
  peppolMessageId: string | null;
  /** ISO 8601 timestamp when the document was created */
  createdAt: string;
  /** ISO 8601 timestamp when the document was last updated */
  updatedAt: string;
}

// ---------------------------------------------------------------------------
// Inbox
// ---------------------------------------------------------------------------

/** Query parameters for listing documents in your inbox. */
export interface InboxListParams {
  /** Number of documents to skip (for pagination). Defaults to `0`. */
  offset?: number;
  /** Maximum number of documents to return (for pagination). Defaults to `20`. */
  limit?: number;
  /** Filter by processing status (`"RECEIVED"` or `"ACKNOWLEDGED"`) */
  status?: InboxStatus;
  /** ISO 8601 timestamp — only return documents created after this date (e.g. `"2026-01-01T00:00:00Z"`) */
  since?: string;
}

/** Paginated list of inbox documents. */
export interface InboxListResponse {
  /** Array of documents in the current page */
  documents: InboxDocument[];
  /** Total number of documents matching the query */
  total: number;
  /** The limit that was applied */
  limit: number;
  /** The offset that was applied */
  offset: number;
}

/** Detail view of a single inbox document, including the raw UBL XML payload. */
export interface InboxDocumentDetailResponse {
  /** Full document object */
  document: InboxDocument;
  /** Raw UBL XML content of the document, or `null` if not available */
  payload: string | null;
}

/** Response returned after acknowledging (marking as processed) an inbox document. */
export interface AcknowledgeResponse {
  /** ID of the acknowledged document */
  documentId: string;
  /** Status is always `"ACKNOWLEDGED"` on success */
  status: "ACKNOWLEDGED";
  /** ISO 8601 timestamp when the document was acknowledged */
  acknowledgedAt: string;
}

// ---------------------------------------------------------------------------
// Inbox all (integrator — cross-firm inbox)
// ---------------------------------------------------------------------------

/**
 * Query parameters for the integrator cross-firm inbox endpoint.
 * Only available with integrator API keys (`sk_int_*`).
 */
export interface InboxAllParams {
  /** Number of documents to skip (for pagination). Defaults to `0`. */
  offset?: number;
  /** Maximum number of documents to return. Defaults to `20`. */
  limit?: number;
  /** Filter by processing status */
  status?: InboxStatus;
  /** ISO 8601 timestamp — only return documents created after this date */
  since?: string;
  /** Filter to a specific firm UUID — useful when polling for one client's documents */
  firm_id?: string;
}

/**
 * Document from the integrator cross-firm inbox.
 * Uses snake_case field names (unlike single-firm endpoints which use camelCase).
 */
export interface InboxAllDocument {
  /** UUID of the firm that owns this document */
  firm_id: string;
  /** Name of the firm that owns this document */
  firm_name: string | null;
  /** Unique document ID (UUID) */
  id: string;
  /** Invoice number, or `null` if not yet assigned */
  number: string | null;
  /** Current processing status */
  status: string;
  /** Document direction (`"inbound"` or `"outbound"`) */
  direction: string;
  /** UBL document type (e.g. `"Invoice"`) */
  doc_type: string;
  /** Issue date in `YYYY-MM-DD` format */
  issue_date: string | null;
  /** Payment due date in `YYYY-MM-DD` format */
  due_date: string | null;
  /** ISO 4217 currency code */
  currency: string;
  /** Supplier (seller) summary */
  supplier: {
    /** Legal name of the supplier */
    name: string | null;
    /** Supplier's ICO (Slovak business registration number) */
    ico: string | null;
    /** Supplier's Peppol participant ID */
    peppol_id: string | null;
  };
  /** Customer (buyer) summary */
  customer: {
    /** Legal name of the customer */
    name: string | null;
    /** Customer's ICO (Slovak business registration number) */
    ico: string | null;
    /** Customer's Peppol participant ID */
    peppol_id: string | null;
  };
  /** Aggregated monetary totals */
  totals: {
    /** Total excluding VAT */
    without_vat: number | null;
    /** Total VAT amount */
    vat: number | null;
    /** Total including VAT */
    with_vat: number | null;
  };
  /** Peppol AS4 message ID */
  peppol_message_id: string | null;
  /** ISO 8601 timestamp when the document was created */
  created_at: string;
}

/** Paginated response from the integrator cross-firm inbox. */
export interface InboxAllResponse {
  /** Array of documents across all managed firms */
  documents: InboxAllDocument[];
  /** Total number of documents matching the query */
  total: number;
  /** The limit that was applied */
  limit: number;
  /** The offset that was applied */
  offset: number;
}

// ---------------------------------------------------------------------------
// Document lifecycle — status
// ---------------------------------------------------------------------------

/** A single entry in a document's status change history. */
export interface StatusHistoryEntry {
  /** Status at this point in time (e.g. `"SENT"`, `"DELIVERED"`, `"FAILED"`) */
  status: string;
  /** ISO 8601 timestamp when the status changed */
  timestamp: string;
  /** Human-readable detail about the transition, or `null` */
  detail: string | null;
}

/** Full status and lifecycle information for a document. */
export interface DocumentStatusResponse {
  /** Document ID (UUID) */
  id: string;
  /** Current status of the document */
  status: string;
  /** UBL document type (e.g. `"Invoice"`, `"CreditNote"`), or `null` if unknown */
  documentType: string | null;
  /** Peppol participant ID of the sender */
  senderPeppolId: string | null;
  /** Peppol participant ID of the receiver */
  receiverPeppolId: string | null;
  /** Ordered list of status transitions from creation to current state */
  statusHistory: StatusHistoryEntry[];
  /** Validation result (warnings, errors) from Peppol validation, or `null` */
  validationResult: Record<string, unknown> | null;
  /** ISO 8601 timestamp when the document was delivered to the receiver, or `null` */
  deliveredAt: string | null;
  /** ISO 8601 timestamp when the document was acknowledged by the receiver, or `null` */
  acknowledgedAt: string | null;
  /** Buyer's Invoice Response status (`"AP"`, `"RE"`, `"UQ"`), or `null` if not yet responded */
  invoiceResponseStatus: InvoiceResponseCode | null;
  /** AS4 message ID assigned by the Peppol access point, or `null` */
  as4MessageId: string | null;
  /** ISO 8601 timestamp when the document was created */
  createdAt: string;
  /** ISO 8601 timestamp when the document was last updated */
  updatedAt: string;
}

// ---------------------------------------------------------------------------
// Document lifecycle — evidence
// ---------------------------------------------------------------------------

/**
 * Delivery evidence and proof-of-receipt for a sent document.
 * Contains AS4 receipts, Message Level Response (MLR), and Invoice Response data.
 */
export interface DocumentEvidenceResponse {
  /** Document ID (UUID) */
  documentId: string;
  /** AS4 receipt from the receiving access point (proof of network delivery), or `null` */
  as4Receipt: Record<string, unknown> | null;
  /** Peppol Message Level Response document (delivery confirmation at the business level), or `null` */
  mlrDocument: Record<string, unknown> | null;
  /** Invoice Response from the buyer (accept/reject/query), or `null` if not yet responded */
  invoiceResponse: {
    /** Response status code, or `null` */
    status: InvoiceResponseCode | null;
    /** Full Invoice Response document */
    document: Record<string, unknown>;
  } | null;
  /** ISO 8601 timestamp when the document was delivered, or `null` */
  deliveredAt: string | null;
  /** ISO 8601 timestamp when the document was sent, or `null` */
  sentAt: string | null;
}

// ---------------------------------------------------------------------------
// Invoice response (respond to received document)
// ---------------------------------------------------------------------------

/** Request body for responding to a received invoice (accept, reject, or query). */
export interface InvoiceRespondRequest {
  /** Response status: `"AP"` = accepted, `"RE"` = rejected, `"UQ"` = under query */
  status: InvoiceResponseCode;
  /** Optional note explaining the response (e.g. reason for rejection) */
  note?: string;
}

/** Response returned after successfully sending an Invoice Response to the supplier. */
export interface InvoiceRespondResponse {
  /** ID of the document that was responded to */
  documentId: string;
  /** The response status that was sent */
  responseStatus: InvoiceResponseCode;
  /** ISO 8601 timestamp when the response was sent */
  respondedAt: string;
}

// ---------------------------------------------------------------------------
// Validate / preflight / convert
// ---------------------------------------------------------------------------

/** Result of validating a document before sending — checks Peppol BIS 3.0 compliance. */
export interface ValidationResult {
  /** `true` if the document passes all validation rules */
  valid: boolean;
  /** Non-fatal warnings (e.g. missing optional fields) */
  warnings: string[];
  /** Generated UBL XML preview — only present when validating JSON input, `null` for XML input */
  ubl: string | null;
}

/** Request body for the preflight check — verifies a Peppol receiver exists and supports the document type. */
export interface PreflightRequest {
  /** Peppol participant ID to check (e.g. `"0245:1234567890"`) */
  receiverPeppolId: string;
  /** Peppol document type identifier to check support for (defaults to standard Invoice) */
  documentTypeId?: string;
}

/** Result of a preflight check against the Peppol SMP. */
export interface PreflightResult {
  /** The Peppol participant ID that was checked */
  receiverPeppolId: string;
  /** `true` if the participant is registered in the Peppol network */
  registered: boolean;
  /** `true` if the participant's SMP indicates support for the requested document type */
  supportsDocumentType: boolean;
  /** URL of the participant's SMP record, or `null` if not registered */
  smpUrl: string | null;
}

/** Request body for converting between JSON and UBL XML formats. */
export interface ConvertRequest {
  /** Input format of `document` — `"json"` for structured object, `"ubl"` for XML string. */
  input_format: ConvertInputFormat;
  /** Desired output format — must differ from `input_format`. */
  output_format: ConvertOutputFormat;
  /** The document to convert. Object when `input_format` is `"json"`, XML string when `"ubl"`. */
  document: Record<string, unknown> | string;
}

/** Result of a format conversion. */
export interface ConvertResult {
  /** The output format that was produced */
  output_format: ConvertOutputFormat;
  /** Parsed JSON object when `output_format` is `"json"`, UBL XML string when `"ubl"` */
  document: Record<string, unknown> | string;
  /** Non-fatal warnings raised during conversion (empty array when none) */
  warnings: string[];
}

// ---------------------------------------------------------------------------
// Peppol
// ---------------------------------------------------------------------------

/** A document type capability advertised by a Peppol participant via their SMP record. */
export interface SmpParticipantCapability {
  /** Peppol document type identifier (e.g. `"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##..."`) */
  documentTypeId: string;
  /** Peppol process identifier (e.g. `"urn:fdc:peppol.eu:2017:poacc:billing:01:1.0"`) */
  processId: string;
  /** AS4 transport profile identifier (e.g. `"peppol-transport-as4-v2_0"`) */
  transportProfile: string;
}

/** Peppol participant details retrieved from the SMP (Service Metadata Publisher). */
export interface PeppolParticipant {
  /** Peppol participant ID (e.g. `"0245:1234567890"`) */
  peppolId: string;
  /** Registered business name, or `null` if not available in the Business Card */
  name: string | null;
  /** ISO 3166-1 alpha-2 country code, or `null` */
  country: string | null;
  /** List of document types and processes this participant supports */
  capabilities: SmpParticipantCapability[];
}

/** Query parameters for searching the Peppol Business Card directory. */
export interface DirectorySearchParams {
  /** Free-text search query (matches business name, identifiers, etc.) */
  q?: string;
  /** ISO 3166-1 alpha-2 country code to filter results (e.g. `"SK"`, `"CZ"`) */
  country?: string;
  /** Page number (1-based). Defaults to `1`. */
  page?: number;
  /** Number of results per page. Defaults to `20`. */
  page_size?: number;
}

/** A single entry from the Peppol Business Card directory. */
export interface DirectoryEntry {
  /** Peppol participant ID */
  peppolId: string;
  /** Registered business name */
  name: string;
  /** ISO 3166-1 alpha-2 country code */
  country: string;
  /** ISO 8601 timestamp when the participant was registered, or `null` */
  registeredAt: string | null;
}

/** Paginated search results from the Peppol Business Card directory. */
export interface DirectorySearchResult {
  /** Array of matching directory entries */
  results: DirectoryEntry[];
  /** Total number of results matching the query */
  total: number;
  /** Current page number (1-based) */
  page: number;
  /** Number of results per page */
  page_size: number;
}

/**
 * Company information from Slovak business registers (ORSR, FinStat)
 * combined with Peppol registration status.
 */
export interface CompanyLookup {
  /** Slovak business registration number (ICO) — 8 digits */
  ico: string;
  /** Legal name of the company */
  name: string;
  /** Tax identification number (DIC), or `null` if not available */
  dic: string | null;
  /** VAT identification number (IC DPH), or `null` if the company is not VAT-registered */
  icDph: string | null;
  /** Registered address of the company */
  address?: PartyAddress;
  /** Peppol participant ID if the company is registered on Peppol, otherwise `null` */
  peppolId: string | null;
}

// ---------------------------------------------------------------------------
// Firms
// ---------------------------------------------------------------------------

/** Summary of a firm managed by the integrator account. */
export interface FirmSummary {
  /** Firm UUID */
  id: string;
  /** Legal name of the firm */
  name: string;
  /** Slovak business registration number (ICO), or `null` */
  ico: string | null;
  /** Peppol participant ID, or `null` if not yet registered */
  peppolId: string | null;
  /** Peppol registration status (e.g. `"active"`, `"pending"`, `"none"`) */
  peppolStatus: string;
}

/** A Peppol identifier (scheme + value) registered for a firm. */
export interface FirmPeppolIdentifier {
  /** Peppol identifier scheme (e.g. `"0245"` for Slovak DIČ) */
  scheme: string;
  /** The identifier value within the scheme (e.g. the ICO number) */
  identifier: string;
}

/** Detailed firm information including tax IDs, address, and Peppol identifiers. */
export interface FirmDetail extends FirmSummary {
  /** Tax identification number (DIC), or `null` */
  dic: string | null;
  /** VAT identification number (IC DPH), or `null` */
  icDph: string | null;
  /** Registered address of the firm */
  address?: PartyAddress;
  /** All Peppol identifiers registered for this firm */
  peppolIdentifiers: FirmPeppolIdentifier[];
  /** ISO 8601 timestamp when the firm was created in the system */
  createdAt: string;
}

/** Wrapper response for the firms list endpoint. */
export interface FirmsListResponse {
  /** Array of firm summaries */
  firms: FirmSummary[];
}

/** Query parameters for listing a firm's documents. */
export interface FirmDocumentsParams {
  /** Number of documents to skip (for pagination). Defaults to `0`. */
  offset?: number;
  /** Maximum number of documents to return. Defaults to `20`. */
  limit?: number;
  /** Filter by document direction (`"inbound"` or `"outbound"`) */
  direction?: DocumentDirection;
}

/** Response after registering a new Peppol identifier for a firm. */
export interface PeppolIdentifierResponse {
  /** Full Peppol participant ID (e.g. `"0245:1234567890"`) */
  peppolId: string;
  /** Peppol identifier scheme */
  scheme: string;
  /** Identifier value within the scheme */
  identifier: string;
  /** ISO 8601 timestamp when the identifier was registered */
  registeredAt: string;
}

// ---------------------------------------------------------------------------
// Firm assignment (integrator)
// ---------------------------------------------------------------------------

/**
 * Request to assign a firm to the integrator account by its ICO.
 * Once assigned, the integrator can send/receive documents on behalf of this firm.
 */
export interface AssignFirmRequest {
  /** Slovak business registration number (ICO) — exactly 8 digits */
  ico: string;
}

/** Response after successfully assigning a firm to the integrator account. */
export interface AssignFirmResponse {
  /** The assigned firm's details */
  firm: {
    /** Firm UUID */
    id: string;
    /** Legal name of the firm */
    name: string | null;
    /** Slovak ICO */
    ico: string | null;
    /** Peppol participant ID, or `null` if not registered */
    peppol_id: string | null;
    /** Peppol registration status */
    peppol_status: string;
  };
  /** Assignment status — always `"active"` on success */
  status: "active";
}

/**
 * Request to assign multiple firms at once by their ICOs.
 * Maximum 50 ICOs per batch request.
 */
export interface BatchAssignFirmsRequest {
  /** Array of Slovak ICOs (8 digits each, max 50 per request) */
  icos: string[];
}

/** Result for a single firm in a batch assignment — may succeed or fail independently. */
export interface BatchAssignFirmResult {
  /** The ICO that was processed */
  ico: string;
  /** Firm details if assignment succeeded */
  firm?: {
    /** Firm UUID */
    id: string;
    /** Legal name */
    name: string | null;
    /** Slovak ICO */
    ico: string | null;
    /** Peppol participant ID */
    peppol_id: string | null;
    /** Peppol registration status */
    peppol_status: string;
  };
  /** `"active"` for newly assigned, `"already_assigned"` if previously assigned */
  status?: "active" | "already_assigned";
  /** Error code if assignment failed for this ICO */
  error?: string;
  /** Human-readable error message */
  message?: string;
}

/** Response from a batch firm assignment operation. */
export interface BatchAssignFirmsResponse {
  /** Individual results for each ICO in the request */
  results: BatchAssignFirmResult[];
}

// ---------------------------------------------------------------------------
// Webhooks
// ---------------------------------------------------------------------------

/** Request body for creating a new webhook subscription. */
export interface CreateWebhookRequest {
  /** HTTPS URL where webhook payloads will be POSTed */
  url: string;
  /** Event types to subscribe to — defaults to all events if omitted */
  events?: WebhookEvent[];
}

/** Request body for updating an existing webhook subscription. Omit fields to leave unchanged. */
export interface UpdateWebhookRequest {
  /** New HTTPS URL for the webhook */
  url?: string;
  /** Updated list of event types to subscribe to */
  events?: WebhookEvent[];
  /** Set to `false` to pause the webhook without deleting it */
  isActive?: boolean;
}

/** A webhook subscription. */
export interface Webhook {
  /** Webhook UUID */
  id: string;
  /** HTTPS URL where payloads are delivered */
  url: string;
  /** Event types this webhook is subscribed to */
  events: WebhookEvent[];
  /** Whether the webhook is currently active and receiving events */
  isActive: boolean;
  /** ISO 8601 timestamp when the webhook was created */
  createdAt: string;
}

/**
 * Webhook details including the signing secret.
 * The `secret` is only returned once at creation time — store it securely.
 */
export interface WebhookDetail extends Webhook {
  /** HMAC-SHA256 signing secret for verifying webhook payloads — only returned on creation */
  secret?: string;
}

/** A single webhook delivery attempt record. */
export interface WebhookDelivery {
  /** Delivery UUID */
  id: string;
  /** ID of the parent webhook */
  webhookId: string;
  /** Event type that triggered this delivery (e.g. `"document.received"`) */
  event: string;
  /** Delivery status (e.g. `"success"`, `"failed"`, `"pending"`) */
  status: string;
  /** Number of delivery attempts made (includes retries) */
  attempts: number;
  /** HTTP status code returned by the webhook URL, or `null` if the request failed */
  responseStatus: number | null;
  /** ISO 8601 timestamp when the delivery was created */
  createdAt: string;
}

/** Webhook details with recent delivery history. */
export interface WebhookWithDeliveries extends Webhook {
  /** Recent delivery attempts for this webhook */
  deliveries: WebhookDelivery[];
}

/** Wrapper response for the webhook list endpoint. */
export interface WebhookListResponse {
  /** Array of webhook subscriptions */
  data: Webhook[];
}

// ---------------------------------------------------------------------------
// Webhook test & delivery history
// ---------------------------------------------------------------------------

/** Response from sending a test event to a webhook endpoint. */
export interface WebhookTestResponse {
  /** Whether the test delivery was successful */
  success: boolean;
  /** HTTP status code returned by the webhook URL, or `null` if the request failed */
  statusCode: number | null;
  /** Round-trip response time in milliseconds */
  responseTime: number;
  /** The webhook UUID that was tested */
  webhookId: string;
  /** The event type used for the test */
  event: string;
  /** Error message if the test delivery failed */
  error?: string;
}

/** A single delivery record with full detail (used in paginated delivery history). */
export interface WebhookDeliveryDetail {
  /** Delivery UUID */
  id: string;
  /** ID of the parent webhook */
  webhookId: string;
  /** Event type that triggered this delivery */
  event: string;
  /** Delivery status */
  status: "SUCCESS" | "FAILED" | "PENDING" | "RETRYING";
  /** Number of delivery attempts made */
  attempts: number;
  /** HTTP status code returned by the webhook URL */
  responseStatus: number | null;
  /** Truncated response body from the webhook URL */
  responseBody: string | null;
  /** ISO 8601 timestamp of the last delivery attempt */
  lastAttemptAt: string | null;
  /** ISO 8601 timestamp of the next scheduled retry, or `null` if not retrying */
  nextRetryAt: string | null;
  /** ISO 8601 timestamp when the delivery was created */
  createdAt: string;
}

/** Query parameters for fetching paginated webhook delivery history. */
export interface WebhookDeliveriesParams {
  /** Maximum number of deliveries to return (1-100). Defaults to `20`. */
  limit?: number;
  /** Number of deliveries to skip for pagination. Defaults to `0`. */
  offset?: number;
  /** Filter by delivery status */
  status?: "SUCCESS" | "FAILED" | "PENDING" | "RETRYING";
  /** Filter by event type (e.g. `"document.received"`) */
  event?: string;
}

/** Paginated response of webhook delivery history. */
export interface WebhookDeliveriesResponse {
  /** Array of delivery records */
  deliveries: WebhookDeliveryDetail[];
  /** Total number of deliveries matching the filter */
  total: number;
  /** Number of deliveries returned */
  limit: number;
  /** Offset used for pagination */
  offset: number;
}

/**
 * Response from rotating a webhook's signing secret. The new secret is
 * returned ONCE — store it immediately. The previous secret is invalidated
 * on the server side and will not verify HMAC signatures from this point on.
 */
export interface WebhookRotateSecretResponse {
  /** The webhook UUID whose secret was rotated */
  id: string;
  /** The new HMAC-SHA256 signing secret (only returned once) */
  secret: string;
  /** Human-readable confirmation message */
  message: string;
}

// ---------------------------------------------------------------------------
// Webhook pull queue
// ---------------------------------------------------------------------------

/**
 * Query parameters for pulling events from the webhook queue.
 * Use the pull queue as an alternative to push webhooks when your server
 * cannot receive inbound HTTPS requests.
 */
export interface WebhookQueueParams {
  /** Maximum number of items to return (1-100). Defaults to `20`. */
  limit?: number;
  /** Filter by event type (e.g. `"document.received"`) — returns only matching events */
  event_type?: string;
}

/** A single event item from the webhook pull queue. */
export interface WebhookQueueItem {
  /** Event ID — use this to acknowledge the event after processing */
  id: string;
  /** Event type (e.g. `"document.received"`, `"document.sent"`) */
  type: string;
  /** ISO 8601 timestamp when the event was created */
  created_at: string;
  /** Event payload containing the document data and metadata */
  payload: Record<string, unknown>;
}

/** Response from pulling events from the webhook queue. */
export interface WebhookQueueResponse {
  /** Array of unacknowledged events */
  items: WebhookQueueItem[];
  /** `true` if there are more events available beyond the current batch */
  has_more: boolean;
}

// ---------------------------------------------------------------------------
// Webhook queue all (integrator — cross-firm)
// ---------------------------------------------------------------------------

/**
 * Query parameters for the integrator cross-firm webhook queue.
 * Only available with integrator API keys (`sk_int_*`).
 */
export interface WebhookQueueAllParams {
  /** Maximum number of events to return (1-500). Defaults to `100`. */
  limit?: number;
  /** ISO 8601 timestamp — only return events created after this date (for cursor-based polling) */
  since?: string;
}

/** A single event from the integrator cross-firm webhook queue. */
export interface WebhookQueueAllEvent {
  /** Unique event ID — use this to acknowledge the event */
  event_id: string;
  /** UUID of the firm this event belongs to */
  firm_id: string;
  /** Event type (e.g. `"document.received"`) */
  event: string;
  /** Event payload containing document data and metadata */
  payload: Record<string, unknown>;
  /** ISO 8601 timestamp when the event was created */
  created_at: string;
}

/** Response from the integrator cross-firm webhook queue. */
export interface WebhookQueueAllResponse {
  /** Array of events across all managed firms */
  events: WebhookQueueAllEvent[];
  /** Number of events returned in this response */
  count: number;
}

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

/** Query parameters for the reporting statistics endpoint. */
export interface StatisticsParams {
  /** Start of the reporting period in `YYYY-MM-DD` format. Defaults to 30 days ago. */
  from?: string;
  /** End of the reporting period in `YYYY-MM-DD` format. Defaults to today. */
  to?: string;
}

/** Aggregated document statistics for a given time period. */
export interface Statistics {
  /** The reporting period boundaries */
  period: {
    /** Start date in `YYYY-MM-DD` format */
    from: string;
    /** End date in `YYYY-MM-DD` format */
    to: string;
  };
  /** Outbound (sent) document statistics */
  outbound: {
    /** Total outbound documents in the period */
    total: number;
    /** Successfully delivered documents */
    delivered: number;
    /** Documents that failed to deliver */
    failed: number;
  };
  /** Inbound (received) document statistics */
  inbound: {
    /** Total inbound documents in the period */
    total: number;
    /** Documents that have been acknowledged */
    acknowledged: number;
    /** Documents still awaiting acknowledgment */
    pending: number;
  };
}

// ---------------------------------------------------------------------------
// Account
// ---------------------------------------------------------------------------

/** Account information including firm details, subscription plan, and usage counters. */
export interface Account {
  /** The firm associated with this API key */
  firm: {
    /** Legal name of the firm */
    name: string;
    /** Slovak business registration number (ICO), or `null` */
    ico: string | null;
    /** Peppol participant ID, or `null` if not registered */
    peppolId: string | null;
    /** Peppol registration status (e.g. `"active"`, `"pending"`, `"none"`) */
    peppolStatus: string;
  };
  /** Current subscription plan */
  plan: {
    /** Plan name (e.g. `"starter"`, `"business"`, `"enterprise"`) */
    name: string;
    /** Plan status — `"active"` or `"expired"` */
    status: "active" | "expired";
  };
  /** Document usage counters for the current billing period */
  usage: {
    /** Number of outbound (sent) documents */
    outbound: number;
    /** Number of inbound (received) documents */
    inbound: number;
  };
}

// ---------------------------------------------------------------------------
// Auth introspection & rotation
// ---------------------------------------------------------------------------

/** Key metadata returned by the auth status introspection endpoint. */
export interface AuthStatusKey {
  /** Opaque key ID (not the plaintext secret) */
  keyId: string;
  /** Short prefix identifying the key (e.g. `"sk_live_abc"`) — safe to log */
  prefix: string;
  /** ISO 8601 timestamp when the key was created */
  createdAt: string;
  /** ISO 8601 timestamp of the last successful request, or `null` if never used */
  lastUsedAt: string | null;
}

/** Rate-limit info returned by the auth status introspection endpoint. */
export interface AuthStatusRateLimit {
  /** Maximum requests allowed in the current window */
  limit: number;
  /** Requests remaining in the current window */
  remaining: number;
  /** Window duration in seconds */
  windowSeconds: number;
}

/** Integrator summary returned when the key belongs to an integrator account. */
export interface AuthStatusIntegrator {
  /** Integrator UUID */
  id: string;
  /** Integrator display name */
  name: string;
}

/**
 * Response from `POST /auth/status` — introspects the calling API key
 * without revealing the plaintext secret.
 */
export interface AuthStatusResponse {
  /** Metadata about the API key being used */
  key: AuthStatusKey;
  /** The firm this key is authenticated against */
  firm: {
    /** Firm UUID */
    id: string;
    /** Legal name of the firm */
    name: string;
    /** Slovak business registration number (ICO), or `null` */
    ico: string | null;
  };
  /** Current subscription plan (e.g. `"api-enterprise"`, `"business"`) */
  plan: string;
  /** Applicable rate-limit configuration */
  rateLimit: AuthStatusRateLimit;
  /** Integrator details — only present when the key is `sk_int_*` */
  integrator?: AuthStatusIntegrator;
}

/**
 * Response from `POST /auth/rotate-secret` — the new plaintext key is
 * returned ONCE. Store it immediately; the previous key is deactivated.
 * Integrator keys (`sk_int_*`) are rejected with 409.
 */
export interface RotateSecretResponse {
  /** Opaque key ID of the newly created key */
  keyId: string;
  /** The new plaintext API key (`sk_live_...`) — returned only once */
  key: string;
  /** Short prefix of the new key — safe to store for reference */
  prefix: string;
  /** ISO 8601 timestamp when the rotation completed */
  rotatedAt: string;
}

// ---------------------------------------------------------------------------
// Batch send
// ---------------------------------------------------------------------------

/** A single item in a batch send request — same shape as `send()` plus an optional idempotency key. */
export type BatchSendItem = SendDocumentRequest & {
  /**
   * Optional idempotency key for this item. When the same key is replayed
   * within the idempotency window, the original result is returned instead
   * of sending the invoice twice.
   */
  idempotencyKey?: string;
};

/** Per-item result in a batch send response. */
export interface BatchSendResult {
  /** Zero-based index of the item in the original request */
  index: number;
  /** `"success"` if the item was sent, `"error"` if it failed */
  status: "success" | "error";
  /**
   * On success: the normal send response (document ID, message ID, status).
   * On error: an error object with `code` and `message`.
   */
  result: SendDocumentResponse | { code: string; message: string };
}

/**
 * Response from `POST /documents/send/batch` — always returns 200.
 * Per-item errors live in `results[].status === "error"`.
 */
export interface BatchSendResponse {
  /** Total number of items in the request */
  total: number;
  /** Number of items that were sent successfully */
  succeeded: number;
  /** Number of items that failed */
  failed: number;
  /** Individual per-item results, in request order */
  results: BatchSendResult[];
}

// ---------------------------------------------------------------------------
// Parse UBL XML
// ---------------------------------------------------------------------------

/**
 * Response from `POST /documents/parse` — the normalized JSON invoice shape
 * derived from a UBL XML input. Matches the structured fields accepted by
 * `send()` so the output can be edited and sent back through the pipeline.
 */
export interface ParsedUblDocument {
  /** Normalized invoice JSON extracted from the UBL XML */
  document: Record<string, unknown>;
  /** Non-fatal warnings raised during parsing (empty array when none) */
  warnings: string[];
}

// ---------------------------------------------------------------------------
// Mark document state
// ---------------------------------------------------------------------------

/**
 * Granular state that can be set via `POST /documents/{id}/mark`.
 * - `"delivered"` — Peppol delivery confirmed (manual override)
 * - `"processed"` — document was processed by the recipient's system
 * - `"failed"` — terminal failure; no further retries
 * - `"read"` — recipient opened / viewed the document
 */
export type DocumentMarkState = "delivered" | "processed" | "failed" | "read";

/** Request body for `POST /documents/{id}/mark`. */
export interface MarkDocumentRequest {
  /** Target state to transition the document into */
  state: DocumentMarkState;
  /** Optional human-readable note recorded alongside the transition */
  note?: string;
}

/** Response from `POST /documents/{id}/mark`. */
export interface MarkDocumentResponse {
  /** Document UUID */
  id: string;
  /** The granular state that was applied */
  state: DocumentMarkState;
  /** The resulting aggregate status (e.g. `"DELIVERED"`, `"FAILED"`) */
  status: string;
  /** ISO 8601 timestamp of delivery, or `null` */
  deliveredAt: string | null;
  /** ISO 8601 timestamp of acknowledgement, or `null` */
  acknowledgedAt: string | null;
  /** ISO 8601 timestamp when the document was marked as read, or `null` */
  readAt: string | null;
}

// ---------------------------------------------------------------------------
// Peppol capabilities probe
// ---------------------------------------------------------------------------

/** Request body for `POST /peppol/capabilities` — Storecove-style capability probe. */
export interface PeppolCapabilitiesRequest {
  /** Peppol identifier scheme (e.g. `"0245"` for Slovak DIČ) */
  scheme: string;
  /** Identifier value within the scheme */
  identifier: string;
  /** Optional specific document type ID to check for support */
  documentType?: string;
}

/**
 * Response from `POST /peppol/capabilities` — describes what a Peppol
 * participant is able to receive over the network.
 */
export interface PeppolCapabilitiesResponse {
  /** `true` if the participant was found in SMP */
  found: boolean;
  /** High-level document families accepted (e.g. `["invoice", "creditNote"]`) */
  accepts: string[];
  /** Full list of specific document type IDs advertised by the SMP */
  supportedDocumentTypes: string[];
  /** The matched document type ID when `documentType` was provided in the request */
  matchedDocumentType?: string;
}

// ---------------------------------------------------------------------------
// Batch participant lookup
// ---------------------------------------------------------------------------

/** A single participant to look up in a batch request. */
export interface BatchLookupParticipant {
  /** Peppol identifier scheme (e.g. `"0245"`) */
  scheme: string;
  /** Identifier value within the scheme */
  identifier: string;
}

/** Request body for `POST /peppol/participants/batch` — max 100 participants. */
export interface BatchLookupRequest {
  /** Participants to look up, max 100 per request */
  participants: BatchLookupParticipant[];
}

/** Per-participant result in a batch lookup response. */
export interface BatchLookupResult {
  /** Peppol identifier scheme */
  scheme: string;
  /** Identifier value within the scheme */
  identifier: string;
  /** `true` if the participant was found in SMP */
  found: boolean;
  /** Participant details when `found === true`, otherwise omitted */
  participant?: PeppolParticipant;
}

/** Response from `POST /peppol/participants/batch`. */
export interface BatchLookupResponse {
  /** Total number of participants in the request */
  total: number;
  /** Number of participants that were found */
  found: number;
  /** Number of participants that were not found */
  notFound: number;
  /** Individual per-participant results, in request order */
  results: BatchLookupResult[];
}

// ---------------------------------------------------------------------------
// Public validator-as-a-service
// ---------------------------------------------------------------------------

/**
 * A single validation finding (error or warning) emitted by the public
 * validator. Surfaced per validation layer (XSD / EN16931 / Peppol).
 */
export interface PublicValidationMessage {
  /** Rule ID that was violated (e.g. `"BR-CO-10"`, `"PEPPOL-EN16931-R001"`) */
  ruleId: string;
  /** Human-readable message describing the violation */
  message: string;
  /** Severity (`"fatal"`, `"error"`, `"warning"`) */
  severity: string;
  /** XPath to the offending node in the UBL XML, or `null` */
  location?: string | null;
}

/**
 * Response from the public `POST /api/validate` endpoint (no auth required).
 * Rate-limited to 20 requests per minute per IP.
 */
export interface PublicValidationReport {
  /** `true` if the XML passes all three validation layers */
  valid: boolean;
  /** UBL 2.1 XSD schema validation layer */
  xsd: {
    /** `true` if the XML is schema-valid UBL */
    valid: boolean;
    /** Errors emitted by the XSD validator */
    messages: PublicValidationMessage[];
  };
  /** EN 16931 core invoice model validation layer */
  en16931: {
    /** `true` if the invoice passes EN 16931 business rules */
    valid: boolean;
    /** Errors and warnings emitted by the EN 16931 schematron */
    messages: PublicValidationMessage[];
  };
  /** Peppol BIS 3.0 + SK CIUS schematron layer */
  peppol: {
    /** `true` if the invoice passes Peppol BIS 3.0 + SK CIUS */
    valid: boolean;
    /** Errors and warnings emitted by the Peppol schematron */
    messages: PublicValidationMessage[];
  };
}

// ---------------------------------------------------------------------------
// Extract
// ---------------------------------------------------------------------------

/** Result of AI-powered data extraction from a single PDF or image file. */
export interface ExtractResult {
  /** Extracted structured data (invoice fields, line items, parties, totals, etc.) */
  extraction: Record<string, unknown>;
  /** Generated UBL 2.1 XML from the extracted data */
  ubl_xml: string;
  /** Confidence score from 0 to 1 indicating extraction reliability */
  confidence: number;
  /** Original file name of the uploaded document */
  file_name: string;
}

/** Result for a single file within a batch extraction — may succeed or fail independently. */
export interface BatchExtractItem {
  /** Original file name */
  file_name: string;
  /** Extracted structured data, present on success */
  extraction?: Record<string, unknown>;
  /** Generated UBL XML, present on success */
  ubl_xml?: string;
  /** Confidence score as a string, present on success */
  confidence?: string;
  /** Error message if extraction failed for this file */
  error?: string;
}

/** Result of a batch extraction operation across multiple files. */
export interface BatchExtractResult {
  /** Unique batch ID for reference */
  batch_id: string;
  /** Total number of files in the batch */
  total: number;
  /** Number of files successfully extracted */
  successful: number;
  /** Number of files that failed extraction */
  failed: number;
  /** Individual results for each file */
  results: BatchExtractItem[];
}
