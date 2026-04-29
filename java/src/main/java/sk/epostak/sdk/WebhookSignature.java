package sk.epostak.sdk;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.security.InvalidKeyException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.List;

/**
 * Verify ePošťák webhook payload signatures with HMAC-SHA256 and timing-safe
 * compare.
 *
 * <p>Header format: {@code t=<unix_seconds>,v1=<hex_signature>}. Multiple
 * {@code v1=} signatures may appear during secret rotation; any of them
 * passing is sufficient.
 *
 * <p>The signed string is {@code "${t}.${rawBody}"}, hex-encoded HMAC-SHA256,
 * computed on the bytes exactly as received off the wire — do NOT re-serialize
 * parsed JSON (the round-trip will reorder keys and mutate whitespace).
 *
 * <p>Default timestamp tolerance is 300 seconds (5 minutes) which matches the
 * server-side replay window. Set {@code 0} to disable the timestamp check (not
 * recommended in production).
 *
 * <pre>{@code
 * VerifyResult result = WebhookSignature.verify(
 *     rawBody,
 *     request.getHeader("X-Epostak-Signature"),
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
        /** {@code Signature} header was missing or empty. */
        MISSING_HEADER,
        /** Header was present but did not parse as {@code t=...,v1=...}. */
        MALFORMED_HEADER,
        /** Header had a {@code t=} component but no {@code v1=} component. */
        NO_V1_SIGNATURE,
        /** None of the {@code v1=} candidates matched the expected HMAC. */
        SIGNATURE_MISMATCH,
        /** Timestamp was outside the configured tolerance window. */
        TIMESTAMP_OUTSIDE_TOLERANCE
    }

    /**
     * Result of {@link WebhookSignature#verify(String, String, String)}.
     *
     * @param valid     whether the signature is valid AND the timestamp is within tolerance
     * @param reason    rejection reason — only set when {@code valid == false}, otherwise {@code null}
     * @param timestamp parsed timestamp from the header, in unix seconds — {@code null} if the header
     *                  did not even contain a parseable {@code t=}
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
     * @param payload         the raw request body as a string
     * @param signatureHeader the value of the {@code X-Epostak-Signature} header
     * @param secret          the webhook signing secret captured at webhook-creation time
     * @return the verification result with {@code valid}, {@code reason}, and parsed {@code timestamp}
     */
    public static VerifyResult verify(String payload, String signatureHeader, String secret) {
        return verify(payload != null ? payload.getBytes(StandardCharsets.UTF_8) : null,
                signatureHeader, secret, DEFAULT_TOLERANCE_SECONDS);
    }

    /**
     * Verify a webhook signature using a custom timestamp tolerance.
     *
     * @param payload            the raw request body as a string
     * @param signatureHeader    the value of the {@code X-Epostak-Signature} header
     * @param secret             the webhook signing secret captured at webhook-creation time
     * @param toleranceSeconds   maximum age of the signature in seconds; {@code 0} disables the check
     * @return the verification result with {@code valid}, {@code reason}, and parsed {@code timestamp}
     */
    public static VerifyResult verify(String payload, String signatureHeader, String secret, long toleranceSeconds) {
        return verify(payload != null ? payload.getBytes(StandardCharsets.UTF_8) : null,
                signatureHeader, secret, toleranceSeconds);
    }

    /**
     * Verify a webhook signature on raw bytes. Prefer this overload when the
     * server framework hands you the raw request body — re-encoding parsed
     * JSON will mutate whitespace and break the HMAC.
     *
     * @param payload            raw request body bytes
     * @param signatureHeader    the value of the {@code X-Epostak-Signature} header
     * @param secret             the webhook signing secret captured at webhook-creation time
     * @param toleranceSeconds   maximum age of the signature in seconds; {@code 0} disables the check
     * @return the verification result with {@code valid}, {@code reason}, and parsed {@code timestamp}
     */
    public static VerifyResult verify(byte[] payload, String signatureHeader, String secret, long toleranceSeconds) {
        if (signatureHeader == null || signatureHeader.isEmpty()) {
            return VerifyResult.fail(Reason.MISSING_HEADER);
        }

        String timestampStr = null;
        List<String> v1Signatures = new ArrayList<>();
        for (String segment : signatureHeader.split(",")) {
            String s = segment.trim();
            int eq = s.indexOf('=');
            if (eq < 0) continue;
            String k = s.substring(0, eq);
            String v = s.substring(eq + 1);
            if ("t".equals(k)) {
                timestampStr = v;
            } else if ("v1".equals(k)) {
                v1Signatures.add(v);
            }
        }

        if (timestampStr == null) {
            return VerifyResult.fail(Reason.MALFORMED_HEADER);
        }
        if (v1Signatures.isEmpty()) {
            return VerifyResult.fail(Reason.NO_V1_SIGNATURE);
        }

        long timestamp;
        try {
            timestamp = Long.parseLong(timestampStr);
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
        byte[] prefix = (timestampStr + ".").getBytes(StandardCharsets.UTF_8);
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

        for (String candidate : v1Signatures) {
            byte[] candidateBytes = hexToBytes(candidate);
            if (candidateBytes == null) continue;
            if (candidateBytes.length != expected.length) continue;
            if (MessageDigest.isEqual(candidateBytes, expected)) {
                return VerifyResult.ok(timestamp);
            }
        }

        return VerifyResult.fail(Reason.SIGNATURE_MISMATCH, timestamp);
    }

    /** Decode a lowercase hex string to bytes, or return {@code null} on error. */
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
