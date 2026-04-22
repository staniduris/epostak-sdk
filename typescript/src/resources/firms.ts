import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  FirmSummary,
  FirmDetail,
  FirmsListResponse,
  FirmDocumentsParams,
  InboxListResponse,
  PeppolIdentifierResponse,
  AssignFirmRequest,
  AssignFirmResponse,
  BatchAssignFirmsRequest,
  BatchAssignFirmsResponse,
} from "../types.js";

/**
 * Resource for managing firms (companies) associated with your account.
 * Integrators use this to assign client firms, view their documents,
 * and register Peppol identifiers.
 *
 * @example
 * ```typescript
 * // List all managed firms
 * const firms = await client.firms.list();
 *
 * // Assign a new firm by ICO
 * const { firm } = await client.firms.assign({ ico: '12345678' });
 * ```
 */
export class FirmsResource extends BaseResource {
  /**
   * List all firms associated with the current account.
   * For integrator keys, returns all assigned client firms.
   *
   * @returns Array of firm summaries
   *
   * @example
   * ```typescript
   * const firms = await client.firms.list();
   * firms.forEach(f => console.log(f.name, f.peppolStatus));
   * ```
   */
  async list(): Promise<FirmSummary[]> {
    const res = await this.request<FirmsListResponse>("GET", "/firms");
    return res.firms;
  }

  /**
   * Get detailed information about a specific firm, including tax IDs,
   * address, and all registered Peppol identifiers.
   *
   * @param id - Firm UUID
   * @returns Detailed firm information
   *
   * @example
   * ```typescript
   * const firm = await client.firms.get('firm-uuid');
   * console.log(firm.icDph); // "SK2020123456"
   * ```
   */
  get(id: string): Promise<FirmDetail> {
    return this.request("GET", `/firms/${encodeURIComponent(id)}`);
  }

  /**
   * List documents belonging to a specific firm.
   * Useful for integrators to view a client's document history.
   *
   * @param id - Firm UUID
   * @param params - Optional pagination (`page`/`page_size`), direction, status, and date range
   * @returns Paginated list of the firm's documents
   *
   * @example
   * ```typescript
   * const { documents } = await client.firms.documents('firm-uuid', {
   *   direction: 'inbound',
   *   page_size: 100,
   * });
   * ```
   */
  documents(
    id: string,
    params?: FirmDocumentsParams,
  ): Promise<InboxListResponse> {
    return this.request(
      "GET",
      `/firms/${encodeURIComponent(id)}/documents${buildQuery({
        page: params?.page,
        page_size: params?.page_size,
        direction: params?.direction,
        status: params?.status,
        from: params?.from,
        to: params?.to,
      })}`,
    );
  }

  /**
   * Register a new Peppol identifier for a firm. This enables the firm
   * to send and receive documents on the Peppol network under this identifier.
   *
   * @param id - Firm UUID
   * @param peppolId - Peppol identifier with scheme and value
   * @returns The registered identifier details
   *
   * @example
   * ```typescript
   * const result = await client.firms.registerPeppolId('firm-uuid', {
   *   scheme: '0245',
   *   identifier: '1234567890',
   * });
   * console.log(result.peppolId); // "0245:1234567890"
   * ```
   */
  registerPeppolId(
    id: string,
    peppolId: { scheme: string; identifier: string },
  ): Promise<PeppolIdentifierResponse> {
    return this.request(
      "POST",
      `/firms/${encodeURIComponent(id)}/peppol-identifiers`,
      peppolId,
    );
  }

  /**
   * Assign a firm to the integrator account by its Slovak ICO.
   * Once assigned, you can send/receive documents on behalf of this firm.
   *
   * @param body - Request containing the firm's ICO (8-digit Slovak business registration number)
   * @returns The assigned firm details and status
   *
   * @example
   * ```typescript
   * const { firm } = await client.firms.assign({ ico: '12345678' });
   * console.log(firm.id); // Use this UUID for subsequent operations
   * ```
   */
  assign(body: AssignFirmRequest): Promise<AssignFirmResponse> {
    return this.request("POST", "/firms/assign", body);
  }

  /**
   * Assign multiple firms at once by their Slovak ICOs.
   * Each ICO is processed independently — individual failures don't affect others.
   * Maximum 50 ICOs per request.
   *
   * @param body - Request containing an array of ICOs
   * @returns Individual results for each ICO (success or error)
   *
   * @example
   * ```typescript
   * const { results } = await client.firms.assignBatch({
   *   icos: ['12345678', '87654321', '11223344'],
   * });
   * results.forEach(r => {
   *   if (r.error) console.error(`${r.ico}: ${r.message}`);
   *   else console.log(`${r.ico}: ${r.status}`);
   * });
   * ```
   */
  assignBatch(
    body: BatchAssignFirmsRequest,
  ): Promise<BatchAssignFirmsResponse> {
    return this.request("POST", "/firms/assign/batch", body);
  }
}
