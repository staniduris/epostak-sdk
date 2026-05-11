import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  InboundDocument,
  InboundListParams,
  InboundDocumentsListResponse,
  InboundAckRequest,
} from "../types.js";

/**
 * Resource for the Pull API inbound document feed.
 *
 * This is the preferred way to receive Peppol documents — it uses cursor-based
 * pagination so you can reliably consume new arrivals without polling offsets.
 * Available on `api-enterprise` and `integrator-managed` plans (scope `documents:read`,
 * `documents:write` for ack).
 *
 * **Prefer this over the legacy `client.documents.inbox` path** which uses
 * offset pagination and is limited to `api-enterprise` plan only.
 *
 * @example
 * ```typescript
 * // Drain all unacknowledged inbound invoices
 * let cursor: string | undefined;
 * do {
 *   const page = await client.inbound.list({ since: cursor, limit: 100 });
 *   for (const doc of page.documents) {
 *     if (!doc.ack.acked_at) {
 *       const xml = await client.inbound.getUbl(doc.id);
 *       await myERP.importInvoice(xml);
 *       await client.inbound.ack(doc.id, { client_reference: 'erp-ref-123' });
 *     }
 *   }
 *   cursor = page.next_cursor ?? undefined;
 * } while (page.has_more);
 * ```
 */
export class InboundResource extends BaseResource {
  /**
   * List inbound documents using cursor-based pagination, newest first.
   * Pass the returned `next_cursor` back as `since` to page forward.
   *
   * @param params - Optional filter and cursor parameters
   * @returns Paginated list of inbound documents
   *
   * @example
   * ```typescript
   * const page = await client.inbound.list({ kind: 'invoice', limit: 50 });
   * ```
   */
  list(params?: InboundListParams): Promise<InboundDocumentsListResponse> {
    return this.request(
      "GET",
      `/inbound/documents${buildQuery({
        since: params?.since,
        limit: params?.limit,
        kind: params?.kind,
        sender: params?.sender,
      })}`,
    );
  }

  /**
   * Retrieve a single inbound document by ID.
   *
   * @param id - Document UUID
   * @returns Full inbound document object
   *
   * @example
   * ```typescript
   * const doc = await client.inbound.get('doc-uuid');
   * console.log(doc.sender.peppol_id, doc.ack.acked_at);
   * ```
   */
  get(id: string): Promise<InboundDocument> {
    return this.request("GET", `/inbound/documents/${encodeURIComponent(id)}`);
  }

  /**
   * Download the raw UBL 2.1 XML of an inbound document.
   *
   * Returns `404` when the document has no UBL path stored (legacy rows).
   *
   * @param id - Document UUID
   * @returns Raw UBL XML string
   *
   * @example
   * ```typescript
   * const xml = await client.inbound.getUbl('doc-uuid');
   * // Parse or store the UBL 2.1 XML
   * ```
   */
  async getUbl(id: string): Promise<string> {
    const res = await this.request<Response>(
      "GET",
      `/inbound/documents/${encodeURIComponent(id)}/ubl`,
      undefined,
      { rawResponse: true },
    );
    return res.text();
  }

  /**
   * Acknowledge an inbound document, marking it as processed.
   *
   * Idempotent — acknowledging an already-acknowledged document simply
   * overwrites the stored `client_reference` (latest-ack-wins). Requires scope
   * `documents:write`.
   *
   * @param id - Document UUID to acknowledge
   * @param body - Optional body with `client_reference` (max 256 chars)
   * @returns The updated document with acknowledgement timestamps filled
   *
   * @example
   * ```typescript
   * const updated = await client.inbound.ack('doc-uuid', {
   *   client_reference: 'erp-order-2026001',
   * });
   * console.log(updated.ack.acked_at); // ISO 8601 timestamp
   * ```
   */
  ack(id: string, body?: InboundAckRequest): Promise<InboundDocument> {
    return this.request(
      "POST",
      `/inbound/documents/${encodeURIComponent(id)}/ack`,
      body ?? {},
      { retry: true },
    );
  }
}
