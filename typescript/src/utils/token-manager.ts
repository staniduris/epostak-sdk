import type { TokenResponse } from "../types.js";

const TOKEN_PATH = "/auth/token";
const RENEW_PATH = "/auth/renew";
const REFRESH_BUFFER_MS = 60_000;

export interface TokenManagerConfig {
  clientId: string;
  clientSecret: string;
  baseUrl: string;
  scope?: string;
}

export class TokenManager {
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private expiresAt = 0;
  private mintPromise: Promise<void> | null = null;

  constructor(private config: TokenManagerConfig) {}

  async getAccessToken(): Promise<string> {
    if (this.accessToken && Date.now() < this.expiresAt - REFRESH_BUFFER_MS) {
      return this.accessToken;
    }

    if (this.mintPromise) {
      await this.mintPromise;
      return this.accessToken!;
    }

    this.mintPromise = this.refreshOrMint();
    try {
      await this.mintPromise;
    } finally {
      this.mintPromise = null;
    }
    return this.accessToken!;
  }

  private async refreshOrMint(): Promise<void> {
    if (this.refreshToken && this.accessToken) {
      try {
        await this.doRenew();
        return;
      } catch {
        // refresh failed — fall through to full mint
      }
    }
    await this.doMint();
  }

  private async doMint(): Promise<void> {
    const url = `${this.config.baseUrl}${TOKEN_PATH}`;
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };

    const body: Record<string, string> = {
      grant_type: "client_credentials",
      client_id: this.config.clientId,
      client_secret: this.config.clientSecret,
    };
    if (this.config.scope) {
      body.scope = this.config.scope;
    }

    const res = await fetch(url, {
      method: "POST",
      headers,
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText);
      throw new Error(`Token mint failed (${res.status}): ${text}`);
    }

    const data = (await res.json()) as TokenResponse;
    this.applyTokenResponse(data);
  }

  private async doRenew(): Promise<void> {
    const url = `${this.config.baseUrl}${RENEW_PATH}`;
    const res = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${this.accessToken}`,
      },
      body: JSON.stringify({
        grant_type: "refresh_token",
        refresh_token: this.refreshToken,
      }),
    });

    if (!res.ok) {
      throw new Error(`Token renew failed (${res.status})`);
    }

    const data = (await res.json()) as TokenResponse;
    this.applyTokenResponse(data);
  }

  private applyTokenResponse(data: TokenResponse): void {
    this.accessToken = data.access_token;
    this.refreshToken = data.refresh_token;
    this.expiresAt = Date.now() + data.expires_in * 1000;
  }
}
