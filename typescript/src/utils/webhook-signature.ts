import { createHmac, timingSafeEqual } from "node:crypto";

/** Options for {@link verifyWebhookSignature}. */
export interface VerifyWebhookSignatureOptions {
  /** Raw request body, exactly as bytes were received off the wire. */
  payload: Buffer | string;
  /** Value of the `X-Webhook-Signature` header — `sha256=<hex>`. */
  signature: string;
  /** Value of the `X-Webhook-Timestamp` header — Unix seconds. */
  timestamp: string;
  /** The webhook signing secret, captured at webhook-creation time. */
  secret: string;
  /**
   * Maximum age of the signature in seconds. Defaults to `300` (5 minutes)
   * which matches the server-side replay window. Set `0` to disable the
   * timestamp check (not recommended in production).
   */
  toleranceSeconds?: number;
}

/**
 * Result of {@link verifyWebhookSignature}.
 *
 * `valid === true` means the body/signature/secret line up AND the timestamp
 * is within tolerance. On `false` the `reason` explains why so callers can
 * log it; the SDK never throws on bad signatures.
 */
export interface VerifyWebhookSignatureResult {
  /** Whether the signature is valid AND the timestamp is within tolerance. */
  valid: boolean;
  /** Reason the signature was rejected — only set when `valid === false`. */
  reason?:
    | "missing_header"
    | "malformed_header"
    | "unsupported_algorithm"
    | "signature_mismatch"
    | "timestamp_outside_tolerance";
  /** Parsed timestamp from the header, in seconds since the epoch. */
  timestamp?: number;
}

/**
 * Verify an ePošťák webhook payload using HMAC-SHA256 with timing-safe compare.
 *
 * Server signs with HMAC-SHA256 over `${timestamp}.${rawBody}` and ships:
 *   - `X-Webhook-Signature: sha256=<hex>`
 *   - `X-Webhook-Timestamp: <unix_seconds>`
 *
 * Use the bytes exactly as received off the wire — do NOT re-serialize the
 * parsed JSON, the round-trip will reorder keys and mutate whitespace.
 *
 * @example
 * ```typescript
 * import { verifyWebhookSignature } from "@epostak/sdk";
 * import express from "express";
 *
 * const app = express();
 *
 * app.post(
 *   "/webhooks/epostak",
 *   express.raw({ type: "application/json" }),
 *   (req, res) => {
 *     const result = verifyWebhookSignature({
 *       payload: req.body, // Buffer — express.raw gives raw bytes
 *       signature: req.header("x-webhook-signature") ?? "",
 *       timestamp: req.header("x-webhook-timestamp") ?? "",
 *       secret: process.env.EPOSTAK_WEBHOOK_SECRET!,
 *     });
 *     if (!result.valid) {
 *       return res.status(400).send(`bad signature: ${result.reason}`);
 *     }
 *     const event = JSON.parse(req.body.toString("utf8"));
 *     // process event...
 *     res.status(204).end();
 *   },
 * );
 * ```
 */
export function verifyWebhookSignature(
  options: VerifyWebhookSignatureOptions,
): VerifyWebhookSignatureResult {
  const { payload, signature, timestamp, secret } = options;
  const tolerance = options.toleranceSeconds ?? 300;

  if (!signature || !timestamp) {
    return { valid: false, reason: "missing_header" };
  }

  // Parse "sha256=<hex>" — server format. Reject any other algorithm.
  const eqIdx = signature.indexOf("=");
  if (eqIdx < 0) {
    return { valid: false, reason: "malformed_header" };
  }
  const algo = signature.slice(0, eqIdx).trim().toLowerCase();
  const sigHex = signature.slice(eqIdx + 1).trim();
  if (algo !== "sha256") {
    return { valid: false, reason: "unsupported_algorithm" };
  }
  if (sigHex.length === 0 || !/^[0-9a-f]+$/i.test(sigHex)) {
    return { valid: false, reason: "malformed_header" };
  }

  const ts = Number(timestamp);
  if (!Number.isFinite(ts)) {
    return { valid: false, reason: "malformed_header" };
  }

  if (tolerance > 0) {
    const nowSec = Math.floor(Date.now() / 1000);
    if (Math.abs(nowSec - ts) > tolerance) {
      return {
        valid: false,
        reason: "timestamp_outside_tolerance",
        timestamp: ts,
      };
    }
  }

  const payloadBuf =
    typeof payload === "string" ? Buffer.from(payload, "utf8") : payload;

  const signed = Buffer.concat([
    Buffer.from(`${timestamp}.`, "utf8"),
    payloadBuf,
  ]);
  const expected = createHmac("sha256", secret).update(signed).digest("hex");
  const expectedBuf = Buffer.from(expected, "hex");

  let candidateBuf: Buffer;
  try {
    candidateBuf = Buffer.from(sigHex, "hex");
  } catch {
    return { valid: false, reason: "malformed_header", timestamp: ts };
  }
  if (candidateBuf.length !== expectedBuf.length) {
    return { valid: false, reason: "signature_mismatch", timestamp: ts };
  }
  if (timingSafeEqual(candidateBuf, expectedBuf)) {
    return { valid: true, timestamp: ts };
  }

  return { valid: false, reason: "signature_mismatch", timestamp: ts };
}
