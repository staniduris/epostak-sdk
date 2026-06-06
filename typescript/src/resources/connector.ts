import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  ConnectorAckResponse,
  ConnectorActionRequest,
  ConnectorActionResponse,
  ConnectorAutopilotRequest,
  ConnectorAutopilotRunResponse,
  ConnectorEventsParams,
  ConnectorEventsResponse,
  ConnectorInboxDocument,
  ConnectorInboxListParams,
  ConnectorInboxListResponse,
  ConnectorMailboxListResponse,
  ConnectorMailboxRepairRequest,
  ConnectorMailboxUpdateResponse,
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
  ConnectorZenInputRequest,
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

/**
 * Connector workflow endpoints for ERP teams.
 *
 * Connector is a polling-first workflow over the Enterprise API. It uses the
 * same credentials, firm scoping, and documentId as the full Enterprise API.
 */
export class ConnectorResource extends BaseResource {
  /** Stage now, send later lifecycle for outbound ERP invoices. */
  readonly outbox: ConnectorOutboxResource;

  constructor(config: ClientConfig) {
    super(config);
    this.outbox = new ConnectorOutboxResource(config);
  }

  /**
   * Validate receiver reachability and payload readiness before sending.
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

  /**
   * Start a managed Connector Autopilot lifecycle run.
   */
  autopilot(
    body: ConnectorAutopilotRequest,
  ): Promise<ConnectorAutopilotRunResponse> {
    return this.request("POST", "/connector/autopilot", body);
  }

  /**
   * Normalize a loose ERP/customer payload into a Connector lifecycle run.
   */
  zenInput(body: ConnectorZenInputRequest): Promise<ConnectorAutopilotRunResponse> {
    return this.request("POST", "/connector/zen-input", body);
  }

  /**
   * Retrieve an Autopilot run by ID.
   */
  getAutopilotRun(autopilotId: string): Promise<ConnectorAutopilotRunResponse> {
    return this.request(
      "GET",
      `/connector/autopilot/${encodeURIComponent(autopilotId)}`,
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
      { retry: true },
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
    );
  }

  /**
   * List Connector-managed customer mailboxes.
   */
  mailboxes(): Promise<ConnectorMailboxListResponse> {
    return this.request("GET", "/connector/mailbox");
  }

  /**
   * Repair Connector mailbox state for one customer or all customers.
   */
  repairMailbox(
    body: ConnectorMailboxRepairRequest = {},
  ): Promise<Record<string, unknown>> {
    return this.request("POST", "/connector/mailbox/repair", body);
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
    );
  }

  /**
   * Retrieve a Connector document lifecycle snapshot.
   */
  getDocument(documentId: string): Promise<Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}`,
    );
  }

  /**
   * Download a Connector document UBL XML body.
   */
  async getDocumentUbl(documentId: string): Promise<string> {
    const res = await this.request<Response>(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/ubl`,
      undefined,
      { rawResponse: true },
    );
    return res.text();
  }

  /**
   * Retrieve Connector document delivery evidence.
   */
  getDocumentEvidence(documentId: string): Promise<Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/evidence`,
    );
  }

  /**
   * Retrieve the Connector evidence bundle manifest.
   */
  getDocumentEvidenceBundle(documentId: string): Promise<Record<string, unknown>> {
    return this.request(
      "GET",
      `/connector/documents/${encodeURIComponent(documentId)}/evidence-bundle`,
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
      { retry: true },
    );
  }
}
