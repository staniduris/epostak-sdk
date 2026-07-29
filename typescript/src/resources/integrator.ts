import { BaseResource, buildQuery } from "../utils/request.js";
import type {
  DeactivateIntegratorKeyRequest,
  DeactivateIntegratorKeyResponse,
  IntegratorKeysResponse,
  IntegratorLicenseInfo,
  IntegratorLicenseInfoParams,
} from "../types.js";

/**
 * Integrator-level helpers — `sk_int_*` only.
 *
 * For the per-firm `/account` and `/licenses/info` views (which integrators
 * also have access to via `X-Firm-Id`), use `client.account` instead. This
 * resource exposes the integrator-aggregate endpoints that don't take a
 * firm context.
 */
export class IntegratorResource extends BaseResource {
  /** Integrator API-key management. */
  readonly keys = new IntegratorKeysResource(this.config);

  /**
   * License & billing namespace.
   *
   * @example
   * ```ts
   * const usage = await client.integrator.licenses.info();
   * console.log(usage.billable.totalCharge, usage.exceedsAutoTier);
   * ```
   */
  readonly licenses = new IntegratorLicensesResource(this.config);
}

/** `GET/DELETE /api/v1/integrator/keys`. */
export class IntegratorKeysResource extends BaseResource {
  /** List all API keys for the current integrator. Requires `firms:manage`. */
  list(): Promise<IntegratorKeysResponse> {
    return this.request("GET", "/integrator/keys");
  }

  /**
   * Deactivate an integrator API key by UUID (`keyId`) or `sk_int_*` prefix (`client_id`).
   */
  deactivate(body: DeactivateIntegratorKeyRequest): Promise<DeactivateIntegratorKeyResponse> {
    return this.request("DELETE", "/integrator/keys", body);
  }
}

/**
 * `GET /api/v1/integrator/licenses/info` and friends.
 *
 * Progressive rates are applied separately to aggregate outbound and inbound
 * counts across the integrator's production `integrator-managed` firms.
 * Managed sandbox usage is returned separately and excluded from billing.
 */
export class IntegratorLicensesResource extends BaseResource {
  /**
   * Aggregate plan + current-period usage across every firm the integrator
   * manages. Requires `account:read` scope on a `sk_int_*` key.
   *
   * @param params - Optional `offset` / `limit` for the per-firm breakdown
   * @returns Plan, period, billed and sandbox aggregates, a production
   *          estimate, signed pricing projection, and paginated firm rows.
   *
   * @example
   * ```ts
   * const usage = await client.integrator.licenses.info({ limit: 100 });
   * console.log(usage.pricing.scheduleVersion, usage.productionEstimate.totalCharge);
   * ```
   */
  info(params?: IntegratorLicenseInfoParams): Promise<IntegratorLicenseInfo> {
    return this.request(
      "GET",
      `/integrator/licenses/info${buildQuery({
        offset: params?.offset,
        limit: params?.limit,
      })}`,
    );
  }
}
