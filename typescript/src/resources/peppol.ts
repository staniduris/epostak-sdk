import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  PeppolParticipant,
  DirectorySearchParams,
  DirectorySearchResult,
  CompanyLookup,
  PeppolCapabilitiesRequest,
  PeppolCapabilitiesResponse,
  BatchLookupParticipant,
  BatchLookupResponse,
} from "../types.js";

/**
 * Sub-resource for searching the Peppol Business Card directory.
 * The directory contains publicly registered Peppol participants.
 */
export class PeppolDirectoryResource extends BaseResource {
  /**
   * Search the Peppol Business Card directory for registered participants.
   * Requires the `documents:read` scope.
   * `q` is required (min 2 chars). Matches business name (starts-with) or
   * an exact participant ID.
   *
   * @param params - Search query, optional country filter, and pagination
   * @returns Paginated search results (`items`, `page`, `page_size`, `has_next`)
   *
   * @example
   * ```typescript
   * // Search for Slovak companies
   * const { items, has_next } = await client.peppol.directory.search({
   *   q: 'Telekom',
   *   country: 'SK',
   *   page_size: 50,
   * });
   * ```
   */
  search(params: DirectorySearchParams): Promise<DirectorySearchResult> {
    return this.request(
      "GET",
      `/peppol/directory/search${buildQuery({
        q: params.q,
        country: params.country,
        page: params.page,
        page_size: params.page_size,
      })}`,
    );
  }
}

/**
 * Resource for Peppol network operations — SMP lookups, directory search,
 * and Slovak company information retrieval.
 *
 * @example
 * ```typescript
 * // Look up a Peppol participant
 * const participant = await client.peppol.lookup('0245', '1234567890');
 * console.log(participant.accessPoint);
 *
 * // Look up a Slovak company by ICO
 * const company = await client.peppol.companyLookup('12345678');
 * ```
 */
export class PeppolResource extends BaseResource {
  /** Sub-resource for searching the Peppol Business Card directory */
  directory: PeppolDirectoryResource;

  constructor(config: ClientConfig) {
    super(config);
    this.directory = new PeppolDirectoryResource(config);
  }

  /**
   * Look up a Peppol participant by their identifier scheme and value.
   * Queries the SMP (Service Metadata Publisher) to retrieve access point
   * metadata and supported document types.
   *
   * Returns `null` when the participant is not registered on Peppol
   * (HTTP 404 is swallowed for this convenience). Any other HTTP error
   * is thrown as usual.
   *
   * @param scheme - Peppol identifier scheme (e.g. `"0245"` for Slovak DIČ)
   * @param identifier - Identifier value within the scheme (e.g. `"1234567890"`)
   * @returns Participant details, or `null` if not registered
   *
   * @example
   * ```typescript
   * const participant = await client.peppol.lookup('0245', '1234567890');
   * if (participant?.accessPoint?.url) {
   *   console.log('AP endpoint:', participant.accessPoint.url);
   * }
   * ```
   */
  async lookup(
    scheme: string,
    identifier: string,
  ): Promise<PeppolParticipant | null> {
    try {
      return await this.request<PeppolParticipant>(
        "GET",
        `/peppol/participants/${encodeURIComponent(scheme)}/${encodeURIComponent(identifier)}`,
      );
    } catch (err) {
      // 404 → not registered. Re-throw anything else.
      if (err instanceof Error && (err as { status?: number }).status === 404) {
        return null;
      }
      throw err;
    }
  }

  /**
   * Look up a Slovak company by its ICO (business registration number).
   * Queries Slovak business registers (ORSR, FinStat) and checks Peppol
   * registration status. Returns company name, tax IDs, address, and Peppol ID.
   *
   * @param ico - Slovak ICO (8-digit business registration number)
   * @returns Company information including Peppol registration status
   *
   * @example
   * ```typescript
   * const company = await client.peppol.companyLookup('12345678');
   * console.log(company.name); // "ACME s.r.o."
   * console.log(company.peppolId); // "0245:1234567890" or null
   * ```
   */
  companyLookup(ico: string): Promise<CompanyLookup> {
    return this.request("GET", `/company/lookup/${encodeURIComponent(ico)}`);
  }

  /**
   * Storecove-style capability probe — describes what a Peppol participant
   * is able to receive over the network. Provide `documentType` to check
   * for a specific BIS 3.0 doctype; omit it to list every supported doctype.
   *
   * Backend returns 404 when the participant isn't registered; the SDK
   * surfaces that as a thrown `EPostakError(404)`.
   *
   * @param body - Participant (scheme + identifier) and optional documentType / processId
   * @returns Capability summary — `matchedDocumentType` is populated when a specific `documentType` was provided
   *
   * @example
   * ```typescript
   * const caps = await client.peppol.capabilities({
   *   participant: { scheme: '0245', identifier: '1234567890' },
   *   documentType: 'Invoice',
   * });
   * if (caps.found && caps.accepts) {
   *   console.log('Ready to receive the requested doctype');
   * }
   * ```
   */
  capabilities(
    body: PeppolCapabilitiesRequest,
  ): Promise<PeppolCapabilitiesResponse> {
    return this.request("POST", "/peppol/capabilities", body);
  }

  /**
   * Look up up to 100 Peppol participants in a single request. Useful for
   * pre-flighting an entire customer database before a bulk send campaign.
   *
   * @param participants - Array of 1–100 participants (scheme + identifier)
   * @returns Aggregate counts plus per-participant results in request order
   *
   * @example
   * ```typescript
   * const batch = await client.peppol.lookupBatch([
   *   { scheme: '0245', identifier: '1234567890' },
   *   { scheme: '0245', identifier: '9876543210' },
   * ]);
   * console.log(`${batch.found}/${batch.total} registered on Peppol`);
   * for (const r of batch.results) {
   *   if (!r.found) {
   *     console.warn(`Not on Peppol: ${r.participant.id}`);
   *   }
   * }
   * ```
   */
  lookupBatch(
    participants: BatchLookupParticipant[],
  ): Promise<BatchLookupResponse> {
    return this.request(
      "POST",
      "/peppol/participants/batch",
      { participants },
      { retry: true },
    );
  }
}
