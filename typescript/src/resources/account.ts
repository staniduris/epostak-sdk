import { BaseResource } from "../utils/request.js";
import type {
  Account,
  AuthStatusResponse,
  RotateSecretResponse,
} from "../types.js";

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

  /**
   * Introspect the calling API key without revealing the plaintext secret.
   * Returns metadata about the key, the firm it is bound to, the current
   * plan with expiration, the applicable per-minute rate limit, and —
   * when the key is an integrator key — the integrator summary.
   *
   * @returns Key introspection details
   *
   * @example
   * ```typescript
   * const status = await client.account.status();
   * console.log(`Key ${status.key.prefix} — plan: ${status.plan.name}`);
   * console.log(`Rate limit: ${status.rateLimit.perMinute}/${status.rateLimit.window}`);
   * if (status.integrator) {
   *   console.log(`Integrator: ${status.integrator.id}`);
   * }
   * ```
   */
  status(): Promise<AuthStatusResponse> {
    return this.request("GET", "/auth/status");
  }

  /**
   * Rotate the calling API key. Deactivates the current key and returns a
   * new plaintext `sk_live_*` key. The new key is returned ONCE — store it
   * immediately in a secret manager.
   *
   * Integrator keys (`sk_int_*`) are rejected with HTTP 403; they span
   * multiple firms and must be rotated through the integrator dashboard.
   *
   * @returns `{key, prefix, message}` — the new plaintext secret is returned once
   * @throws {EPostakError} 403 — when called with an integrator key
   *
   * @example
   * ```typescript
   * const rotated = await client.account.rotateSecret();
   * // Persist `rotated.key` to your secret store IMMEDIATELY — it is
   * // not retrievable later.
   * await secretStore.put('epostak_api_key', rotated.key);
   * ```
   */
  rotateSecret(): Promise<RotateSecretResponse> {
    return this.request("POST", "/auth/rotate-secret");
  }
}
