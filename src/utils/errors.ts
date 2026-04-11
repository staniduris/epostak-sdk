/**
 * Error thrown when an API request fails.
 */
export class EPostakError extends Error {
  /** HTTP status code */
  status: number;
  /** Machine-readable error code from the API */
  code?: string;
  /** Additional error details (validation messages, etc.) */
  details?: unknown;

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
