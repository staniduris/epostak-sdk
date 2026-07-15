import { createHash } from "node:crypto";
import { BaseResource, buildQuery } from "../utils/request.js";
import { verifyWebhookSignature } from "../utils/webhook-signature.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  ConnectorAckResponse,
  ConnectorActionRequest,
  ConnectorActionResponse,
  ConnectorAutopilotRequest,
  ConnectorAutopilotRunResponse,
  ConnectorBusinessAcknowledgeResponse,
  ConnectorBusinessDocument,
  ConnectorBusinessDocumentRequest,
  ConnectorBusinessDocumentListParams,
  ConnectorBusinessDocumentListResponse,
  ConnectorBusinessEventsResponse,
  ConnectorWebhookConfiguration,
  ConnectorWebhookDeliveriesParams,
  ConnectorWebhookDeliveriesResponse,
  ConnectorWebhookEvent,
  ConnectorWebhookTestResponse,
  ConnectorEventsParams,
  ConnectorEventsResponse,
  ConnectorInboxDocument,
  ConnectorInboxListParams,
  ConnectorInboxListResponse,
  ConnectorInvoiceResponseRequest,
  ConnectorInvoiceResponseResult,
  ConnectorMailboxListResponse,
  ConnectorMailboxRepairRequest,
  ConnectorMailboxUpdateResponse,
  ConnectorMapperRequest,
  ConnectorMapperPreviewRequest,
  ConnectorMapperResponse,
  ConnectorOutboxBatchSendRequest,
  ConnectorOutboxBatchSendResponse,
  ConnectorOutboxItem,
  ConnectorOutboxListParams,
  ConnectorOutboxListResponse,
  ConnectorOutboxSendOptions,
  ConnectorOutboxStageRequest,
  ConnectorOutboxStageResponse,
  ConnectorPreflightRequest,
  ConnectorPreflightResponse,
  ConnectorReconcileParams,
  ConnectorReconcileResponse,
  ConnectorSendRequest,
  ConnectorSendResponse,
  ConnectorSendPolicyOptions,
  ConnectorStatusResponse,
  ConnectorSyncParams,
  ConnectorSyncResponse,
  ConnectorSubmitDocumentRequest,
  ConnectorZenInputRequest,
  WebhookRotateSecretResponse,
} from "../types.js";

/**
 * Connector outbox lets an ERP stage invoices now and send them later.
 */
export class ConnectorOutboxResource extends BaseResource {
  /**
   * Stage one or more ERP invoices without immediate Peppol delivery.
   */
  stage(
    body: ConnectorOutboxStageRequest,
  ): Promise<ConnectorOutboxStageResponse> {
    return this.request("POST", "/connector/outbox", body);
  }

  /**
   * List staged Connector outbox items, optionally filtered by status.
   */
  list(
    params?: ConnectorOutboxListParams,
  ): Promise<ConnectorOutboxListResponse> {
    return this.request(
      "GET",
      `/connector/outbox${buildQuery({
        status: params?.status,
        limit: params?.limit,
        offset: params?.offset,
      })}`,
    );
  }

  /**
   * Retrieve a single staged outbox item.
   */
  get(outboxId: string): Promise<ConnectorOutboxItem> {
    return this.request(
      "GET",
      `/connector/outbox/${encodeURIComponent(outboxId)}`,
    );
  }

  /**
   * Send one staged item through the Connector send workflow.
   */
  send(
    outboxId: string,
    options?: ConnectorOutboxSendOptions,
  ): Promise<ConnectorOutboxItem> {
    return this.request(
      "POST",
      `/connector/outbox/${encodeURIComponent(outboxId)}/send`,
      options ?? {},
      { retry: true },
    );
  }

  /**
   * Send ready, failed, or due scheduled outbox items in a batch.
   */
  sendBatch(
    body: ConnectorOutboxBatchSendRequest = {},
  ): Promise<ConnectorOutboxBatchSendResponse> {
    return this.request("POST", "/connector/outbox/send", body);
  }

  /**
   * Cancel a staged item before it is sent.
   */
  cancel(outboxId: string): Promise<ConnectorOutboxItem> {
    return this.request(
      "DELETE",
      `/connector/outbox/${encodeURIComponent(outboxId)}`,
    );
  }
}

function withCustomerRef<T extends { customerRef?: string }>(
  customerRef: string,
  body: T,
): T & { customerRef: string } {
  if (body.customerRef && normalizeConnectorIdentity(body.customerRef) !== customerRef) {
    throw new Error("Connector customerRef conflicts with scoped customer");
  }
  return { ...body, customerRef };
}

/** ECMAScript TrimString, matching the backend identity normalization contract. */
function normalizeConnectorIdentity(value: string): string {
  return value.trim();
}

function assertConnectorDocument(body: ConnectorBusinessDocumentRequest): void {
  if (!body.externalId?.trim()) {
    throw new Error("Connector externalId is required");
  }
  if (!body.number?.trim()) {
    throw new Error("Connector number is required");
  }
  if (!body.recipient?.country?.trim()) {
    throw new Error("Connector recipient.country is required");
  }
  if (
    ![
      body.recipient.companyId,
      body.recipient.taxId,
      body.recipient.vatId,
      body.recipient.networkId,
    ].some((value) => value?.trim())
  ) {
    throw new Error(
      "Connector recipient requires companyId, taxId, vatId, or networkId",
    );
  }
  if (!Array.isArray(body.lines) || body.lines.length === 0) {
    throw new Error("Connector lines must contain at least one item");
  }
}

/**
 * Stable, bounded key for the `(customerRef, externalId)` tuple. Lengths are
 * UTF-8 byte lengths encoded as unsigned 32-bit big-endian integers.
 */
function connectorDocumentIdempotencyKey(
  customerRef: string,
  externalId: string,
): string {
  const customer = Buffer.from(normalizeConnectorIdentity(customerRef), "utf8");
  const external = Buffer.from(normalizeConnectorIdentity(externalId), "utf8");
  const tuple = Buffer.allocUnsafe(8 + customer.length + external.length);
  tuple.writeUInt32BE(customer.length, 0);
  customer.copy(tuple, 4);
  tuple.writeUInt32BE(external.length, 4 + customer.length);
  external.copy(tuple, 8 + customer.length);
  return `connector:v1:${createHash("sha256").update(tuple).digest("hex")}`;
}

function validateConnectorIdempotencyKey(value: string): string {
  const byteLength = Buffer.byteLength(value, "utf8");
  if (!normalizeConnectorIdentity(value) || byteLength > 255) {
    throw new Error("Connector idempotency key must be 1-255 UTF-8 bytes");
  }
  return value;
}

export class ConnectorDocumentsResource {
  constructor(protected readonly parent: ConnectorResource) {}

  get(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocument(documentId);
  }

  respond(
    documentId: string,
    customerRef: string,
    body: ConnectorInvoiceResponseRequest,
  ): Promise<ConnectorInvoiceResponseResult> {
    return this.parent.respondDocument(documentId, customerRef, body);
  }

  ubl(documentId: string): Promise<string> {
    return this.parent.getDocumentUbl(documentId);
  }

  evidence(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentEvidence(documentId);
  }

  evidenceBundle(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentEvidenceBundle(documentId);
  }

  supportPacket(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentSupportPacket(documentId);
  }
}

export class ConnectorAdvancedDocumentsResource {
  constructor(
    private readonly parent: ConnectorResource,
    private readonly customerRef?: string,
  ) {}

  ubl(documentId: string): Promise<string> {
    return this.parent.getDocumentUbl(documentId, this.customerRef);
  }

  evidence(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentEvidence(documentId, this.customerRef);
  }

  evidenceBundle(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentEvidenceBundle(documentId, this.customerRef);
  }

  supportPacket(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentSupportPacket(documentId, this.customerRef);
  }
}

export class ConnectorCustomerDocumentsResource extends ConnectorDocumentsResource {
  constructor(
    parent: ConnectorResource,
    private readonly customerRef: string,
  ) {
    super(parent);
  }

  send(
    body: ConnectorBusinessDocumentRequest,
    options?: { idempotencyKey?: string },
  ): Promise<ConnectorBusinessDocument> {
    return this.parent.submitCustomerDocument(
      this.customerRef,
      "send",
      body,
      options,
    );
  }

  stage(
    body: ConnectorBusinessDocumentRequest,
    options?: { idempotencyKey?: string },
  ): Promise<ConnectorBusinessDocument> {
    return this.parent.submitCustomerDocument(
      this.customerRef,
      "stage",
      body,
      options,
    );
  }

  list(
    params?: ConnectorBusinessDocumentListParams,
  ): Promise<ConnectorBusinessDocumentListResponse> {
    return this.parent.listCustomerDocuments(this.customerRef, params);
  }

  get(documentId: string): Promise<ConnectorBusinessDocument> {
    return this.parent.getDocument(documentId, this.customerRef);
  }

  acknowledge(
    documentId: string,
    reference: string,
  ): Promise<ConnectorBusinessAcknowledgeResponse> {
    return this.parent.acknowledgeDocument(documentId, reference, this.customerRef);
  }

  respond(
    documentId: string,
    body: ConnectorInvoiceResponseRequest,
  ): Promise<ConnectorInvoiceResponseResult>;
  respond(
    documentId: string,
    customerRef: string,
    body: ConnectorInvoiceResponseRequest,
  ): Promise<ConnectorInvoiceResponseResult>;
  respond(
    documentId: string,
    customerRefOrBody: string | ConnectorInvoiceResponseRequest,
    body?: ConnectorInvoiceResponseRequest,
  ): Promise<ConnectorInvoiceResponseResult> {
    if (typeof customerRefOrBody === "string") {
      if (!body) throw new Error("Connector response body is required");
      const suppliedCustomerRef = normalizeConnectorIdentity(customerRefOrBody);
      if (suppliedCustomerRef !== this.customerRef) {
        throw new Error("Connector customerRef conflicts with scoped customer");
      }
      return this.parent.respondDocument(documentId, this.customerRef, body);
    }
    return this.parent.respondDocument(
      documentId,
      this.customerRef,
      customerRefOrBody,
    );
  }

  /** Send a previously staged customer document. */
  sendDocument(documentId: string): Promise<ConnectorBusinessDocument> {
    return this.parent.sendCustomerDocument(documentId, this.customerRef);
  }

  /** Cancel a staged customer document before delivery starts. */
  cancelDocument(documentId: string): Promise<ConnectorBusinessDocument> {
    return this.parent.cancelCustomerDocument(documentId, this.customerRef);
  }

  ubl(documentId: string): Promise<string> {
    return this.parent.getDocumentUbl(documentId, this.customerRef);
  }

  evidence(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentEvidence(documentId, this.customerRef);
  }

  evidenceBundle(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentEvidenceBundle(documentId, this.customerRef);
  }

  supportPacket(documentId: string): Promise<Record<string, unknown>> {
    return this.parent.getDocumentSupportPacket(documentId, this.customerRef);
  }
}

export class ConnectorCustomerEventsResource {
  constructor(
    private readonly parent: ConnectorResource,
    private readonly customerRef: string,
  ) {}

  list(
    params?: ConnectorEventsParams,
  ): Promise<ConnectorBusinessEventsResponse> {
    return this.parent.listCustomerEvents(this.customerRef, params);
  }
}

/** One global Connector webhook per integrator. */
export class ConnectorWebhookResource extends BaseResource {
  /** Verify `sha256(timestamp + "." + rawBody)` with the shared webhook helper. */
  readonly verifySignature = verifyWebhookSignature;

  get(): Promise<ConnectorWebhookConfiguration> {
    return this.request("GET", "/connector/webhook", undefined, {
      omitFirmId: true,
    });
  }

  configure(
    url: string,
    events?: ConnectorWebhookEvent[],
  ): Promise<ConnectorWebhookConfiguration> {
    if (!url.trim()) throw new Error("Connector webhook URL is required");
    return this.request(
      "PUT",
      "/connector/webhook",
      { url: url.trim(), ...(events ? { events } : {}) },
      { omitFirmId: true },
    );
  }

  delete(): Promise<void> {
    return this.request("DELETE", "/connector/webhook", undefined, {
      omitFirmId: true,
    });
  }

  rotateSecret(): Promise<WebhookRotateSecretResponse> {
    return this.request(
      "POST",
      "/connector/webhook/rotate-secret",
      undefined,
      { omitFirmId: true },
    );
  }

  test(customerRef: string): Promise<ConnectorWebhookTestResponse> {
    const normalized = normalizeConnectorIdentity(customerRef);
    if (!normalized) throw new Error("Connector customerRef is required");
    return this.request(
      "POST",
      "/connector/webhook/test",
      { customerRef: normalized },
      { omitFirmId: true },
    );
  }

  deliveries(
    params?: ConnectorWebhookDeliveriesParams,
  ): Promise<ConnectorWebhookDeliveriesResponse> {
    return this.request(
      "GET",
      `/connector/webhook/deliveries${buildQuery({
        cursor: params?.cursor,
        limit: params?.limit,
        status: params?.status?.toUpperCase(),
      })}`,
      undefined,
      { omitFirmId: true },
    );
  }
}

export class ConnectorCustomerMailboxResource {
  constructor(
    private readonly parent: ConnectorResource,
    private readonly customerRef: string,
  ) {}

  repair(): Promise<Record<string, unknown>> {
    return this.parent.repairMailbox({ customerRef: this.customerRef });
  }

  updateSendPolicy(
    body: ConnectorSendPolicyOptions,
  ): Promise<ConnectorMailboxUpdateResponse> {
    return this.parent.updateMailboxSendPolicy(this.customerRef, body);
  }
}

/**
 * Advanced customer workflow helpers.
 *
 * New integrations should start with `customer.documents` and
 * `customer.events`. Mapper is the supported preview/normalization helper;
 * the remaining members are supported legacy compatibility wrappers.
 */
export class ConnectorCustomerAdvancedResource {
  readonly documents: ConnectorAdvancedDocumentsResource;
  readonly mailbox: ConnectorCustomerMailboxResource;

  constructor(
    private readonly parent: ConnectorResource,
    private readonly customerRef: string,
  ) {
    this.documents = new ConnectorAdvancedDocumentsResource(parent, customerRef);
    this.mailbox = new ConnectorCustomerMailboxResource(parent, customerRef);
  }

  autopilot(
    body: Omit<ConnectorAutopilotRequest, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.parent.advanced.autopilot(
      withCustomerRef(this.customerRef, body),
    );
  }

  mapper(
    body: ConnectorMapperPreviewRequest,
  ): Promise<ConnectorMapperResponse> {
    if (body.execute !== undefined && body.execute !== "preview") {
      throw new Error("Connector Mapper only supports preview normalization");
    }
    return this.parent.advanced.mapper(
      withCustomerRef(this.customerRef, { ...body, execute: "preview" }),
    );
  }

  zenInput(
    body: Omit<ConnectorZenInputRequest, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.parent.advanced.zenInput(
      withCustomerRef(this.customerRef, body),
    );
  }

  sync(
    params?: Omit<ConnectorSyncParams, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorSyncResponse> {
    return this.parent.advanced.sync(
      withCustomerRef(this.customerRef, params ?? {}),
    );
  }
}

export class ConnectorCustomerResource {
  readonly documents: ConnectorCustomerDocumentsResource;
  readonly events: ConnectorCustomerEventsResource;
  readonly advanced: ConnectorCustomerAdvancedResource;
  readonly mailbox: ConnectorCustomerMailboxResource;

  constructor(
    parent: ConnectorResource,
    customerRef: string,
  ) {
    this.documents = new ConnectorCustomerDocumentsResource(parent, customerRef);
    this.events = new ConnectorCustomerEventsResource(parent, customerRef);
    this.advanced = new ConnectorCustomerAdvancedResource(parent, customerRef);
    this.mailbox = this.advanced.mailbox;
  }

  submitDocument(
    body: ConnectorSubmitDocumentRequest,
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.advanced.autopilot({ ...body, mode: body.mode ?? "stage" });
  }

  autopilot(
    body: Omit<ConnectorAutopilotRequest, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.advanced.autopilot(body);
  }

  mapper(
    body: Omit<ConnectorMapperRequest, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorMapperResponse> {
    return this.advanced.mapper(body);
  }

  zenInput(
    body: Omit<ConnectorZenInputRequest, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.advanced.zenInput(body);
  }

  sync(
    params?: Omit<ConnectorSyncParams, "customerRef"> & { customerRef?: string },
  ): Promise<ConnectorSyncResponse> {
    return this.advanced.sync(params);
  }
}

export class ConnectorCustomersResource {
  constructor(private readonly parent: ConnectorResource) {}

  for(customerRef: string): ConnectorCustomerResource {
    if (!customerRef.trim()) throw new Error("Connector customerRef is required");
    return new ConnectorCustomerResource(this.parent, normalizeConnectorIdentity(customerRef));
  }
}

/**
 * Advanced and legacy Connector workflows.
 *
 * These endpoints expose protocol-oriented staging, preflight, mailbox, and
 * repair controls. Most ERP integrations only need
 * `connector.customers.for(customerRef).documents` and `.events`.
 */
export class ConnectorAdvancedResource {
  readonly outbox: ConnectorOutboxResource;
  readonly documents: ConnectorAdvancedDocumentsResource;

  constructor(
    private readonly parent: ConnectorResource,
    config: ClientConfig,
  ) {
    this.outbox = new ConnectorOutboxResource(config);
    this.documents = new ConnectorAdvancedDocumentsResource(parent);
  }

  preflight(body: ConnectorPreflightRequest): Promise<ConnectorPreflightResponse> {
    return this.parent.preflight(body);
  }

  send(
    body: ConnectorSendRequest,
    options?: { idempotencyKey?: string },
  ): Promise<ConnectorSendResponse> {
    return this.parent.send(body, options);
  }

  status(documentId: string): Promise<ConnectorStatusResponse> {
    return this.parent.status(documentId);
  }

  inbox(params?: ConnectorInboxListParams): Promise<ConnectorInboxListResponse> {
    return this.parent.inbox(params);
  }

  getInboxDocument(documentId: string): Promise<ConnectorInboxDocument> {
    return this.parent.getInboxDocument(documentId);
  }

  ack(documentId: string): Promise<ConnectorAckResponse> {
    return this.parent.ack(documentId);
  }

  events(params?: ConnectorEventsParams): Promise<ConnectorEventsResponse> {
    return this.parent.events(params);
  }

  autopilot(
    body: ConnectorAutopilotRequest,
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.parent.autopilot(body);
  }

  mapper(body: ConnectorMapperRequest): Promise<ConnectorMapperResponse> {
    return this.parent.mapper(body);
  }

  zenInput(body: ConnectorZenInputRequest): Promise<ConnectorAutopilotRunResponse> {
    return this.parent.zenInput(body);
  }

  getAutopilotRun(autopilotId: string): Promise<ConnectorAutopilotRunResponse> {
    return this.parent.getAutopilotRun(autopilotId);
  }

  sendAutopilotRun(autopilotId: string): Promise<ConnectorAutopilotRunResponse> {
    return this.parent.sendAutopilotRun(autopilotId);
  }

  reconcile(params?: ConnectorReconcileParams): Promise<ConnectorReconcileResponse> {
    return this.parent.reconcile(params);
  }

  mailboxes(): Promise<ConnectorMailboxListResponse> {
    return this.parent.mailboxes();
  }

  repairMailbox(
    body: ConnectorMailboxRepairRequest = {},
  ): Promise<Record<string, unknown>> {
    return this.parent.repairMailbox(body);
  }

  updateMailboxSendPolicy(
    customerRef: string,
    body: ConnectorSendPolicyOptions,
  ): Promise<ConnectorMailboxUpdateResponse> {
    return this.parent.updateMailboxSendPolicy(customerRef, body);
  }

  sync(params?: ConnectorSyncParams): Promise<ConnectorSyncResponse> {
    return this.parent.sync(params);
  }

  runAction(
    actionId: string,
    body: ConnectorActionRequest = {},
  ): Promise<ConnectorActionResponse> {
    return this.parent.runAction(actionId, body);
  }
}

/**
 * Connector workflow endpoints for ERP teams.
 *
 * Connector is a distinct managed ERP product. Customer-scoped calls resolve
 * approved links by customerRef and omit X-Firm-Id. Legacy firm-scoped methods
 * remain supported compatibility aliases.
 */
export class ConnectorResource extends BaseResource {
  /** Advanced protocol and compatibility workflows. */
  readonly advanced: ConnectorAdvancedResource;
  readonly outbox: ConnectorOutboxResource;
  readonly documents: ConnectorDocumentsResource;
  /** Customer-scoped Connector workflow for integrator-managed firms. */
  readonly customers: ConnectorCustomersResource;
  /** Configure the single push webhook shared by all managed Connector firms. */
  readonly webhook: ConnectorWebhookResource;

  constructor(config: ClientConfig) {
    super(config);
    this.advanced = new ConnectorAdvancedResource(this, config);
    this.outbox = this.advanced.outbox;
    this.documents = new ConnectorDocumentsResource(this);
    this.customers = new ConnectorCustomersResource(this);
    this.webhook = new ConnectorWebhookResource(config);
  }

  /**
   * Validate receiver reachability and payload readiness before sending.
   * normally send through a customer-scoped documents resource.
   */
  preflight(body: ConnectorPreflightRequest): Promise<ConnectorPreflightResponse> {
    return this.request("POST", "/connector/preflight", body);
  }

  /**
   * Send an ERP document payload through the Connector workflow.
   */
  send(
    body: ConnectorSendRequest,
    options?: { idempotencyKey?: string },
  ): Promise<ConnectorSendResponse> {
    return this.request(
      "POST",
      "/connector/send",
      body,
      options?.idempotencyKey
        ? { idempotencyKey: options.idempotencyKey }
        : undefined,
    );
  }

  /**
   * Get Connector status for a documentId returned by Connector or Enterprise send.
   */
  status(documentId: string): Promise<ConnectorStatusResponse> {
    return this.request(
      "GET",
      `/connector/status/${encodeURIComponent(documentId)}`,
    );
  }

  /**
   * List Connector inbox documents with cursor pagination.
   */
  inbox(params?: ConnectorInboxListParams): Promise<ConnectorInboxListResponse> {
    return this.request(
      "GET",
      `/connector/inbox${buildQuery({
        cursor: params?.cursor,
        limit: params?.limit,
      })}`,
    );
  }

  /**
   * Retrieve a single Connector inbox document, including payload metadata.
   */
  getInboxDocument(documentId: string): Promise<ConnectorInboxDocument> {
    return this.request(
      "GET",
      `/connector/inbox/${encodeURIComponent(documentId)}`,
    );
  }

  /**
   * Acknowledge a Connector inbox document as processed.
   */
  ack(documentId: string): Promise<ConnectorAckResponse> {
    return this.request(
      "POST",
      `/connector/inbox/${encodeURIComponent(documentId)}/ack`,
      {},
      { retry: true },
    );
  }

  /**
   * List Connector polling events with cursor pagination.
   */
  events(params?: ConnectorEventsParams): Promise<ConnectorEventsResponse> {
    return this.request(
      "GET",
      `/connector/events${buildQuery({
        cursor: params?.cursor,
        limit: params?.limit,
      })}`,
    );
  }

  listCustomerEvents(
    customerRef: string,
    params?: ConnectorEventsParams,
  ): Promise<ConnectorBusinessEventsResponse> {
    if (!customerRef.trim()) throw new Error("Connector customerRef is required");
    return this.request(
      "GET",
      `/connector/events${buildQuery({
        customerRef,
        cursor: params?.cursor,
        limit: params?.limit,
      })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  submitCustomerDocument(
    customerRef: string,
    delivery: "send" | "stage",
    body: ConnectorBusinessDocumentRequest,
    options?: { idempotencyKey?: string },
  ): Promise<ConnectorBusinessDocument> {
    assertConnectorDocument(body);
    const normalizedCustomerRef = normalizeConnectorIdentity(customerRef);
    const normalizedExternalId = normalizeConnectorIdentity(body.externalId);
    const idempotencyKey =
      options?.idempotencyKey === undefined
        ? connectorDocumentIdempotencyKey(normalizedCustomerRef, normalizedExternalId)
        : validateConnectorIdempotencyKey(options.idempotencyKey);
    return this.request(
      "POST",
      "/connector/documents",
      {
        ...body,
        externalId: normalizedExternalId,
        customerRef: normalizedCustomerRef,
        delivery,
      },
      { omitFirmId: true, idempotencyKey, retry: true },
    );
  }

  listCustomerDocuments(
    customerRef: string,
    params?: ConnectorBusinessDocumentListParams,
  ): Promise<ConnectorBusinessDocumentListResponse> {
    return this.request(
      "GET",
      `/connector/documents${buildQuery({
        customerRef,
        direction: params?.direction,
        state: params?.state,
        type: params?.type,
        createdAfter: params?.createdAfter,
        cursor: params?.cursor,
        limit: params?.limit,
      })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  acknowledgeDocument(
    documentId: string,
    reference: string,
    customerRef?: string,
  ): Promise<ConnectorBusinessAcknowledgeResponse> {
    if (!reference.trim()) throw new Error("Connector reference is required");
    return this.request(
      "POST",
      `/connector/documents/${encodeURIComponent(documentId)}/acknowledge${buildQuery({ customerRef })}`,
      { reference },
      { omitFirmId: true, retry: true },
    );
  }

  respondDocument(
    documentId: string,
    customerRef: string,
    body: ConnectorInvoiceResponseRequest,
  ): Promise<ConnectorInvoiceResponseResult> {
    const normalizedDocumentId = documentId.trim();
    const normalizedCustomerRef = normalizeConnectorIdentity(customerRef);
    const statuses = new Set<ConnectorInvoiceResponseRequest["status"]>([
      "received",
      "in_process",
      "under_query",
      "conditionally_accepted",
      "rejected",
      "accepted",
      "paid",
    ]);
    if (!normalizedDocumentId) throw new Error("Connector documentId is required");
    if (!normalizedCustomerRef) throw new Error("Connector customerRef is required");
    if (!statuses.has(body.status)) throw new Error("Invalid Connector response status");
    if (body.note !== undefined && typeof body.note !== "string") {
      throw new Error("Connector response note must be a string");
    }
    return this.request(
      "POST",
      `/connector/documents/${encodeURIComponent(normalizedDocumentId)}/respond${buildQuery({ customerRef: normalizedCustomerRef })}`,
      { status: body.status, ...(body.note !== undefined ? { note: body.note } : {}) },
      { omitFirmId: true, retry: true },
    );
  }

  sendCustomerDocument(documentId: string, customerRef?: string): Promise<ConnectorBusinessDocument> {
    if (!documentId.trim()) throw new Error("Connector documentId is required");
    return this.request(
      "POST",
      `/connector/documents/${encodeURIComponent(documentId)}/send${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true, retry: true },
    );
  }

  cancelCustomerDocument(documentId: string, customerRef?: string): Promise<ConnectorBusinessDocument> {
    if (!documentId.trim()) throw new Error("Connector documentId is required");
    return this.request(
      "POST",
      `/connector/documents/${encodeURIComponent(documentId)}/cancel${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true, retry: true },
    );
  }

  /**
   * Start a managed Connector Autopilot lifecycle run.
   */
  autopilot(
    body: ConnectorAutopilotRequest,
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.request("POST", "/connector/autopilot", body, {
      omitFirmId: true,
    });
  }

  /**
   * Map a saved Connector Mapper template input into a preview, staged, or sent run.
   */
  mapper(body: ConnectorMapperRequest): Promise<ConnectorMapperResponse> {
    return this.request("POST", "/connector/mapper", body, {
      omitFirmId: true,
    });
  }

  /**
   * Normalize a loose ERP/customer payload into a Connector lifecycle run.
   */
  zenInput(body: ConnectorZenInputRequest): Promise<ConnectorAutopilotRunResponse> {
    return this.request("POST", "/connector/zen-input", body, {
      omitFirmId: true,
    });
  }

  /**
   * Retrieve an Autopilot run by ID.
   */
  getAutopilotRun(autopilotId: string): Promise<ConnectorAutopilotRunResponse> {
    return this.request(
      "GET",
      `/connector/autopilot/${encodeURIComponent(autopilotId)}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * Send a shadow-validated or staged Autopilot run.
   */
  sendAutopilotRun(autopilotId: string): Promise<ConnectorAutopilotRunResponse> {
    return this.request(
      "POST",
      `/connector/autopilot/${encodeURIComponent(autopilotId)}/send`,
      {},
      { omitFirmId: true, retry: true },
    );
  }

  /**
   * List Connector reconciliation items for ERP state sync.
   */
  reconcile(params?: ConnectorReconcileParams): Promise<ConnectorReconcileResponse> {
    return this.request(
      "GET",
      `/connector/reconcile${buildQuery({
        status: params?.status,
        since: params?.since,
      })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * List Connector-managed customer mailboxes.
   */
  mailboxes(): Promise<ConnectorMailboxListResponse> {
    return this.request("GET", "/connector/mailbox", undefined, {
      omitFirmId: true,
    });
  }

  /**
   * Repair Connector mailbox state for one customer or all customers.
   */
  repairMailbox(
    body: ConnectorMailboxRepairRequest = {},
  ): Promise<Record<string, unknown>> {
    return this.request("POST", "/connector/mailbox/repair", body, {
      omitFirmId: true,
    });
  }

  /**
   * Update the managed send policy for a Connector mailbox.
   */
  updateMailboxSendPolicy(
    customerRef: string,
    body: ConnectorSendPolicyOptions,
  ): Promise<ConnectorMailboxUpdateResponse> {
    return this.request(
      "PATCH",
      `/connector/mailbox/${encodeURIComponent(customerRef)}/send-policy`,
      body,
      { omitFirmId: true },
    );
  }

  /**
   * List Connector sync items for ERP reconciliation cursors.
   */
  sync(params?: ConnectorSyncParams): Promise<ConnectorSyncResponse> {
    return this.request(
      "GET",
      `/connector/sync${buildQuery({
        customerRef: params?.customerRef,
        cursor: params?.cursor,
        limit: params?.limit,
      })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * Retrieve a Connector document lifecycle snapshot.
   */
  getDocument(documentId: string): Promise<Record<string, unknown>>;
  getDocument(
    documentId: string,
    customerRef: string,
  ): Promise<ConnectorBusinessDocument>;
  getDocument(
    documentId: string,
    customerRef?: string,
  ): Promise<ConnectorBusinessDocument | Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * Download a Connector document UBL XML body.
   */
  async getDocumentUbl(documentId: string, customerRef?: string): Promise<string> {
    const res = await this.request<Response>(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/ubl${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true, rawResponse: true },
    );
    return res.text();
  }

  /**
   * Retrieve Connector document delivery evidence.
   */
  getDocumentEvidence(documentId: string, customerRef?: string): Promise<Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/evidence${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * Retrieve the Connector evidence bundle manifest.
   */
  getDocumentEvidenceBundle(documentId: string, customerRef?: string): Promise<Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/evidence-bundle${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * Retrieve the Connector support packet manifest.
   */
  getDocumentSupportPacket(documentId: string, customerRef?: string): Promise<Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/support-packet${buildQuery({ customerRef })}`,
      undefined,
      { omitFirmId: true },
    );
  }

  /**
   * Execute a pending Connector action.
   */
  runAction(
    actionId: string,
    body: ConnectorActionRequest = {},
  ): Promise<ConnectorActionResponse> {
    return this.request(
      "POST",
      `/connector/actions/${encodeURIComponent(actionId)}`,
      body,
      { omitFirmId: true, retry: true },
    );
  }
}
