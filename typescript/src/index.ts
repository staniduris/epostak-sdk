// Main client
export { EPostak } from "./client.js";
export type { EPostakConfig } from "./client.js";

// Error classes
export {
  EPostakError,
  DuplicateInvoiceNumberError,
  UblValidationError,
} from "./utils/errors.js";
export type {
  DuplicateInvoiceRecipient,
  DuplicateInvoiceExistingDocument,
} from "./utils/errors.js";

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
export { BoxResource } from "./resources/box.js";
export {
  ConnectorAdvancedResource,
  ConnectorAdvancedDocumentsResource,
  ConnectorDocumentsResource,
  ConnectorCustomerAdvancedResource,
  ConnectorCustomerDocumentsResource,
  ConnectorCustomerEventsResource,
  ConnectorCustomerMailboxResource,
  ConnectorCustomerResource,
  ConnectorCustomersResource,
  ConnectorWebhookResource,
  ConnectorOutboxResource,
  ConnectorResource,
} from "./resources/connector.js";
export { EnterprisePullResource, EnterpriseResource } from "./resources/enterprise.js";
export { DocumentsResource, InboxResource } from "./resources/documents.js";
export { EventsResource } from "./resources/events.js";
export { FirmsResource } from "./resources/firms.js";
export { PeppolResource, PeppolDirectoryResource } from "./resources/peppol.js";
export {
  WebhooksResource,
  WebhookQueueResource,
} from "./resources/webhooks.js";
export { ReportingResource } from "./resources/reporting.js";
export { ExtractResource } from "./resources/extract.js";
export { PayloadsResource } from "./resources/payloads.js";
export { AccountResource } from "./resources/account.js";
export {
  IntegratorResource,
  IntegratorKeysResource,
  IntegratorLicensesResource,
} from "./resources/integrator.js";
export { InboundResource } from "./resources/inbound.js";
export { OutboundResource } from "./resources/outbound.js";
export {
  SapiParticipantDocumentsResource,
  SapiParticipantResource,
  SapiParticipantsResource,
  SapiResource,
} from "./resources/sapi.js";
export type { SapiParticipantOptions, SapiSendOptions } from "./resources/sapi.js";

// All types
export type {
  // Primitives
  InvoiceResponseCode,
  WebhookEvent,
  WebhookPayload,
  WebhookPayloadEnvelope,
  WebhookPayloadData,
  DocumentDirection,
  ConvertInputFormat,
  ConvertOutputFormat,
  InboxStatus,
  ConnectorOutcome,
  ConnectorPreflightRequest,
  ConnectorRepairItem,
  ConnectorSafeFix,
  ConnectorRepairReport,
  ConnectorPreflightResponse,
  ConnectorSendRequest,
  ConnectorSendResponse,
  ConnectorEvent,
  ConnectorBusinessEvent,
  ConnectorBusinessEventData,
  ConnectorStatusResponse,
  ConnectorInboxDocument,
  ConnectorInboxListParams,
  ConnectorInboxListResponse,
  ConnectorAckResponse,
  ConnectorEventsParams,
  ConnectorEventsResponse,
  ConnectorBusinessEventsResponse,
  ConnectorOutboxStatus,
  ConnectorOutboxStageItem,
  ConnectorOutboxStageRequest,
  ConnectorOutboxItem,
  ConnectorOutboxStageResponse,
  ConnectorOutboxListParams,
  ConnectorOutboxListResponse,
  ConnectorOutboxSendOptions,
  ConnectorOutboxBatchSendRequest,
  ConnectorOutboxBatchSendResponse,
  BoxStatus,
  BoxDirection,
  BoxListParams,
  BoxRetention,
  BoxLastError,
  BoxItem,
  BoxItemDetail,
  BoxListResponse,
  BoxCreateRequest,
  BoxScheduleRequest,
  ConnectorSubmitDocumentRequest,
  ConnectorMapperPreviewRequest,
  ConnectorBusinessDocumentType,
  ConnectorBusinessState,
  ConnectorBusinessRecipient,
  ConnectorBusinessLine,
  ConnectorBusinessPrepayment,
  ConnectorBusinessAttachment,
  ConnectorBusinessDocumentRequest,
  ConnectorBusinessParty,
  ConnectorBusinessInvoiceResponse,
  ConnectorBusinessDocument,
  ConnectorBusinessDocumentListParams,
  ConnectorBusinessDocumentListResponse,
  ConnectorBusinessAcknowledgeResponse,
  ConnectorInvoiceResponseStatus,
  ConnectorInvoiceResponseRequest,
  ConnectorInvoiceResponseResult,
  ConnectorWebhookConfiguration,
  ConnectorWebhook,
  ConnectorWebhookDelivery,
  ConnectorWebhookDeliveriesParams,
  ConnectorWebhookDeliveriesResponse,
  ConnectorWebhookEvent,
  ConnectorWebhookTestEvent,
  ConnectorWebhookTestResponse,
  ConnectorWebhookDiagnosisCode,
  ConnectorWebhookTestScenario,
  ConnectorWebhookDeliveryAttempt,
  ConnectorWebhookDeliveryDetail,
  ConnectorWebhookReplayResult,
  ConnectorWebhookTestSuiteRequest,
  ConnectorWebhookTestSuiteAccepted,
  ConnectorWebhookTestSuiteStatus,
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
  DocumentStatusBatchResult,
  DocumentStatusBatchResponse,
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
  PeppolRoutingStatus,
  PeppolAccessPoint,
  PeppolCertificateInfo,
  PeppolParticipant,
  DirectorySearchParams,
  DirectoryEntry,
  DirectorySearchResult,
  CompanyLookup,
  PeppolResolveParams,
  PeppolResolveResponse,
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
  WebhookDeadLetterParams,
  WebhookDeadLetter,
  WebhookDeadLetterResponse,
  WebhookDeadLetterReplayResponse,
  WebhookDeadLetterResolveResponse,
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
  ReportingSubmissionsParams,
  ReportingSubmission,
  ReportingSubmissionsResponse,
  // Account
  Account,
  // Integrator billing aggregate
  IntegratorLicenseInfo,
  IntegratorLicenseInfoParams,
  IntegratorKey,
  IntegratorKeysResponse,
  DeactivateIntegratorKeyRequest,
  DeactivateIntegratorKeyResponse,
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
  // Rate-limit
  RateLimitState,
  // UBL validation
  UblRule,
  // Pull API — Inbound
  PeppolParty,
  InboundDocumentAck,
  InboundDocument,
  InboundListParams,
  InboundDocumentsListResponse,
  InboundAckRequest,
  // Pull API — Outbound
  OutboundDocumentAttempt,
  OutboundDocument as PullOutboundDocument,
  OutboundListParams,
  OutboundDocumentsListResponse,
  OutboundEvent,
  OutboundEventsParams,
  OutboundEventsListResponse,
  SapiDocumentMetadata,
  SapiSendDocumentRequest,
  SapiSendDocumentResponse,
  SapiDocumentListParams,
  SapiDocumentListItem,
  SapiDocumentListResponse,
  SapiDocumentDetail,
  SapiAcknowledgeResponse,
} from "./types.js";
