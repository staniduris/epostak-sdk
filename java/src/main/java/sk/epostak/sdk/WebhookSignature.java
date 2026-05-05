package sk.epostak.sdk;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.security.InvalidKeyException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;

/**
 * Verify ePošťák webhook payload signatures with HMAC-SHA256 and timing-safe
 * compare.
 *
 * <p>The server signs with HMAC-SHA256 over {@code "${timestamp}.${rawBody}"}
 * and ships two headers:
 * <ul>
 *   <li>{@code X-Webhook-Signature: sha256=<hex>}</li>
 *   <li>{@code X-Webhook-Timestamp: <unix_seconds>}</li>
 * </ul>
 *
 * <p>Use the raw body bytes exactly as received off the wire — do NOT
 * re-serialize parsed JSON (the round-trip will reorder keys and mutate
 * whitespace, breaking the HMAC).
 *
 * <p>Default timestamp tolerance is 300 seconds (5 minutes) which matches the
 * server-side replay window. Set {@code 0} to disable the timestamp check (not
 * recommended in production).
 *
 * <pre>{@code
 * VerifyResult result = WebhookSignature.verify(
 *     rawBody,
 *     request.getHeader("X-Webhook-Signature"),
 *     request.getHeader("X-Webhook-Timestamp"),
 *     System.getenv("EPOSTAK_WEBHOOK_SECRET"));
 * if (!result.valid()) {
 *     response.setStatus(400);
 *     response.getWriter().write("bad signature: " + result.reason());
 *     return;
 * }
 * }</pre>
 */
public final class WebhookSignature {

    /** Default maximum age in seconds (5 minutes). */
    private static final long DEFAULT_TOLERANCE_SECONDS = 300L;

    private WebhookSignature() {}

    /** Reasons the signature was rejected — only meaningful when {@link VerifyResult#valid()} is {@code false}. */
    public enum Reason {
        /** {@code X-Webhook-Signature} or {@code X-Webhook-Timestamp} header was missing or empty. */
        MISSING_HEADER,
        /** Header was present but did not parse as {@code sha256=<hex>} or timestamp was not numeric. */
        MALFORMED_HEADER,
        /** Algorithm prefix was present but not {@code sha256}. */
        UNSUPPORTED_ALGORITHM,
        /** The computed HMAC did not match the supplied signature. */
        SIGNATURE_MISMATCH,
        /** Timestamp was outside the configured tolerance window. */
        TIMESTAMP_OUTSIDE_TOLERANCE
    }

    /**
     * Result of {@link WebhookSignature#verify}.
     *
     * @param valid     whether the signature is valid AND the timestamp is within tolerance
     * @param reason    rejection reason — only set when {@code valid == false}, otherwise {@code null}
     * @param timestamp parsed timestamp from the header, in unix seconds — {@code null} if the header
     *                  did not even contain a parseable timestamp
     */
    public record VerifyResult(boolean valid, Reason reason, Long timestamp) {
        /** Convenience: a successful result for {@code timestamp}. */
        public static VerifyResult ok(long timestamp) {
            return new VerifyResult(true, null, timestamp);
        }

        /** Convenience: a failure result with no parsed timestamp. */
        public static VerifyResult fail(Reason reason) {
            return new VerifyResult(false, reason, null);
        }

        /** Convenience: a failure result that did parse a timestamp. */
        public static VerifyResult fail(Reason reason, long timestamp) {
            return new VerifyResult(false, reason, timestamp);
        }
    }

    /**
     * Verify a webhook signature using the default 5-minute replay window.
     *
     * @param payload            the raw request body as a string
     * @param signatureHeader    value of the {@code X-Webhook-Signature} header ({@code sha256=<hex>})
     * @param timestampHeader    value of the {@code X-Webhook-Timestamp} header (unix seconds)
     * @param secret             the webhook signing secret captured at webhook-creation time
     * @return the verification result with {@code valid}, {@code reason}, and parsed {@code timestamp}
     */
    public static VerifyResult verify(String payload, String signatureHeader, String timestampHeader, String secret) {
        return verify(payload != null ? payload.getBytes(StandardCharsets.UTF_8) : null,
                signatureHeader, timestampHeader, secret, DEFAULT_TOLERANCE_SECONDS);
    }

    /**
     * Verify a webhook signature using a custom timestamp tolerance.
     *
     * @param payload            the raw request body as a string
     * @param signatureHeader    value of the {@code X-Webhook-Signature} header ({@code sha256=<hex>})
     * @param timestampHeader    value of the {@code X-Webhook-Timestamp} header (unix seconds)
     * @param secret             the webhook signing secret captured at webhook-creation time
     * @param toleranceSeconds   maximum age of the signature in seconds; {@code 0} disables the check
     * @return the verification result with {@code valid}, {@code reason}, and parsed {@code timestamp}
     */
    public static VerifyResult verify(String payload, String signatureHeader, String timestampHeader, String secret, long toleranceSeconds) {
        return verify(payload != null ? payload.getBytes(StandardCharsets.UTF_8) : null,
                signatureHeader, timestampHeader, secret, toleranceSeconds);
    }

    /**
     * Verify a webhook signature on raw bytes. Prefer this overload when the
     * server framework hands you the raw request body — re-encoding parsed
     * JSON will mutate whitespace and break the HMAC.
     *
     * @param payload            raw request body bytes
     * @param signatureHeader    value of the {@code X-Webhook-Signature} header ({@code sha256=<hex>})
     * @param timestampHeader    value of the {@code X-Webhook-Timestamp} header (unix seconds)
     * @param secret             the webhook signing secret captured at webhook-creation time
     * @param toleranceSeconds   maximum age of the signature in seconds; {@code 0} disables the check
     * @return the verification result with {@code valid}, {@code reason}, and parsed {@code timestamp}
     */
    public static VerifyResult verify(byte[] payload, String signatureHeader, String timestampHeader, String secret, long toleranceSeconds) {
        if (signatureHeader == null || signatureHeader.isEmpty()
                || timestampHeader == null || timestampHeader.isEmpty()) {
            return VerifyResult.fail(Reason.MISSING_HEADER);
        }

        // Parse "sha256=<hex>" format
        int eqIdx = signatureHeader.indexOf('=');
        if (eqIdx < 0) {
            return VerifyResult.fail(Reason.MALFORMED_HEADER);
        }
        String algo = signatureHeader.substring(0, eqIdx).trim().toLowerCase();
        String sigHex = signatureHeader.substring(eqIdx + 1).trim();

        if (!"sha256".equals(algo)) {
            return VerifyResult.fail(Reason.UNSUPPORTED_ALGORITHM);
        }
        if (sigHex.isEmpty() || !sigHex.matches("[0-9a-fA-F]+")) {
            return VerifyResult.fail(Reason.MALFORMED_HEADER);
        }

        long timestamp;
        try {
            timestamp = Long.parseLong(timestampHeader.trim());
        } catch (NumberFormatException e) {
            return VerifyResult.fail(Reason.MALFORMED_HEADER);
        }

        if (toleranceSeconds > 0) {
            long nowSec = System.currentTimeMillis() / 1000L;
            if (Math.abs(nowSec - timestamp) > toleranceSeconds) {
                return VerifyResult.fail(Reason.TIMESTAMP_OUTSIDE_TOLERANCE, timestamp);
            }
        }

        byte[] payloadBytes = payload != null ? payload : new byte[0];
        byte[] prefix = (timestampHeader.trim() + ".").getBytes(StandardCharsets.UTF_8);
        byte[] signed = new byte[prefix.length + payloadBytes.length];
        System.arraycopy(prefix, 0, signed, 0, prefix.length);
        System.arraycopy(payloadBytes, 0, signed, prefix.length, payloadBytes.length);

        byte[] expected;
        try {
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), "HmacSHA256"));
            expected = mac.doFinal(signed);
        } catch (NoSuchAlgorithmException | InvalidKeyException e) {
            return VerifyResult.fail(Reason.SIGNATURE_MISMATCH, timestamp);
        }

        byte[] candidateBytes = hexToBytes(sigHex);
        if (candidateBytes == null || candidateBytes.length != expected.length) {
            return VerifyResult.fail(Reason.SIGNATURE_MISMATCH, timestamp);
        }
        if (MessageDigest.isEqual(candidateBytes, expected)) {
            return VerifyResult.ok(timestamp);
        }

        return VerifyResult.fail(Reason.SIGNATURE_MISMATCH, timestamp);
    }

    /** Decode a hex string to bytes, or return {@code null} on error. */
    private static byte[] hexToBytes(String hex) {
        if (hex == null || hex.length() % 2 != 0) return null;
        byte[] bytes = new byte[hex.length() / 2];
        for (int i = 0; i < bytes.length; i++) {
            int high = Character.digit(hex.charAt(i * 2), 16);
            int low = Character.digit(hex.charAt(i * 2 + 1), 16);
            if (high < 0 || low < 0) return null;
            bytes[i] = (byte) ((high << 4) | low);
        }
        return bytes;
    }
}
