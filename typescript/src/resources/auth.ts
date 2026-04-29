import { BaseResource, request } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  AuthStatusResponse,
  RotateSecretResponse,
  TokenResponse,
  RevokeResponse,
  IpAllowlistResponse,
} from "../types.js";

/**
 * Sub-resource for managing the per-key IP allowlist (Wave 3.1).
 *
 * An empty list means "no IP restriction" — any caller IP is accepted. When
 * the list is non-empty, requests authenticated with this key are rejected
 * (`403`) unless the source IP matches at least one entry. Each entry is
 * either a bare IPv4/IPv6 address or a CIDR block (`addr/prefix`).
 */
export class IpAllowlistResource extends BaseResource {
  /**
   * Read the current IP allowlist for the calling API key.
   *
   * @example
   * ```typescript
   * const { ip_allowlist } = await client.auth.ipAllowlist.get();
   * ```
   */
  get(): Promise<IpAllowlistResponse> {
    return this.request("GET", "/auth/ip-allowlist");
  }

  /**
   * Replace the IP allowlist for the calling API key. Pass an empty array
   * to clear the restriction. Maximum 50 entries; each must be either a
   * bare IP or a valid CIDR.
   *
   * @example
   * ```typescript
   * await client.auth.ipAllowlist.update({
   *   cidrs: ["192.168.1.0/24", "203.0.113.42"],
   * });
   * ```
   */
  update(params: { cidrs: string[] }): Promise<IpAllowlistResponse> {
    return this.request("PUT", "/auth/ip-allowlist", {
      ip_allowlist: params.cidrs,
    });
  }
}

/**
 * Resource for the OAuth client_credentials flow and key management.
 *
 * The token mint endpoint accepts the API key as the `client_secret` of an
 * OAuth `client_credentials` exchange. ePošťák returns a short-lived JWT
 * access token (`expires_in: 900` seconds) and a 30-day rotating refresh
 * token. Use `auth.token()` once at startup, cache the access token, and
 * call `auth.renew()` before it expires.
 *
 * @example
 * ```typescript
 * const client = new EPostak({ apiKey: "sk_live_xxxxx" });
 *
 * const tokens = await client.auth.token({ apiKey: "sk_live_xxxxx" });
 * console.log(tokens.access_token, tokens.expires_in);
 *
 * // Later, before the access token expires:
 * const renewed = await client.auth.renew({
 *   refreshToken: tokens.refresh_token,
 * });
 *
 * // On logout / key rotation:
 * await client.auth.revoke({ token: tokens.refresh_token,
 *   tokenTypeHint: "refresh_token" });
 * ```
 */
export class AuthResource extends BaseResource {
  /** Sub-resource for the per-key IP allowlist. */
  ipAllowlist: IpAllowlistResource;

  constructor(config: ClientConfig) {
    super(config);
    this.ipAllowlist = new IpAllowlistResource(config);
  }

  /**
   * Mint an OAuth access token via the `client_credentials` grant.
   *
   * The API key is sent as both the `Authorization: Bearer` header and the
   * `client_secret` body field — the server accepts either, but doubling
   * up keeps the SDK compatible across spec revisions. For integrator keys
   * (`sk_int_*`) you must also pass `firmId`, which is forwarded as
   * `X-Firm-Id` so the issued JWT is bound to the right tenant.
   *
   * @returns Access + refresh token pair, scope, and `expires_in` (seconds).
   *
   * @example
   * ```typescript
   * // sk_live_* — direct firm access
   * const tokens = await client.auth.token({ apiKey: "sk_live_xxxxx" });
   *
   * // sk_int_* — integrator acting on behalf of a managed firm
   * const tokens = await client.auth.token({
   *   apiKey: "sk_int_xxxxx",
   *   firmId: "client-firm-uuid",
   * });
   * ```
   */
  token(params: {
    apiKey: string;
    firmId?: string;
    /** Optional space-separated scope subset (defaults to the key's own scopes). */
    scope?: string;
  }): Promise<TokenResponse> {
    const headers: Record<string, string> = {};
    if (params.firmId) headers["X-Firm-Id"] = params.firmId;
    return request<TokenResponse>(
      this.config.baseUrl,
      params.apiKey,
      undefined,
      "POST",
      "/auth/token",
      {
        grant_type: "client_credentials",
        client_id: params.apiKey,
        client_secret: params.apiKey,
        ...(params.scope ? { scope: params.scope } : {}),
      },
      { headers, maxRetries: this.config.maxRetries },
    );
  }

  /**
   * Exchange a refresh token for a new access + refresh pair. The old
   * refresh token is invalidated server-side, so always replace your stored
   * refresh token with the value returned by this call.
   *
   * @example
   * ```typescript
   * const renewed = await client.auth.renew({ refreshToken: stored });
   * await secrets.put("epostak_refresh", renewed.refresh_token);
   * ```
   */
  renew(params: { refreshToken: string }): Promise<TokenResponse> {
    return this.request<TokenResponse>("POST", "/auth/renew", {
      grant_type: "refresh_token",
      refresh_token: params.refreshToken,
    });
  }

  /**
   * Revoke an access or refresh token. Idempotent — a 200 is returned even
   * if the token is unknown or already revoked, so this is safe to call
   * unconditionally on logout. Pass `tokenTypeHint` when you know which
   * variant the token is; the server will skip the auto-detect path.
   *
   * @example
   * ```typescript
   * await client.auth.revoke({
   *   token: storedRefresh,
   *   tokenTypeHint: "refresh_token",
   * });
   * ```
   */
  revoke(params: {
    token: string;
    tokenTypeHint?: "access_token" | "refresh_token";
  }): Promise<RevokeResponse> {
    return this.request<RevokeResponse>("POST", "/auth/revoke", {
      token: params.token,
      ...(params.tokenTypeHint
        ? { token_type_hint: params.tokenTypeHint }
        : {}),
    });
  }

  /**
   * Introspect the calling API key without revealing the plaintext secret.
   * Returns the key metadata, the firm it is bound to, the current plan,
   * and — for integrator keys — the integrator summary.
   *
   * @example
   * ```typescript
   * const status = await client.auth.status();
   * console.log(status.key.prefix, status.plan.name);
   * ```
   */
  status(): Promise<AuthStatusResponse> {
    return this.request("GET", "/auth/status");
  }

  /**
   * Rotate the calling API key. The previous key is deactivated immediately
   * and the new plaintext key is returned ONCE — store it in your secret
   * manager before continuing. Integrator keys (`sk_int_*`) are rejected
   * with `403`; rotate those through the integrator dashboard instead.
   *
   * @example
   * ```typescript
   * const rotated = await client.auth.rotateSecret();
   * await secrets.put("epostak_api_key", rotated.key);
   * ```
   */
  rotateSecret(): Promise<RotateSecretResponse> {
    return this.request("POST", "/auth/rotate-secret");
  }
}
