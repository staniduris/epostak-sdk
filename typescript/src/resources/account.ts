import { BaseResource } from "../utils/request.js";
import type { Account } from "../types.js";

/**
 * Resource for retrieving account information — firm details, subscription
 * plan, and document usage for the current billing period.
 *
 * For key introspection, OAuth token minting, and key rotation see
 * `client.auth.*`.
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
   */
  get(): Promise<Account> {
    return this.request("GET", "/account");
  }

  licenseInfo(): Promise<Record<string, unknown>> {
    return this.request("GET", "/licenses/info");
  }
}
