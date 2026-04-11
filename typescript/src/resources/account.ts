import { BaseResource } from "../utils/request.js";
import type { Account } from "../types.js";

/**
 * Resource for retrieving account information — firm details, subscription plan,
 * and document usage for the current billing period.
 *
 * @example
 * ```typescript
 * const account = await client.account.get();
 * console.log(`Plan: ${account.plan.name} (${account.plan.status})`);
 * console.log(`Usage: ${account.usage.outbound} sent, ${account.usage.inbound} received`);
 * ```
 */
export class AccountResource extends BaseResource {
  /**
   * Get account information for the authenticated API key.
   * Returns the associated firm, current subscription plan, and usage counters.
   *
   * @returns Account details including firm, plan, and usage
   *
   * @example
   * ```typescript
   * const account = await client.account.get();
   * if (account.plan.status === 'expired') {
   *   console.warn('Subscription expired — renew to continue sending documents');
   * }
   * ```
   */
  get(): Promise<Account> {
    return this.request("GET", "/account");
  }
}
