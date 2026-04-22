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
   * Supports free-text search and country filtering with pagination.
   *
   * @param params - Optional search query, country filter, and pagination
   * @returns Paginated search results from the directory
   *
   * @example
   * ```typescript
   * // Search for Slovak companies
   * const { results, total } = await client.peppol.directory.search({
   *   q: 'consulting',
   *   country: 'SK',
   *   page_size: 50,
   * });
   * ```
   */
  search(params?: DirectorySearchParams): Promise<DirectorySearchResult> {
    return this.request(
      "GET",
      `/peppol/directory/search${buildQuery({
        q: params?.q,
        country: params?.country,
        page: params?.page,
        page_size: params?.page_size,
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
 * console.log(participant.capabilities);
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
   * Queries the SMP (Service Metadata Publisher) to retrieve the participant's
   * name, country, and supported document types.
   *
   * @param scheme - Peppol identifier scheme (e.g. `"0245"` for Slovak DIČ)
   * @param identifier - Identifier value within the scheme (e.g. `"1234567890"`)
   * @returns Participant details including capabilities (supported document types)
   *
   * @example
   * ```typescript
   * const participant = await client.peppol.lookup('0245', '1234567890');
   * if (participant.capabilities.length > 0) {
   *   console.log('Supports:', participant.capabilities.map(c => c.documentTypeId));
   * }
   * ```
   */
  lookup(scheme: string, identifier: string): Promise<PeppolParticipant> {
    return this.request(
      "GET",
      `/peppol/participants/${encodeURIComponent(scheme)}/${encodeURIComponent(identifier)}`,
    );
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
   * is able to receive over the network. Use when you want a higher-level
   * summary than the raw SMP capabilities returned by {@link lookup}.
   *
   * @param body - Participant scheme, identifier, and optional document type
   * @returns Capability summary including accepted families and supported document types
   *
   * @example
   * ```typescript
   * const caps = await client.peppol.capabilities({
   *   scheme: '0245',
   *   identifier: '1234567890',
   *   documentType: 'Invoice',
   * });
   * if (caps.found && caps.accepts.includes('invoice')) {
   *   console.log('Ready to receive invoices');
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
   *     console.warn(`Not on Peppol: ${r.scheme}:${r.identifier}`);
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
