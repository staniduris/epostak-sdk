import { DocumentsResource } from "./resources/documents.js";
import { FirmsResource } from "./resources/firms.js";
import { PeppolResource } from "./resources/peppol.js";
import { WebhooksResource } from "./resources/webhooks.js";
import { ReportingResource } from "./resources/reporting.js";
import { ExtractResource } from "./resources/extract.js";
import { AccountResource } from "./resources/account.js";
import { AuthResource } from "./resources/auth.js";
import { AuditResource } from "./resources/audit.js";
import { IntegratorResource } from "./resources/integrator.js";
import type { ClientConfig } from "./utils/request.js";
import type { PublicValidationReport } from "./types.js";
import { EPostakError } from "./utils/errors.js";
import { TokenManager } from "./utils/token-manager.js";

const DEFAULT_BASE_URL = "https://epostak.sk/api/v1";
const PUBLIC_VALIDATE_URL = "https://epostak.sk/api/validate";

export interface EPostakConfig {
  /**
   * Client ID for authentication. This is the API key ID (UUID) returned
   * when the key was created, or the key prefix (e.g. `sk_live_abc...xyz`).
   */
  clientId: string;
  /**
   * Client secret — the full API key string (`sk_live_*` or `sk_int_*`).
   * Used to mint JWT access tokens via the OAuth client_credentials flow.
   */
  clientSecret: string;
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
   */
  maxRetries?: number;
}

/**
 * ePošťák API client.
 *
 * Authenticates via OAuth client_credentials: the SDK automatically mints
 * a short-lived JWT from your API key on the first request and refreshes
 * it before expiry. You never handle JWTs directly.
 *
 * @example
 * ```typescript
 * import { EPostak } from "@epostak/sdk";
 *
 * const client = new EPostak({
 *   clientId: "sk_live_xxxxx",
 *   clientSecret: "sk_live_xxxxx",
 * });
 * const result = await client.documents.send({ ... });
 * ```
 */
export class EPostak {
  private readonly clientConfig: ClientConfig;
  private readonly tokenManager: TokenManager;
  private readonly _clientId: string;
  private readonly _clientSecret: string;

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
  /** Integrator-aggregate endpoints (sk_int_* keys). */
  integrator: IntegratorResource;

  constructor(config: EPostakConfig);
  /** @internal Used by withFirm to share the token manager. */
  constructor(config: EPostakConfig, tokenManager: TokenManager);
  constructor(config: EPostakConfig, tokenManager?: TokenManager) {
    if (!config.clientSecret || typeof config.clientSecret !== "string") {
      throw new Error("EPostak: clientSecret is required");
    }

    const baseUrl = config.baseUrl ?? DEFAULT_BASE_URL;

    this._clientId = config.clientId;
    this._clientSecret = config.clientSecret;

    const tmConfig: import("./utils/token-manager.js").TokenManagerConfig = {
      clientId: config.clientId,
      clientSecret: config.clientSecret,
      baseUrl,
    };

    this.tokenManager = tokenManager ?? new TokenManager(tmConfig);

    this.clientConfig = {
      tokenManager: this.tokenManager,
      baseUrl,
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
    this.integrator = new IntegratorResource(this.clientConfig);
  }

  /**
   * Create a new client instance scoped to a specific firm.
   * Shares the same token manager (JWT is reused), only adds X-Firm-Id.
   *
   * @param firmId - The firm UUID to scope subsequent requests to
   * @returns A new `EPostak` client with the `X-Firm-Id` header set
   *
   * @example
   * ```typescript
   * const integrator = new EPostak({
   *   clientId: "sk_int_xxxxx",
   *   clientSecret: "sk_int_xxxxx",
   * });
   *
   * const firmA = integrator.withFirm("firm-a-uuid");
   * await firmA.documents.send({ ... });
   * ```
   */
  withFirm(firmId: string): EPostak {
    return new EPostak(
      {
        clientId: this._clientId,
        clientSecret: this._clientSecret,
        baseUrl: this.clientConfig.baseUrl,
        firmId,
        maxRetries: this.clientConfig.maxRetries,
      },
      this.tokenManager,
    );
  }

  /**
   * Validate a UBL 2.1 XML invoice against UBL XSD + EN 16931 + Peppol BIS 3.0
   * schematron. This hits the **public** validator-as-a-service endpoint —
   * no API key is required.
   */
  validate(xml: string): Promise<PublicValidationReport> {
    return EPostak.validate(xml);
  }

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
