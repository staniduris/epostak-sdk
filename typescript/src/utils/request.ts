import { EPostakError } from "./errors.js";

/** Internal configuration passed to all resource classes. */
export interface ClientConfig {
  /** Base URL for the API (e.g. `"https://epostak.sk/api/enterprise"`) */
  baseUrl: string;
  /** API key for authentication (`sk_live_*` or `sk_int_*`) */
  apiKey: string;
  /** Optional firm UUID for integrator keys — sent as `X-Firm-Id` header */
  firmId: string | undefined;
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
   * @param options - Additional options for headers or raw response handling
   * @returns Parsed JSON response, or raw Response if `rawResponse` is true
   */
  protected request<T>(
    method: string,
    path: string,
    body?: unknown,
    options?: { headers?: Record<string, string>; rawResponse?: boolean },
  ): Promise<T> {
    return request<T>(
      this.config.baseUrl,
      this.config.apiKey,
      this.config.firmId,
      method,
      path,
      body,
      options,
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

/**
 * Core fetch wrapper used by all resource methods.
 * Handles Bearer authentication, `X-Firm-Id` header for integrator keys,
 * JSON serialization, FormData pass-through, and error normalization
 * into {@link EPostakError} instances.
 *
 * @param baseUrl - API base URL
 * @param apiKey - Bearer token for authentication
 * @param firmId - Optional firm UUID sent as `X-Firm-Id` header
 * @param method - HTTP method (GET, POST, PATCH, DELETE)
 * @param path - API endpoint path (appended to baseUrl)
 * @param body - Request body — JSON-serializable object or FormData
 * @param options - Additional headers or `rawResponse: true` to return the raw `Response`
 * @returns Parsed JSON response of type `T`, raw `Response` if rawResponse is true, or `undefined` for 204 responses
 * @throws {EPostakError} On non-2xx responses or network errors
 */
export async function request<T>(
  baseUrl: string,
  apiKey: string,
  firmId: string | undefined,
  method: string,
  path: string,
  body?: unknown,
  options?: { headers?: Record<string, string>; rawResponse?: boolean },
): Promise<T> {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${apiKey}`,
    ...options?.headers,
  };

  if (firmId) {
    headers["X-Firm-Id"] = firmId;
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

  let res: Response;
  try {
    res = await fetch(`${baseUrl}${path}`, {
      method,
      headers,
      body: fetchBody,
    });
  } catch (err) {
    throw new EPostakError(0, {
      error: err instanceof Error ? err.message : "Network error",
    });
  }

  if (!res.ok) {
    let errorBody: Record<string, unknown> = {};
    try {
      errorBody = (await res.json()) as Record<string, unknown>;
    } catch {
      errorBody = { error: res.statusText };
    }
    throw new EPostakError(res.status, errorBody);
  }

  if (options?.rawResponse) {
    return res as unknown as T;
  }

  // Handle 204 No Content (e.g. DELETE ack, batch-ack)
  if (res.status === 204 || res.headers.get("content-length") === "0") {
    return undefined as unknown as T;
  }

  return res.json() as Promise<T>;
}
