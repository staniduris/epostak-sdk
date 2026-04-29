import { createHmac, timingSafeEqual } from "node:crypto";

/** Options for {@link verifyWebhookSignature}. */
export interface VerifyWebhookSignatureOptions {
  /** Raw request body, exactly as bytes were received off the wire. */
  payload: Buffer | string;
  /** Value of the `X-Epostak-Signature` (or equivalent) header. */
  signatureHeader: string;
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
    | "no_v1_signature"
    | "signature_mismatch"
    | "timestamp_outside_tolerance";
  /** Parsed timestamp from the header, in seconds since the epoch. */
  timestamp?: number;
}

/**
 * Verify an ePošťák webhook payload using HMAC-SHA256 with timing-safe compare.
 *
 * Header format: `t=<unix_seconds>,v1=<hex_signature>`. Multiple `v1=`
 * signatures may appear (during secret rotation); any of them passing is
 * sufficient.
 *
 * The signed string is `${t}.${rawBody}`, hex-encoded HMAC-SHA256, computed
 * on the bytes exactly as received off the wire — do NOT re-serialize the
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
 *       signatureHeader: req.header("x-epostak-signature") ?? "",
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
  const { payload, signatureHeader, secret } = options;
  const tolerance = options.toleranceSeconds ?? 300;

  if (!signatureHeader || signatureHeader.length === 0) {
    return { valid: false, reason: "missing_header" };
  }

  const parts = signatureHeader.split(",").map((s) => s.trim());
  let timestamp: string | undefined;
  const v1Signatures: string[] = [];
  for (const p of parts) {
    const eq = p.indexOf("=");
    if (eq < 0) continue;
    const k = p.slice(0, eq);
    const v = p.slice(eq + 1);
    if (k === "t") timestamp = v;
    else if (k === "v1") v1Signatures.push(v);
  }

  if (!timestamp) return { valid: false, reason: "malformed_header" };
  if (v1Signatures.length === 0)
    return { valid: false, reason: "no_v1_signature" };

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

  for (const candidate of v1Signatures) {
    let candidateBuf: Buffer;
    try {
      candidateBuf = Buffer.from(candidate, "hex");
    } catch {
      continue;
    }
    if (candidateBuf.length !== expectedBuf.length) continue;
    if (timingSafeEqual(candidateBuf, expectedBuf)) {
      return { valid: true, timestamp: ts };
    }
  }

  return { valid: false, reason: "signature_mismatch", timestamp: ts };
}
