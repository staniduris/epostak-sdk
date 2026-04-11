import { BaseResource, buildQuery } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  PeppolParticipant,
  DirectorySearchParams,
  DirectorySearchResult,
  CompanyLookup,
} from "../types.js";

export class PeppolDirectoryResource extends BaseResource {
  search(params?: DirectorySearchParams): Promise<DirectorySearchResult> {
    return this.request("GET", `/peppol/directory/search${buildQuery({
      q: params?.q,
      country: params?.country,
      page: params?.page,
      page_size: params?.page_size,
    })}`);
  }
}

export class PeppolResource extends BaseResource {
  /** Access Peppol Business Card directory */
  directory: PeppolDirectoryResource;

  constructor(config: ClientConfig) {
    super(config);
    this.directory = new PeppolDirectoryResource(config);
  }

  lookup(scheme: string, identifier: string): Promise<PeppolParticipant> {
    return this.request("GET", `/peppol/participants/${encodeURIComponent(scheme)}/${encodeURIComponent(identifier)}`);
  }

  companyLookup(ico: string): Promise<CompanyLookup> {
    return this.request("GET", `/company/lookup/${encodeURIComponent(ico)}`);
  }
}
