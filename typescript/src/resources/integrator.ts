import { BaseResource, buildQuery } from "../utils/request.js";
import type {
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

/**
 * `GET /api/v1/integrator/licenses/info` and friends.
 *
 * Tier rates are applied to the AGGREGATE document count across all the
 * integrator's `integrator-managed` firms — not per-firm. A 100-firm × 50-doc
 * integrator lands in tier 2–3, not tier 1 like a standalone firm would.
 * Volumes above `contactThreshold` (5 000) flip `exceedsAutoTier` to `true`,
 * auto-billing pauses there and sales handles the contract manually.
 */
export class IntegratorLicensesResource extends BaseResource {
  /**
   * Aggregate plan + current-period usage across every firm the integrator
   * manages. Requires `account:read` scope on a `sk_int_*` key.
   *
   * @param params - Optional `offset` / `limit` for the per-firm breakdown
   * @returns Plan, period, billable aggregate, non-managed aggregate, tiers,
   *          and a paginated per-firm list (sorted by outbound count desc).
   *
   * @example
   * ```ts
   * const usage = await client.integrator.licenses.info({ limit: 100 });
   * if (usage.exceedsAutoTier) {
   *   // sales review required — auto-billing has paused
   * }
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
