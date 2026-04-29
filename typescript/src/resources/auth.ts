import { BaseResource, request } from "../utils/request.js";
import type { ClientConfig } from "../utils/request.js";
import type {
  AuthStatusResponse,
  RotateSecretResponse,
  TokenResponse,
  RevokeResponse,
  IpAllowlistResponse,
} from "../types.js";

export class IpAllowlistResource extends BaseResource {
  get(): Promise<IpAllowlistResponse> {
    return this.request("GET", "/auth/ip-allowlist");
  }

  update(params: { cidrs: string[] }): Promise<IpAllowlistResponse> {
    return this.request("PUT", "/auth/ip-allowlist", {
      ip_allowlist: params.cidrs,
    });
  }
}

/**
 * OAuth client_credentials flow and key management.
 *
 * The SDK automatically mints and refreshes JWTs via the token manager.
 * Use `auth.token()` only when you need to manually obtain tokens (e.g.
 * for a separate service that will use the JWT directly).
 */
export class AuthResource extends BaseResource {
  ipAllowlist: IpAllowlistResource;

  constructor(config: ClientConfig) {
    super(config);
    this.ipAllowlist = new IpAllowlistResource(config);
  }

  /**
   * Manually mint an OAuth access token via `client_credentials` grant.
   *
   * Most users don't need this — the SDK auto-mints tokens internally.
   * Use this when you need a JWT for a separate service.
   *
   * @param params.clientId - The API key ID (UUID) or key prefix
   * @param params.clientSecret - The full API key (`sk_live_*` or `sk_int_*`)
   * @param params.firmId - Required for `sk_int_*` keys
   * @param params.scope - Optional space-separated scope subset
   */
  token(params: {
    clientId: string;
    clientSecret: string;
    firmId?: string;
    scope?: string;
  }): Promise<TokenResponse> {
    const headers: Record<string, string> = {};
    if (params.firmId) headers["X-Firm-Id"] = params.firmId;
    return request<TokenResponse>(
      this.config.baseUrl,
      params.clientSecret,
      undefined,
      "POST",
      "/auth/token",
      {
        grant_type: "client_credentials",
        client_id: params.clientId,
        client_secret: params.clientSecret,
        ...(params.scope ? { scope: params.scope } : {}),
      },
      { headers, maxRetries: this.config.maxRetries },
    );
  }

  renew(params: { refreshToken: string }): Promise<TokenResponse> {
    return this.request<TokenResponse>("POST", "/auth/renew", {
      grant_type: "refresh_token",
      refresh_token: params.refreshToken,
    });
  }

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

  status(): Promise<AuthStatusResponse> {
    return this.request("GET", "/auth/status");
  }

  rotateSecret(): Promise<RotateSecretResponse> {
    return this.request("POST", "/auth/rotate-secret");
  }
}
