import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  ConnectorAckResponse,
  ConnectorEventsParams,
  ConnectorEventsResponse,
  ConnectorInboxDocument,
  ConnectorInboxListParams,
  ConnectorInboxListResponse,
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
  ConnectorSendRequest,
  ConnectorSendResponse,
  ConnectorStatusResponse,
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
}
