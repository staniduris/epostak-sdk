import { EPostakError } from "./errors.js";

export interface ClientConfig {
  baseUrl: string;
  apiKey: string;
  firmId: string | undefined;
}

export class BaseResource {
  constructor(protected config: ClientConfig) {}

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
 * Handles auth headers, JSON serialisation, FormData pass-through, and error
 * normalisation.
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
