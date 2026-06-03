import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  ConnectorAckResponse,
  ConnectorEventsParams,
  ConnectorEventsResponse,
  ConnectorInboxDocument,
  ConnectorInboxListParams,
  ConnectorInboxListResponse,
  ConnectorPreflightRequest,
  ConnectorPreflightResponse,
  ConnectorSendRequest,
  ConnectorSendResponse,
  ConnectorStatusResponse,
} from "../types.js";

/**
 * Connector workflow endpoints for ERP teams.
 *
 * Connector is a polling-first workflow over the Enterprise API. It uses the
 * same credentials, firm scoping, and documentId as the full Enterprise API.
 */
export class ConnectorResource extends BaseResource {
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
