import { DocumentsResource } from "./resources/documents.js";
import { FirmsResource } from "./resources/firms.js";
import { PeppolResource } from "./resources/peppol.js";
import { WebhooksResource } from "./resources/webhooks.js";
import { ReportingResource } from "./resources/reporting.js";
import { ExtractResource } from "./resources/extract.js";
import { AccountResource } from "./resources/account.js";
import type { ClientConfig } from "./utils/request.js";

const DEFAULT_BASE_URL = "https://epostak.sk/api/enterprise";

export interface EPostakConfig {
  /**
   * Your Enterprise API key. Use `sk_live_*` for direct access or
   * `sk_int_*` for integrator (multi-tenant) access.
   */
  apiKey: string;
  /**
   * Base URL for the API. Defaults to `https://epostak.sk/api/enterprise`.
   * Override for staging or local testing.
   */
  baseUrl?: string;
  /**
   * Firm UUID to act on behalf of. Required when using integrator keys
   * (`sk_int_*`). Each API call will include `X-Firm-Id` header.
   */
  firmId?: string;
}

/**
 * ePošťák Enterprise API client.
 *
 * @example
 * ```typescript
 * import { EPostak } from '@epostak/sdk';
 *
 * const client = new EPostak({ apiKey: 'sk_live_xxxxx' });
 * const result = await client.documents.send({ ... });
 * ```
 */
export class EPostak {
  private readonly clientConfig: ClientConfig;

  /** Send and receive documents via Peppol */
  documents: DocumentsResource;
  /** Manage client firms (integrator keys) */
  firms: FirmsResource;
  /** SMP lookup and Peppol directory search */
  peppol: PeppolResource;
  /** Manage webhook subscriptions and pull queue */
  webhooks: WebhooksResource;
  /** Document statistics and reports */
  reporting: ReportingResource;
  /** AI-powered OCR extraction from PDFs and images */
  extract: ExtractResource;
  /** Account and firm information */
  account: AccountResource;

  constructor(config: EPostakConfig) {
    if (!config.apiKey || typeof config.apiKey !== "string") {
      throw new Error("EPostak: apiKey is required");
    }

    this.clientConfig = {
      apiKey: config.apiKey,
      baseUrl: config.baseUrl ?? DEFAULT_BASE_URL,
      firmId: config.firmId,
    };

    this.documents = new DocumentsResource(this.clientConfig);
    this.firms = new FirmsResource(this.clientConfig);
    this.peppol = new PeppolResource(this.clientConfig);
    this.webhooks = new WebhooksResource(this.clientConfig);
    this.reporting = new ReportingResource(this.clientConfig);
    this.extract = new ExtractResource(this.clientConfig);
    this.account = new AccountResource(this.clientConfig);
  }

  /**
   * Create a new client instance scoped to a specific firm.
   * Useful when an integrator key (`sk_int_*`) needs to switch between
   * client firms without creating a new `EPostak` instance from scratch.
   *
   * @param firmId - The firm UUID to scope subsequent requests to
   * @returns A new `EPostak` client with the `X-Firm-Id` header set
   *
   * @example
   * ```typescript
   * const integrator = new EPostak({ apiKey: 'sk_int_xxxxx' });
   *
   * // Act on behalf of firm A
   * const firmA = integrator.withFirm('firm-a-uuid');
   * await firmA.documents.send({ ... });
   *
   * // Act on behalf of firm B
   * const firmB = integrator.withFirm('firm-b-uuid');
   * await firmB.documents.inbox.list();
   * ```
   */
  withFirm(firmId: string): EPostak {
    return new EPostak({
      apiKey: this.clientConfig.apiKey,
      baseUrl: this.clientConfig.baseUrl,
      firmId,
    });
  }
}
