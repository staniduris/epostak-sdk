// ---------------------------------------------------------------------------
// Shared primitives
// ---------------------------------------------------------------------------

export type InvoiceResponseCode = "AP" | "RE" | "UQ";
export type WebhookEvent =
  | "document.created"
  | "document.sent"
  | "document.received"
  | "document.validated";
export type DocumentDirection = "inbound" | "outbound";
export type ConvertDirection = "json_to_ubl" | "ubl_to_json";
export type InboxStatus = "RECEIVED" | "ACKNOWLEDGED";

// ---------------------------------------------------------------------------
// Line items
// ---------------------------------------------------------------------------

export interface LineItem {
  description: string;
  quantity: number;
  /** UN/CEFACT unit code, e.g. HUR = hours, C62 = pieces, KGM = kg */
  unit?: string;
  unitPrice: number;
  /** VAT rate in percent, e.g. 23 */
  vatRate: number;
  /** Optional discount in percent */
  discount?: number;
}

export interface LineItemResponse {
  description: string;
  quantity: number;
  unit?: string;
  unitPrice: number;
  vatRate: number;
  vatCategory?: string;
  lineTotal: number;
}

// ---------------------------------------------------------------------------
// Party
// ---------------------------------------------------------------------------

export interface PartyAddress {
  street?: string;
  city?: string;
  zip?: string;
  country?: string;
}

export interface Party {
  name?: string;
  ico?: string;
  dic?: string;
  icDph?: string;
  address?: PartyAddress;
  peppolId?: string;
}

// ---------------------------------------------------------------------------
// Send document
// ---------------------------------------------------------------------------

export interface SendDocumentJsonRequest {
  receiverPeppolId: string;
  invoiceNumber?: string;
  issueDate?: string;
  dueDate?: string;
  currency?: string;
  note?: string;
  iban?: string;
  paymentMethod?: string;
  variableSymbol?: string;
  buyerReference?: string;
  receiverName?: string;
  receiverIco?: string;
  receiverDic?: string;
  receiverIcDph?: string;
  receiverAddress?: string;
  receiverCountry?: string;
  items: LineItem[];
}

export interface SendDocumentXmlRequest {
  receiverPeppolId: string;
  xml: string;
}

export type SendDocumentRequest =
  | SendDocumentJsonRequest
  | SendDocumentXmlRequest;

export interface SendDocumentResponse {
  documentId: string;
  messageId: string;
  status: "SENT";
}

// ---------------------------------------------------------------------------
// Update document (draft only)
// ---------------------------------------------------------------------------

export interface UpdateDocumentRequest {
  invoiceNumber?: string;
  issueDate?: string;
  dueDate?: string | null;
  currency?: string;
  note?: string | null;
  iban?: string | null;
  variableSymbol?: string | null;
  buyerReference?: string | null;
  receiverName?: string;
  receiverIco?: string | null;
  receiverDic?: string | null;
  receiverIcDph?: string | null;
  receiverAddress?: string | null;
  receiverCountry?: string | null;
  receiverPeppolId?: string | null;
  items?: LineItem[];
}

// ---------------------------------------------------------------------------
// Document (shared response shape)
// ---------------------------------------------------------------------------

export interface DocumentTotals {
  withoutVat: number;
  vat: number;
  withVat: number;
}

/** Document as returned by the API (same shape for sent and received). */
export interface InboxDocument {
  id: string;
  number: string;
  status: string;
  direction: DocumentDirection;
  docType: string;
  issueDate: string;
  dueDate: string | null;
  currency: string;
  supplier: Party;
  customer: Party;
  lines: LineItemResponse[];
  totals: DocumentTotals;
  peppolMessageId: string | null;
  createdAt: string;
  updatedAt: string;
}

// ---------------------------------------------------------------------------
// Inbox
// ---------------------------------------------------------------------------

export interface InboxListParams {
  offset?: number;
  limit?: number;
  status?: InboxStatus;
  /** ISO 8601 timestamp — only return documents created after this date */
  since?: string;
}

export interface InboxListResponse {
  documents: InboxDocument[];
  total: number;
  limit: number;
  offset: number;
}

export interface InboxDocumentDetailResponse {
  document: InboxDocument;
  /** UBL XML content, null if not available */
  payload: string | null;
}

export interface AcknowledgeResponse {
  documentId: string;
  status: "ACKNOWLEDGED";
  acknowledgedAt: string;
}

// ---------------------------------------------------------------------------
// Inbox all (integrator — cross-firm inbox)
// ---------------------------------------------------------------------------

export interface InboxAllParams {
  offset?: number;
  limit?: number;
  status?: InboxStatus;
  /** ISO 8601 timestamp — only return documents created after this date */
  since?: string;
  /** Filter to a specific firm UUID */
  firm_id?: string;
}

export interface InboxAllDocument {
  firm_id: string;
  firm_name: string | null;
  id: string;
  number: string | null;
  status: string;
  direction: string;
  doc_type: string;
  issue_date: string | null;
  due_date: string | null;
  currency: string;
  supplier: {
    name: string | null;
    ico: string | null;
    peppol_id: string | null;
  };
  customer: {
    name: string | null;
    ico: string | null;
    peppol_id: string | null;
  };
  totals: {
    without_vat: number | null;
    vat: number | null;
    with_vat: number | null;
  };
  peppol_message_id: string | null;
  created_at: string;
}

export interface InboxAllResponse {
  documents: InboxAllDocument[];
  total: number;
  limit: number;
  offset: number;
}

// ---------------------------------------------------------------------------
// Document lifecycle — status
// ---------------------------------------------------------------------------

export interface StatusHistoryEntry {
  status: string;
  timestamp: string;
  detail: string | null;
}

export interface DocumentStatusResponse {
  id: string;
  status: string;
  documentType: string | null;
  senderPeppolId: string | null;
  receiverPeppolId: string | null;
  statusHistory: StatusHistoryEntry[];
  validationResult: Record<string, unknown> | null;
  deliveredAt: string | null;
  acknowledgedAt: string | null;
  invoiceResponseStatus: InvoiceResponseCode | null;
  as4MessageId: string | null;
  createdAt: string;
  updatedAt: string;
}

// ---------------------------------------------------------------------------
// Document lifecycle — evidence
// ---------------------------------------------------------------------------

export interface DocumentEvidenceResponse {
  documentId: string;
  as4Receipt: Record<string, unknown> | null;
  mlrDocument: Record<string, unknown> | null;
  invoiceResponse: {
    status: InvoiceResponseCode | null;
    document: Record<string, unknown>;
  } | null;
  deliveredAt: string | null;
  sentAt: string | null;
}

// ---------------------------------------------------------------------------
// Invoice response (respond to received document)
// ---------------------------------------------------------------------------

export interface InvoiceRespondRequest {
  /** AP = accepted, RE = rejected, UQ = under query */
  status: InvoiceResponseCode;
  note?: string;
}

export interface InvoiceRespondResponse {
  documentId: string;
  responseStatus: InvoiceResponseCode;
  respondedAt: string;
}

// ---------------------------------------------------------------------------
// Validate / preflight / convert
// ---------------------------------------------------------------------------

export interface ValidationResult {
  valid: boolean;
  warnings: string[];
  /** Generated UBL XML, only present for JSON mode requests */
  ubl: string | null;
}

export interface PreflightRequest {
  receiverPeppolId: string;
  documentTypeId?: string;
}

export interface PreflightResult {
  receiverPeppolId: string;
  registered: boolean;
  supportsDocumentType: boolean;
  smpUrl: string | null;
}

export interface ConvertRequest {
  direction: ConvertDirection;
  /** JSON document data (for json_to_ubl) */
  data?: Record<string, unknown>;
  /** UBL XML string (for ubl_to_json) */
  xml?: string;
}

export interface ConvertResult {
  direction: ConvertDirection;
  /** UBL XML string for json_to_ubl, parsed object for ubl_to_json */
  result: string | Record<string, unknown>;
}

// ---------------------------------------------------------------------------
// Peppol
// ---------------------------------------------------------------------------

export interface SmpParticipantCapability {
  documentTypeId: string;
  processId: string;
  transportProfile: string;
}

export interface PeppolParticipant {
  peppolId: string;
  name: string | null;
  country: string | null;
  capabilities: SmpParticipantCapability[];
}

export interface DirectorySearchParams {
  q?: string;
  country?: string;
  page?: number;
  page_size?: number;
}

export interface DirectoryEntry {
  peppolId: string;
  name: string;
  country: string;
  registeredAt: string | null;
}

export interface DirectorySearchResult {
  results: DirectoryEntry[];
  total: number;
  page: number;
  page_size: number;
}

export interface CompanyLookup {
  ico: string;
  name: string;
  dic: string | null;
  icDph: string | null;
  address?: PartyAddress;
  peppolId: string | null;
}

// ---------------------------------------------------------------------------
// Firms
// ---------------------------------------------------------------------------

export interface FirmSummary {
  id: string;
  name: string;
  ico: string | null;
  peppolId: string | null;
  peppolStatus: string;
}

export interface FirmPeppolIdentifier {
  scheme: string;
  identifier: string;
}

export interface FirmDetail extends FirmSummary {
  dic: string | null;
  icDph: string | null;
  address?: PartyAddress;
  peppolIdentifiers: FirmPeppolIdentifier[];
  createdAt: string;
}

export interface FirmsListResponse {
  firms: FirmSummary[];
}

export interface FirmDocumentsParams {
  offset?: number;
  limit?: number;
  direction?: DocumentDirection;
}

export interface PeppolIdentifierResponse {
  peppolId: string;
  scheme: string;
  identifier: string;
  registeredAt: string;
}

// ---------------------------------------------------------------------------
// Firm assignment (integrator)
// ---------------------------------------------------------------------------

export interface AssignFirmRequest {
  /** Slovak ICO (8 digits) */
  ico: string;
}

export interface AssignFirmResponse {
  firm: {
    id: string;
    name: string | null;
    ico: string | null;
    peppol_id: string | null;
    peppol_status: string;
  };
  status: "active";
}

export interface BatchAssignFirmsRequest {
  /** Array of Slovak ICOs (8 digits each, max 50) */
  icos: string[];
}

export interface BatchAssignFirmResult {
  ico: string;
  firm?: {
    id: string;
    name: string | null;
    ico: string | null;
    peppol_id: string | null;
    peppol_status: string;
  };
  status?: "active" | "already_assigned";
  error?: string;
  message?: string;
}

export interface BatchAssignFirmsResponse {
  results: BatchAssignFirmResult[];
}

// ---------------------------------------------------------------------------
// Webhooks
// ---------------------------------------------------------------------------

export interface CreateWebhookRequest {
  url: string;
  events?: WebhookEvent[];
}

export interface UpdateWebhookRequest {
  url?: string;
  events?: WebhookEvent[];
  isActive?: boolean;
}

export interface Webhook {
  id: string;
  url: string;
  events: WebhookEvent[];
  isActive: boolean;
  createdAt: string;
}

export interface WebhookDetail extends Webhook {
  /** HMAC-SHA256 signing secret — only returned on creation */
  secret?: string;
}

export interface WebhookDelivery {
  id: string;
  webhookId: string;
  event: string;
  status: string;
  attempts: number;
  responseStatus: number | null;
  createdAt: string;
}

export interface WebhookWithDeliveries extends Webhook {
  deliveries: WebhookDelivery[];
}

export interface WebhookListResponse {
  data: Webhook[];
}

// ---------------------------------------------------------------------------
// Webhook pull queue
// ---------------------------------------------------------------------------

export interface WebhookQueueParams {
  /** Max items to return (1–100, default 20) */
  limit?: number;
  /** Filter by event type, e.g. 'document.received' */
  event_type?: string;
}

export interface WebhookQueueItem {
  id: string;
  type: string;
  created_at: string;
  payload: Record<string, unknown>;
}

export interface WebhookQueueResponse {
  items: WebhookQueueItem[];
  has_more: boolean;
}

// ---------------------------------------------------------------------------
// Webhook queue all (integrator — cross-firm)
// ---------------------------------------------------------------------------

export interface WebhookQueueAllParams {
  /** Max items to return (1–500, default 100) */
  limit?: number;
  /** ISO 8601 timestamp — only return events created after this date */
  since?: string;
}

export interface WebhookQueueAllEvent {
  event_id: string;
  firm_id: string;
  event: string;
  payload: Record<string, unknown>;
  created_at: string;
}

export interface WebhookQueueAllResponse {
  events: WebhookQueueAllEvent[];
  count: number;
}

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

export interface StatisticsParams {
  from?: string;
  to?: string;
}

export interface Statistics {
  period: { from: string; to: string };
  outbound: { total: number; delivered: number; failed: number };
  inbound: { total: number; acknowledged: number; pending: number };
}

// ---------------------------------------------------------------------------
// Account
// ---------------------------------------------------------------------------

export interface Account {
  firm: {
    name: string;
    ico: string | null;
    peppolId: string | null;
    peppolStatus: string;
  };
  plan: {
    name: string;
    status: "active" | "expired";
  };
  usage: {
    outbound: number;
    inbound: number;
  };
}

// ---------------------------------------------------------------------------
// Extract
// ---------------------------------------------------------------------------

export interface ExtractResult {
  extraction: Record<string, unknown>;
  ubl_xml: string;
  confidence: number;
  file_name: string;
}

export interface BatchExtractItem {
  file_name: string;
  extraction?: Record<string, unknown>;
  ubl_xml?: string;
  confidence?: string;
  error?: string;
}

export interface BatchExtractResult {
  batch_id: string;
  total: number;
  successful: number;
  failed: number;
  results: BatchExtractItem[];
}
