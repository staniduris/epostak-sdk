import { DocumentsResource } from "./resources/documents.js";
import { FirmsResource } from "./resources/firms.js";
import { PeppolResource } from "./resources/peppol.js";
import { WebhooksResource } from "./resources/webhooks.js";
import { ReportingResource } from "./resources/reporting.js";
import { ExtractResource } from "./resources/extract.js";
import { AccountResource } from "./resources/account.js";
import { AuthResource } from "./resources/auth.js";
import { AuditResource } from "./resources/audit.js";
import type { ClientConfig } from "./utils/request.js";
import type { PublicValidationReport } from "./types.js";
import { EPostakError } from "./utils/errors.js";

const DEFAULT_BASE_URL = "https://epostak.sk/api/v1";
const PUBLIC_VALIDATE_URL = "https://epostak.sk/api/validate";

export interface EPostakConfig {
  /**
   * Your API key. Use `sk_live_*` for direct firm access or `sk_int_*`
   * for integrator (multi-tenant) access.
   */
  apiKey: string;
  /**
   * Base URL for the API. Defaults to `https://epostak.sk/api/v1`.
   * Override for staging or local testing.
   */
  baseUrl?: string;
  /**
   * Firm UUID to act on behalf of. Required when using integrator keys
   * (`sk_int_*`). Each API call will include `X-Firm-Id` header.
   */
  firmId?: string;
  /**
   * Maximum number of automatic retries on 429 (rate-limit) and 5xx errors.
   * Uses exponential backoff with jitter. Defaults to 3, set 0 to disable.
   * Only GET and DELETE are retried by default; POST/PATCH/PUT require
   * explicit opt-in via the `retry` option on individual requests.
   */
  maxRetries?: number;
}

/**
 * ePošťák API client.
 *
 * @example
 * ```typescript
 * import { EPostak } from "@epostak/sdk";
 *
 * const client = new EPostak({ apiKey: "sk_live_xxxxx" });
 * const result = await client.documents.send({ ... });
 * ```
 */
export class EPostak {
  private readonly clientConfig: ClientConfig;

  /** OAuth token mint/renew/revoke + key introspection, rotation, IP allowlist. */
  auth: AuthResource;
  /** Per-firm audit feed (cursor-paginated). */
  audit: AuditResource;
  /** Send and receive documents via Peppol. */
  documents: DocumentsResource;
  /** Manage client firms (integrator keys). */
  firms: FirmsResource;
  /** SMP lookup and Peppol directory search. */
  peppol: PeppolResource;
  /** Manage webhook subscriptions and pull queue. */
  webhooks: WebhooksResource;
  /** Document statistics and reports. */
  reporting: ReportingResource;
  /** AI-powered OCR extraction from PDFs and images. */
  extract: ExtractResource;
  /** Account and firm information. */
  account: AccountResource;

  constructor(config: EPostakConfig) {
    if (!config.apiKey || typeof config.apiKey !== "string") {
      throw new Error("EPostak: apiKey is required");
    }

    this.clientConfig = {
      apiKey: config.apiKey,
      baseUrl: config.baseUrl ?? DEFAULT_BASE_URL,
      firmId: config.firmId,
      maxRetries: config.maxRetries ?? 3,
    };

    this.auth = new AuthResource(this.clientConfig);
    this.audit = new AuditResource(this.clientConfig);
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
   * const integrator = new EPostak({ apiKey: "sk_int_xxxxx" });
   *
   * const firmA = integrator.withFirm("firm-a-uuid");
   * await firmA.documents.send({ ... });
   *
   * const firmB = integrator.withFirm("firm-b-uuid");
   * await firmB.documents.inbox.list();
   * ```
   */
  withFirm(firmId: string): EPostak {
    return new EPostak({
      apiKey: this.clientConfig.apiKey,
      baseUrl: this.clientConfig.baseUrl,
      firmId,
      maxRetries: this.clientConfig.maxRetries,
    });
  }

  /**
   * Validate a UBL 2.1 XML invoice against UBL XSD + EN 16931 + Peppol BIS 3.0
   * schematron. This hits the **public** validator-as-a-service endpoint —
   * no API key is required and the call works even without a configured client.
   *
   * Rate-limited to 20 requests per minute per IP. Max 10 MB per XML payload.
   *
   * @param xml - Full UBL 2.1 Invoice or CreditNote XML
   * @returns Full three-layer validation report
   */
  validate(xml: string): Promise<PublicValidationReport> {
    return EPostak.validate(xml);
  }

  /**
   * Static variant of {@link EPostak.prototype.validate}. Call this without
   * instantiating a client when you only need the public validator.
   */
  static async validate(
    xml: string,
    baseUrl: string = PUBLIC_VALIDATE_URL,
  ): Promise<PublicValidationReport> {
    let res: Response;
    try {
      res = await fetch(baseUrl, {
        method: "POST",
        headers: { "Content-Type": "application/xml" },
        body: xml,
      });
    } catch (err) {
      throw new EPostakError(0, {
        error: err instanceof Error ? err.message : "Network error",
      });
    }
    if (!res.ok) {
      let errorBody: Record<string, unknown> = {};
      try {
        errorBody = (await res.json()) as Record<string, unknown>;
      } catch {
        errorBody = { error: res.statusText };
      }
      throw new EPostakError(res.status, errorBody, res.headers);
    }
    return (await res.json()) as PublicValidationReport;
  }
}
