package sk.epostak.sdk;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.security.InvalidKeyException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;

/**
 * Utility class for verifying ePostak webhook payload signatures.
 *
 * <p>Webhooks are signed with HMAC-SHA512. The signature is provided in the
 * {@code X-Epostak-Signature} header in the format:
 * {@code t=<unix_ms>,v1=<hex_hmac_sha512>}.
 *
 * <p>Uses constant-time comparison ({@link MessageDigest#isEqual}) to prevent
 * timing attacks. Rejects payloads older than the specified max age to prevent
 * replay attacks.
 *
 * @example
 * <pre>{@code
 * boolean valid = WebhookSignature.verify(
 *     requestBody,
 *     request.getHeader("X-Epostak-Signature"),
 *     System.getenv("WEBHOOK_SECRET"));
 * if (!valid) {
 *     response.setStatus(401);
 *     return;
 * }
 * }</pre>
 */
public final class WebhookSignature {

    /** Default maximum age in milliseconds (5 minutes). */
    private static final long DEFAULT_MAX_AGE_MS = 300_000L;

    private WebhookSignature() {}

    /**
     * Verify a webhook signature with the default 5-minute replay window.
     *
     * @param payload   the raw request body as a string
     * @param signature the value of the {@code X-Epostak-Signature} header
     * @param secret    the webhook signing secret from webhook creation
     * @return {@code true} if the signature is valid and within the replay window
     */
    public static boolean verify(String payload, String signature, String secret) {
        return verify(payload, signature, secret, DEFAULT_MAX_AGE_MS);
    }

    /**
     * Verify a webhook signature with a custom replay window.
     *
     * @param payload    the raw request body as a string
     * @param signature  the value of the {@code X-Epostak-Signature} header
     * @param secret     the webhook signing secret from webhook creation
     * @param maxAgeMs   maximum acceptable age of the payload in milliseconds
     * @return {@code true} if the signature is valid and within the replay window
     */
    public static boolean verify(String payload, String signature, String secret, long maxAgeMs) {
        try {
            // Signature format: t=<unix_ms>,v1=<hex_hmac_sha512>
            String tPart = null;
            String vPart = null;
            for (String segment : signature.split(",")) {
                if (segment.startsWith("t=")) tPart = segment.substring(2);
                else if (segment.startsWith("v1=")) vPart = segment.substring(3);
            }
            if (tPart == null || vPart == null) return false;

            long timestamp;
            try {
                timestamp = Long.parseLong(tPart);
            } catch (NumberFormatException e) {
                return false;
            }

            // Replay protection
            long age = System.currentTimeMillis() - timestamp;
            if (age < 0 || age > maxAgeMs) return false;

            // Compute HMAC-SHA512
            String message = timestamp + "." + payload;
            Mac mac = Mac.getInstance("HmacSHA512");
            mac.init(new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), "HmacSHA512"));
            byte[] computed = mac.doFinal(message.getBytes(StandardCharsets.UTF_8));

            byte[] expected = hexToBytes(vPart);
            if (expected == null) return false;

            // Constant-time comparison
            return MessageDigest.isEqual(computed, expected);
        } catch (NoSuchAlgorithmException | InvalidKeyException e) {
            return false;
        }
    }

    /** Decode a lowercase hex string to bytes, or return {@code null} on error. */
    private static byte[] hexToBytes(String hex) {
        if (hex.length() % 2 != 0) return null;
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
