import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  OutboundDocument,
  OutboundListParams,
  OutboundDocumentsListResponse,
  OutboundEventsParams,
  OutboundEventsListResponse,
} from "../types.js";

/**
 * Resource for the Pull API outbound document feed and event stream.
 *
 * `list()` and `get()` return a unified view of both Invoice-backed (billing)
 * and PeppolDocument (non-billing) outbound documents. `events()` provides a
 * cursor-based lifecycle event stream — Invoice-backed only in v1.
 *
 * Available on `api-enterprise` and `integrator-managed` plans
 * (scope `documents:read`).
 *
 * @example
 * ```typescript
 * // Monitor outbound delivery failures
 * const page = await client.outbound.list({ status: 'failed' });
 * for (const doc of page.documents) {
 *   console.error(`Failed: ${doc.id} — ${doc.error.message}`);
 * }
 * ```
 */
export class OutboundResource extends BaseResource {
  /**
   * List outbound documents using cursor-based pagination.
   * Returns newest first by default; pass `next_cursor` back as `since` to page forward.
   *
   * **Note:** `attempt_history` is always an empty array on list results.
   * Call `get(id)` to retrieve the full attempt history for a specific document.
   *
   * @param params - Optional filter and cursor parameters
   * @returns Paginated list of outbound documents
   *
   * @example
   * ```typescript
   * const { documents, next_cursor } = await client.outbound.list({
   *   status: 'delivered',
   *   limit: 50,
   * });
   * ```
   */
  list(params?: OutboundListParams): Promise<OutboundDocumentsListResponse> {
    return this.request(
      "GET",
      `/outbound/documents${buildQuery({
        since: params?.since,
        limit: params?.limit,
        kind: params?.kind,
        status: params?.status,
        business_status: params?.business_status,
        recipient: params?.recipient,
      })}`,
    );
  }

  /**
   * Retrieve a single outbound document by ID, including the full delivery
   * attempt history (`attempt_history`).
   *
   * @param id - Document UUID
   * @returns Full outbound document with attempt history
   *
   * @example
   * ```typescript
   * const doc = await client.outbound.get('doc-uuid');
   * for (const attempt of doc.attempt_history) {
   *   console.log(`Attempt ${attempt.attempt}: ${attempt.status}`);
   * }
   * ```
   */
  get(id: string): Promise<OutboundDocument> {
    return this.request("GET", `/outbound/documents/${encodeURIComponent(id)}`);
  }

  /**
   * Download the raw UBL 2.1 XML of an outbound document.
   *
   * Returns `404` when no UBL is stored (non-billing non-XML documents).
   *
   * @param id - Document UUID
   * @returns Raw UBL XML string
   *
   * @example
   * ```typescript
   * const xml = await client.outbound.getUbl('doc-uuid');
   * ```
   */
  async getUbl(id: string): Promise<string> {
    const res = await this.request<Response>(
      "GET",
      `/outbound/documents/${encodeURIComponent(id)}/ubl`,
      undefined,
      { rawResponse: true },
    );
    return res.text();
  }

  /**
   * Cursor-based stream of outbound document lifecycle events.
   * Results are sorted oldest-to-newest (ascending) — a saved cursor
   * naturally advances forward in time.
   *
   * **Coverage:** Invoice-backed (billing) events only in v1. Non-billing outbound
   * document state changes are tracked via `list()` with `status` filter.
   *
   * @param params - Optional cursor, limit, and document_id filter
   * @returns Paginated event list with next_cursor
   *
   * @example
   * ```typescript
   * // Consume all new events since last run
   * const { events, next_cursor } = await client.outbound.events({
   *   since: storedCursor,
   *   limit: 200,
   * });
   * for (const ev of events) {
   *   console.log(ev.type, ev.document_id, ev.occurred_at);
   * }
   * // Persist next_cursor for the next run
   * ```
   */
  events(params?: OutboundEventsParams): Promise<OutboundEventsListResponse> {
    return this.request(
      "GET",
      `/outbound/events${buildQuery({
        since: params?.since,
        limit: params?.limit,
        document_id: params?.document_id,
      })}`,
    );
  }
}
