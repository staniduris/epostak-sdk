// Main client
export { EPostak } from "./client.js";
export type { EPostakConfig } from "./client.js";

// Error class
export { EPostakError } from "./utils/errors.js";

// Resource classes (for typing and instanceof checks)
export { DocumentsResource, InboxResource } from "./resources/documents.js";
export { FirmsResource } from "./resources/firms.js";
export { PeppolResource, PeppolDirectoryResource } from "./resources/peppol.js";
export {
  WebhooksResource,
  WebhookQueueResource,
} from "./resources/webhooks.js";
export { ReportingResource } from "./resources/reporting.js";
export { ExtractResource } from "./resources/extract.js";
export { AccountResource } from "./resources/account.js";

// All types
export type {
  // Primitives
  InvoiceResponseCode,
  WebhookEvent,
  DocumentDirection,
  ConvertDirection,
  InboxStatus,
  // Line items
  LineItem,
  LineItemResponse,
  // Party
  PartyAddress,
  Party,
  // Send document
  SendDocumentJsonRequest,
  SendDocumentXmlRequest,
  SendDocumentRequest,
  SendDocumentResponse,
  // Update document
  UpdateDocumentRequest,
  // Document
  DocumentTotals,
  InboxDocument,
  // Inbox
  InboxListParams,
  InboxListResponse,
  InboxDocumentDetailResponse,
  AcknowledgeResponse,
  // Inbox all (integrator)
  InboxAllParams,
  InboxAllDocument,
  InboxAllResponse,
  // Document lifecycle
  StatusHistoryEntry,
  DocumentStatusResponse,
  DocumentEvidenceResponse,
  InvoiceRespondRequest,
  InvoiceRespondResponse,
  // Validate / preflight / convert
  ValidationResult,
  PreflightRequest,
  PreflightResult,
  ConvertRequest,
  ConvertResult,
  // Peppol
  SmpParticipantCapability,
  PeppolParticipant,
  DirectorySearchParams,
  DirectoryEntry,
  DirectorySearchResult,
  CompanyLookup,
  // Firms
  FirmSummary,
  FirmPeppolIdentifier,
  FirmDetail,
  FirmsListResponse,
  FirmDocumentsParams,
  PeppolIdentifierResponse,
  // Firm assignment (integrator)
  AssignFirmRequest,
  AssignFirmResponse,
  BatchAssignFirmsRequest,
  BatchAssignFirmResult,
  BatchAssignFirmsResponse,
  // Webhooks
  CreateWebhookRequest,
  UpdateWebhookRequest,
  Webhook,
  WebhookDetail,
  WebhookDelivery,
  WebhookWithDeliveries,
  WebhookListResponse,
  // Webhook test & delivery history
  WebhookTestResponse,
  WebhookDeliveryDetail,
  WebhookDeliveriesParams,
  WebhookDeliveriesResponse,
  // Webhook queue
  WebhookQueueParams,
  WebhookQueueItem,
  WebhookQueueResponse,
  // Webhook queue all (integrator)
  WebhookQueueAllParams,
  WebhookQueueAllEvent,
  WebhookQueueAllResponse,
  // Reporting
  StatisticsParams,
  Statistics,
  // Account
  Account,
  // Extract
  ExtractResult,
  BatchExtractItem,
  BatchExtractResult,
} from "./types.js";
