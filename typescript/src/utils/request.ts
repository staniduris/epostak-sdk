import { EPostakError, buildApiError } from "./errors.js";
import type { TokenManager } from "./token-manager.js";

/** Internal configuration passed to all resource classes. */
export interface ClientConfig {
  /** Base URL for the API (e.g. `"https://epostak.sk/api/v1"`) */
  baseUrl: string;
  /** Token manager that provides JWT access tokens. */
  tokenManager: TokenManager;
  /** Optional firm UUID for integrator keys — sent as `X-Firm-Id` header */
  firmId: string | undefined;
  /** Maximum number of retries on 429/5xx errors. Default 3, set 0 to disable. */
  maxRetries: number;
}

const DEFAULT_MAX_RETRIES = 3;
const BASE_DELAY_MS = 500;
const MAX_DELAY_MS = 30_000;
/** HTTP methods retried by default (idempotent / safe). */
const DEFAULT_RETRYABLE_METHODS = new Set(["GET", "DELETE"]);

/** Per-request options accepted by `BaseResource.request`. */
export interface RequestOptions {
  /** Extra headers to merge into the request. */
  headers?: Record<string, string>;
  /** Return the raw `Response` instead of parsing JSON. */
  rawResponse?: boolean;
  /** Allow retries even for non-idempotent methods (POST/PATCH/PUT). */
  retry?: boolean;
  /**
   * Optional client-chosen replay-safety token. Sent as the `Idempotency-Key`
   * HTTP header. The server returns `409` (surfaced as
   * `EPostakError` with code `idempotency_conflict`) when the same key is
   * replayed before the original request finishes.
   */
  idempotencyKey?: string;
}

/**
 * Base class for all API resource classes. Provides the authenticated `request()`
 * method that handles headers, serialization, and error handling.
 */
export class BaseResource {
  /**
   * @param config - Client configuration with API key, base URL, and optional firm ID
   */
  constructor(protected config: ClientConfig) {}

  /**
   * Make an authenticated API request. Used internally by all resource methods.
   *
   * @param method - HTTP method (GET, POST, PATCH, DELETE)
   * @param path - API endpoint path (appended to baseUrl)
   * @param body - Request body (JSON-serializable object or FormData)
   * @param options - Additional options for headers, raw response, retry, idempotency
   * @returns Parsed JSON response, or raw Response if `rawResponse` is true
   */
  protected request<T>(
    method: string,
    path: string,
    body?: unknown,
    options?: RequestOptions,
  ): Promise<T> {
    return request<T>(
      this.config.baseUrl,
      this.config.tokenManager,
      this.config.firmId,
      method,
      path,
      body,
      {
        ...options,
        maxRetries: this.config.maxRetries,
      },
    );
  }
}

/**
 * Build a query string from a params object, skipping undefined values.
 * Number values are coerced to strings automatically.
 */
export function buildQuery(
  params: Record<string, string | number | undefined>,
): string {
  const qs = new URLSearchParams();
  for (const [key, val] of Object.entries(params)) {
    if (val !== undefined) qs.set(key, String(val));
  }
  const s = qs.toString();
  return s ? `?${s}` : "";
}

/** Sleep helper for retry backoff. */
function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Compute delay for a retry attempt with exponential backoff + jitter. */
function retryDelay(attempt: number): number {
  const jitter = Math.random() * BASE_DELAY_MS;
  return Math.min(BASE_DELAY_MS * 2 ** attempt + jitter, MAX_DELAY_MS);
}

/** Whether the HTTP status code is retryable (429 or 5xx). */
function isRetryableStatus(status: number): boolean {
  return status === 429 || status >= 500;
}

/**
 * Core fetch wrapper used by all resource methods.
 * Handles Bearer authentication, `X-Firm-Id` header for integrator keys,
 * `Idempotency-Key`, JSON serialization, FormData pass-through, error
 * normalization into {@link EPostakError} instances (including RFC 7807
 * envelopes and `WWW-Authenticate: insufficient_scope`), and automatic
 * retries with exponential backoff on 429 / 5xx responses.
 *
 * @returns Parsed JSON response of type `T`, raw `Response` if `rawResponse`
 *   is true, or `undefined` for 204 responses
 * @throws {EPostakError} On non-2xx responses (after exhausting retries) or
 *   network errors. `409` responses with `code: "idempotency_conflict"` are
 *   surfaced verbatim — callers can detect them via `err.code`.
 */
export async function request<T>(
  baseUrl: string,
  tokenManagerOrKey: TokenManager | string,
  firmId: string | undefined,
  method: string,
  path: string,
  body?: unknown,
  options?: RequestOptions & { maxRetries?: number },
): Promise<T> {
  const bearer =
    typeof tokenManagerOrKey === "string"
      ? tokenManagerOrKey
      : await tokenManagerOrKey.getAccessToken();
  const headers: Record<string, string> = {
    Authorization: `Bearer ${bearer}`,
    ...options?.headers,
  };

  if (firmId) {
    headers["X-Firm-Id"] = firmId;
  }

  if (options?.idempotencyKey) {
    headers["Idempotency-Key"] = options.idempotencyKey;
  }

  if (body !== undefined && !(body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  let fetchBody: BodyInit | null = null;
  if (body instanceof FormData) {
    fetchBody = body;
  } else if (body !== undefined) {
    fetchBody = JSON.stringify(body);
  }

  const maxRetries = options?.maxRetries ?? DEFAULT_MAX_RETRIES;
  const upperMethod = method.toUpperCase();
  const canRetry =
    maxRetries > 0 &&
    (DEFAULT_RETRYABLE_METHODS.has(upperMethod) || options?.retry === true);

  let attempt = 0;

  // eslint-disable-next-line no-constant-condition
  while (true) {
    let res: Response;
    try {
      res = await fetch(`${baseUrl}${path}`, {
        method,
        headers,
        body: fetchBody,
      });
    } catch (err) {
      if (canRetry && attempt < maxRetries) {
        await sleep(retryDelay(attempt));
        attempt++;
        continue;
      }
      throw new EPostakError(0, {
        error: err instanceof Error ? err.message : "Network error",
      });
    }

    // Retry on retryable status codes if we have attempts left.
    if (canRetry && isRetryableStatus(res.status) && attempt < maxRetries) {
      const retryAfterHeader = res.headers.get("Retry-After");
      let delayMs: number;
      if (retryAfterHeader) {
        const retryAfterSeconds = Number(retryAfterHeader);
        delayMs = Number.isFinite(retryAfterSeconds)
          ? retryAfterSeconds * 1000
          : retryDelay(attempt);
      } else {
        delayMs = retryDelay(attempt);
      }
      await sleep(delayMs);
      attempt++;
      continue;
    }

    if (!res.ok) {
      let errorBody: Record<string, unknown> = {};
      try {
        errorBody = (await res.json()) as Record<string, unknown>;
      } catch {
        errorBody = { error: res.statusText };
      }
      throw buildApiError(res.status, errorBody, res.headers);
    }

    if (options?.rawResponse) {
      return res as unknown as T;
    }

    if (res.status === 204 || res.headers.get("content-length") === "0") {
      return undefined as unknown as T;
    }

    return res.json() as Promise<T>;
  }
}
