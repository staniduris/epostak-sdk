// Main client
export { EPostak } from "./client.js";
export type { EPostakConfig } from "./client.js";

// Error class
export { EPostakError } from "./utils/errors.js";

// Webhook signature verification helper
export { verifyWebhookSignature } from "./utils/webhook-signature.js";
export type {
  VerifyWebhookSignatureOptions,
  VerifyWebhookSignatureResult,
} from "./utils/webhook-signature.js";

// Token manager (advanced — most users don't need this)
export { TokenManager } from "./utils/token-manager.js";

// OAuth authorization_code + PKCE helpers (integrator-initiated onboarding)
export { OAuth } from "./resources/oauth.js";

// Resource classes (for typing and instanceof checks)
export { AuthResource, IpAllowlistResource } from "./resources/auth.js";
export { AuditResource } from "./resources/audit.js";
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
export {
  IntegratorResource,
  IntegratorLicensesResource,
} from "./resources/integrator.js";

// All types
export type {
  // Primitives
  InvoiceResponseCode,
  WebhookEvent,
  DocumentDirection,
  ConvertInputFormat,
  ConvertOutputFormat,
  InboxStatus,
  ValidateFormat,
  ReportingPeriod,
  WebhookDeliveryStatus,
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
  DocumentAttachment,
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
  DocumentStatusValidation,
  DocumentEvidenceResponse,
  InvoiceRespondRequest,
  InvoiceRespondResponse,
  // Validate / preflight / convert
  ValidateDocumentRequest,
  ValidationResult,
  ValidationFinding,
  PreflightRequest,
  PreflightResult,
  ConvertRequest,
  ConvertResult,
  // Peppol
  PeppolAccessPoint,
  PeppolParticipant,
  DirectorySearchParams,
  DirectoryEntry,
  DirectorySearchResult,
  CompanyLookup,
  // Firms
  FirmSummary,
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
  WebhookRotateSecretResponse,
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
  StatisticsTopParty,
  Statistics,
  // Account
  Account,
  // Integrator billing aggregate
  IntegratorLicenseInfo,
  IntegratorLicenseInfoParams,
  IntegratorPricingTier,
  IntegratorBillableUsage,
  IntegratorNonManagedUsage,
  IntegratorFirmUsage,
  // Extract
  ExtractResult,
  BatchExtractItem,
  BatchExtractResult,
  // Auth — OAuth + introspection + rotation + IP allowlist
  TokenResponse,
  RevokeResponse,
  AuthStatusKey,
  AuthStatusPlan,
  AuthStatusRateLimit,
  AuthStatusIntegrator,
  AuthStatusResponse,
  RotateSecretResponse,
  IpAllowlistResponse,
  // Cursor pagination
  CursorPage,
  // Audit
  AuditActorType,
  AuditEvent,
  AuditListParams,
  // Batch send
  BatchSendItem,
  BatchSendResult,
  BatchSendResponse,
  // Parse UBL
  ParsedUblDocument,
  // Mark document state
  DocumentMarkState,
  MarkDocumentRequest,
  MarkDocumentResponse,
  // Peppol capabilities
  PeppolCapabilitiesRequest,
  PeppolCapabilitiesResponse,
  // Batch participant lookup
  BatchLookupParticipant,
  BatchLookupRequest,
  BatchLookupResult,
  BatchLookupResponse,
  // Public validator
  PublicValidationMessage,
  PublicValidationReport,
  // Outbox
  OutboxParams,
  OutboxListResponse,
  OutboxDocument,
  // Invoice responses list
  InvoiceResponseItem,
  InvoiceResponsesListResponse,
  // Document events
  DocumentEventsParams,
  DocumentEvent,
  DocumentEventsResponse,
} from "./types.js";
