/**
 * Error thrown when an ePosťak API request fails.
 * Extends the native `Error` class with HTTP status, API error code, and details.
 *
 * The `message` property contains the human-readable error message from the API.
 * For network errors (no response), `status` is `0`.
 *
 * @example
 * ```typescript
 * try {
 *   await client.documents.send({ ... });
 * } catch (err) {
 *   if (err instanceof EPostakError) {
 *     console.error(`HTTP ${err.status}: ${err.message}`);
 *     if (err.code === 'VALIDATION_ERROR') {
 *       console.error('Details:', err.details);
 *     }
 *   }
 * }
 * ```
 */
export class EPostakError extends Error {
  /** HTTP status code (e.g. `400`, `401`, `404`, `500`). `0` indicates a network error. */
  status: number;
  /** Machine-readable error code from the API (e.g. `"VALIDATION_ERROR"`, `"NOT_FOUND"`) */
  code?: string;
  /** Additional error details — typically an object with field-level validation messages */
  details?: unknown;

  /**
   * @param status - HTTP status code from the response (or `0` for network errors)
   * @param body - Parsed JSON error body from the API response
   */
  constructor(status: number, body: Record<string, unknown>) {
    const errorObj = body?.error;
    const errorObjMap =
      errorObj !== null && typeof errorObj === "object"
        ? (errorObj as Record<string, unknown>)
        : null;

    const msg =
      typeof errorObj === "string"
        ? errorObj
        : errorObjMap && "message" in errorObjMap
          ? String(errorObjMap.message)
          : "API request failed";

    super(msg);
    this.name = "EPostakError";
    this.status = status;

    if (errorObjMap) {
      if ("code" in errorObjMap) this.code = String(errorObjMap.code);
      if ("details" in errorObjMap) this.details = errorObjMap.details;
    }
  }
}
