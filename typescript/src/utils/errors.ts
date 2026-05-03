/**
 * Error thrown when an ePošťák API request fails.
 *
 * The SDK normalizes both the legacy `{ error: { code, message, details } }`
 * envelope and the RFC 7807 `application/problem+json` envelope
 * (`{ type, title, status, detail, instance }`) into the same shape.
 *
 * - `message` — human-readable error message (from `error.message` / `title` /
 *   `detail`, in that order of preference).
 * - `status` — HTTP status code. `0` indicates a client-side network error.
 * - `code` — machine-readable error code (e.g. `"VALIDATION_ERROR"`,
 *   `"idempotency_conflict"`, `"insufficient_scope"`).
 * - `details` — optional additional payload (typically a list of field-level
 *   validation messages or schematron rule IDs).
 * - `requestId` — server-assigned request identifier — present whenever the
 *   server returns `X-Request-Id`.
 * - RFC 7807 fields: `type`, `title`, `detail`, `instance`.
 * - `requiredScope` — set when the server returns `403` with a
 *   `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."` header.
 *
 * @example
 * ```typescript
 * try {
 *   await client.documents.send({ ... });
 * } catch (err) {
 *   if (err instanceof EPostakError) {
 *     console.error(`HTTP ${err.status} (${err.code}): ${err.message}`);
 *     if (err.code === "VALIDATION_FAILED") {
 *       console.error("Schematron errors:", err.details);
 *     } else if (err.code === "idempotency_conflict") {
 *       console.error("Request still in flight, retry later.");
 *     } else if (err.requiredScope) {
 *       console.error(`Missing scope: ${err.requiredScope}`);
 *     }
 *   }
 * }
 * ```
 */
export class EPostakError extends Error {
  /** HTTP status code (e.g. `400`, `401`, `404`, `500`). `0` on network errors. */
  status: number;
  /** Machine-readable error code from the API. */
  code?: string;
  /** Additional error details — typically a list of field-level validation messages. */
  details?: unknown;
  /** Server-assigned request ID — set whenever the server emits `X-Request-Id`. */
  requestId?: string;

  // RFC 7807 (application/problem+json) fields — populated when the server
  // returns a problem+json envelope. Otherwise undefined.
  /** RFC 7807 `type` — URI reference identifying the problem type. */
  type?: string;
  /** RFC 7807 `title` — short, human-readable summary. */
  title?: string;
  /** RFC 7807 `detail` — human-readable explanation of this specific occurrence. */
  detail?: string;
  /** RFC 7807 `instance` — URI reference identifying this specific occurrence. */
  instance?: string;

  /**
   * Required OAuth scope when the server rejects with `403 insufficient_scope`.
   * Parsed from the `WWW-Authenticate: Bearer error="insufficient_scope"
   * scope="..."` header. `null` when the header is absent or the rejection
   * was for a different reason.
   */
  requiredScope: string | null;

  /**
   * @param status - HTTP status code (or `0` for network errors)
   * @param body - Parsed JSON error body from the API response
   * @param headers - Optional response headers, used to extract `X-Request-Id`
   *   and to parse `WWW-Authenticate` for `requiredScope`.
   */
  constructor(
    status: number,
    body: Record<string, unknown>,
    headers?: Headers,
  ) {
    // RFC 7807 envelope: { type, title, status, detail, instance, ... }
    const isProblem =
      typeof body === "object" &&
      body !== null &&
      ("title" in body || "detail" in body) &&
      typeof body.error === "undefined";

    let msg = "API request failed";
    let code: string | undefined;
    let details: unknown;

    if (isProblem) {
      msg =
        (typeof body.title === "string" && body.title) ||
        (typeof body.detail === "string" && body.detail) ||
        msg;
      // Some routes carry `code` next to RFC 7807 fields.
      if (typeof body.code === "string") code = body.code;
      if ("errors" in body) details = body.errors;
    } else {
      const errorObj = body?.error;
      const errorObjMap =
        errorObj !== null && typeof errorObj === "object"
          ? (errorObj as Record<string, unknown>)
          : null;

      msg =
        typeof errorObj === "string"
          ? errorObj
          : errorObjMap && "message" in errorObjMap
            ? String(errorObjMap.message)
            : typeof body?.message === "string"
              ? body.message
              : msg;

      if (errorObjMap) {
        if ("code" in errorObjMap) code = String(errorObjMap.code);
        if ("details" in errorObjMap) details = errorObjMap.details;
      }
    }

    super(msg);
    this.name = "EPostakError";
    this.status = status;
    if (code !== undefined) this.code = code;
    if (details !== undefined) this.details = details;

    // RFC 7807 fields — copied through verbatim when present.
    if (typeof body?.type === "string") this.type = body.type;
    if (typeof body?.title === "string") this.title = body.title;
    if (typeof body?.detail === "string") this.detail = body.detail;
    if (typeof body?.instance === "string") this.instance = body.instance;

    // Server-assigned request ID — body wins, then header.
    if (typeof body?.requestId === "string") {
      this.requestId = body.requestId;
    } else if (
      typeof body?.error === "object" &&
      body.error !== null &&
      typeof (body.error as Record<string, unknown>).requestId === "string"
    ) {
      this.requestId = String(
        (body.error as Record<string, unknown>).requestId,
      );
    }
    if (!this.requestId) {
      const headerId = headers?.get("x-request-id");
      if (headerId) this.requestId = headerId;
    }

    // Parse WWW-Authenticate for OAuth `insufficient_scope` rejections.
    this.requiredScope = parseRequiredScope(headers);
    if (!this.requiredScope) {
      // Fall back to body fields used by the route's JSON envelope.
      const bodyScope =
        body && typeof body === "object"
          ? ((body as Record<string, unknown>).required_scope ??
            (body.error as Record<string, unknown> | undefined)?.required_scope)
          : undefined;
      if (typeof bodyScope === "string" && bodyScope.length > 0) {
        this.requiredScope = bodyScope;
      }
    }
  }
}

/**
 * Extract the `scope="..."` value from a
 * `WWW-Authenticate: Bearer error="insufficient_scope" scope="documents:send"`
 * header. Returns `null` if the header is absent or the rejection was for a
 * different reason (e.g. `error="invalid_token"`).
 */
function parseRequiredScope(headers?: Headers): string | null {
  if (!headers) return null;
  const raw = headers.get("www-authenticate");
  if (!raw) return null;
  if (!/error\s*=\s*"?insufficient_scope/i.test(raw)) return null;
  const m = raw.match(/scope\s*=\s*"([^"]+)"/i);
  return m ? m[1] : null;
}

/** Recipient identification on the existing duplicate invoice. */
export interface DuplicateInvoiceRecipient {
  peppolId: string | null;
  ico: string | null;
  name: string | null;
}

/** Existing document that caused the duplicate-invoice-number conflict. */
export interface DuplicateInvoiceExistingDocument {
  id: string;
  invoiceNumber: string;
  status: string;
  /** ISO 8601 timestamp — `peppolSentAt` if available, otherwise `createdAt`. */
  sentAt: string;
  recipient: DuplicateInvoiceRecipient | null;
}

/**
 * Thrown when `POST /api/v1/documents/send` (or the dashboard create
 * endpoint) rejects an outbound invoice whose `invoice_number` is already
 * in use for the firm. The conflict key is `(firmId, invoiceNumber)` —
 * recipient is intentionally NOT part of it; outbound numbering belongs
 * to the sender.
 *
 * @example
 * ```typescript
 * try {
 *   await client.documents.send({ invoiceNumber: "2026001", ... });
 * } catch (err) {
 *   if (err instanceof DuplicateInvoiceNumberError) {
 *     const existing = err.existingDocument;
 *     if (existing) {
 *       console.error(
 *         `Already sent on ${existing.sentAt}, document id ${existing.id}`,
 *       );
 *     }
 *   }
 * }
 * ```
 */
export class DuplicateInvoiceNumberError extends EPostakError {
  /** Always `["firmId", "invoiceNumber"]`. */
  conflictKey: string[];
  /**
   * The pre-existing outbound invoice that triggered the conflict.
   * `null` if the original was deleted between the constraint hit and
   * the lookup, or if the lookup itself failed.
   */
  existingDocument: DuplicateInvoiceExistingDocument | null;

  constructor(
    status: number,
    body: Record<string, unknown>,
    headers?: Headers,
  ) {
    super(status, body, headers);
    this.name = "DuplicateInvoiceNumberError";

    const errorObj = body?.error;
    const errorMap =
      errorObj !== null && typeof errorObj === "object" && !Array.isArray(errorObj)
        ? (errorObj as Record<string, unknown>)
        : {};

    this.conflictKey = Array.isArray(errorMap.conflictKey)
      ? (errorMap.conflictKey as unknown[]).map(String)
      : ["firmId", "invoiceNumber"];

    const ed = errorMap.existingDocument;
    if (ed && typeof ed === "object" && !Array.isArray(ed)) {
      const edMap = ed as Record<string, unknown>;
      const recipient =
        edMap.recipient && typeof edMap.recipient === "object"
          ? (edMap.recipient as Record<string, unknown>)
          : null;
      this.existingDocument = {
        id: String(edMap.id ?? ""),
        invoiceNumber: String(edMap.invoiceNumber ?? ""),
        status: String(edMap.status ?? ""),
        sentAt: String(edMap.sentAt ?? ""),
        recipient: recipient
          ? {
              peppolId:
                recipient.peppolId == null ? null : String(recipient.peppolId),
              ico: recipient.ico == null ? null : String(recipient.ico),
              name: recipient.name == null ? null : String(recipient.name),
            }
          : null,
      };
    } else {
      this.existingDocument = null;
    }
  }
}

/**
 * Build the right error subclass from a parsed API error body.
 * Falls back to {@link EPostakError} when no specialised mapping applies.
 */
export function buildApiError(
  status: number,
  body: Record<string, unknown>,
  headers?: Headers,
): EPostakError {
  const errorObj = body?.error;
  const code =
    errorObj !== null &&
    typeof errorObj === "object" &&
    "code" in (errorObj as Record<string, unknown>)
      ? String((errorObj as Record<string, unknown>).code)
      : undefined;
  if (code === "DUPLICATE_INVOICE_NUMBER") {
    return new DuplicateInvoiceNumberError(status, body, headers);
  }
  return new EPostakError(status, body, headers);
}
