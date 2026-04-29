import { createHash, randomBytes } from "node:crypto";
import type { TokenResponse } from "../types.js";
import { EPostakError } from "../utils/errors.js";

/**
 * Helpers for the **integrator-initiated** OAuth `authorization_code` + PKCE
 * flow. Use these from your own backend when you want to onboard an
 * end-user firm into ePošťák from inside your own application — the user
 * clicks a "Connect ePošťák" button in your UI, lands on our `/oauth/authorize`
 * consent page, and we redirect back to your `redirect_uri` with a `code`.
 *
 * This is independent of the regular `EPostak.auth.token({ apiKey })` flow
 * (which uses `client_credentials`). Use one or the other depending on how
 * the firm is linked to you.
 *
 * The OAuth token endpoint lives at `https://epostak.sk/api/oauth/token` —
 * **not** under `/api/v1` — so this resource bypasses `EPostakConfig.baseUrl`.
 *
 * @example
 * ```typescript
 * import { OAuth } from "@epostak/sdk";
 *
 * // 1. On every onboarding attempt, generate a fresh PKCE pair.
 * const { codeVerifier, codeChallenge } = OAuth.generatePkce();
 * await sessions.put(req.session.id, codeVerifier);
 *
 * // 2. Build the authorize URL and redirect the user.
 * const url = OAuth.buildAuthorizeUrl({
 *   clientId: process.env.EPOSTAK_OAUTH_CLIENT_ID!,
 *   redirectUri: "https://your-app.com/oauth/epostak/callback",
 *   scope: "firm:read firm:manage document:send",
 *   state: req.session.id,
 *   codeChallenge,
 * });
 * res.redirect(url);
 *
 * // 3. On callback, exchange the code for a token pair.
 * const tokens = await OAuth.exchangeCode({
 *   code: req.query.code,
 *   codeVerifier: await sessions.get(req.query.state),
 *   clientId: process.env.EPOSTAK_OAUTH_CLIENT_ID!,
 *   clientSecret: process.env.EPOSTAK_OAUTH_CLIENT_SECRET!,
 *   redirectUri: "https://your-app.com/oauth/epostak/callback",
 * });
 * ```
 */
export class OAuth {
  /** Default origin for ePošťák OAuth endpoints. Override for staging. */
  static readonly DEFAULT_ORIGIN = "https://epostak.sk";

  /**
   * Generate a fresh PKCE code-verifier + S256 code-challenge pair.
   *
   * The `codeVerifier` is 43 base64url characters (≈256 bits of entropy).
   * Store it server-side keyed by `state` — you must NOT round-trip it
   * through the user's browser, that defeats PKCE.
   */
  static generatePkce(): { codeVerifier: string; codeChallenge: string } {
    const codeVerifier = randomBytes(32).toString("base64url");
    const codeChallenge = createHash("sha256")
      .update(codeVerifier)
      .digest("base64url");
    return { codeVerifier, codeChallenge };
  }

  /**
   * Build a `/oauth/authorize` URL the integrator can redirect the user to.
   * Uses `response_type=code`, `code_challenge_method=S256`.
   *
   * @param params.scope - Space-separated subset of registered scopes. Omit
   *   to receive the full registered scope list on the consent screen.
   * @param params.origin - Override the host (defaults to ePošťák production).
   */
  static buildAuthorizeUrl(params: {
    clientId: string;
    redirectUri: string;
    codeChallenge: string;
    state: string;
    scope?: string;
    origin?: string;
  }): string {
    const url = new URL(
      "/oauth/authorize",
      params.origin ?? OAuth.DEFAULT_ORIGIN,
    );
    url.searchParams.set("client_id", params.clientId);
    url.searchParams.set("redirect_uri", params.redirectUri);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("code_challenge", params.codeChallenge);
    url.searchParams.set("code_challenge_method", "S256");
    url.searchParams.set("state", params.state);
    if (params.scope) url.searchParams.set("scope", params.scope);
    return url.toString();
  }

  /**
   * Exchange an authorization `code` for a `TokenResponse` on the OAuth
   * token endpoint. Hits `${origin}/api/oauth/token` directly — does not
   * route through `EPostakConfig.baseUrl`, since OAuth lives outside `/api/v1`.
   *
   * The returned access token is a 15-minute JWT; the refresh token is
   * 30-day rotating. Persist both server-side keyed by your firm record.
   */
  static async exchangeCode(params: {
    code: string;
    codeVerifier: string;
    clientId: string;
    clientSecret: string;
    redirectUri: string;
    origin?: string;
  }): Promise<TokenResponse> {
    const url = new URL(
      "/api/oauth/token",
      params.origin ?? OAuth.DEFAULT_ORIGIN,
    );
    const body = new URLSearchParams({
      grant_type: "authorization_code",
      code: params.code,
      code_verifier: params.codeVerifier,
      client_id: params.clientId,
      client_secret: params.clientSecret,
      redirect_uri: params.redirectUri,
    });

    const res = await fetch(url.toString(), {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        Accept: "application/json",
      },
      body: body.toString(),
    });

    const text = await res.text();
    let parsed: unknown = {};
    try {
      parsed = text ? JSON.parse(text) : {};
    } catch {
      parsed = { error: { message: text } };
    }

    if (!res.ok) {
      throw new EPostakError(
        res.status,
        parsed as Record<string, unknown>,
        res.headers,
      );
    }
    return parsed as TokenResponse;
  }
}
