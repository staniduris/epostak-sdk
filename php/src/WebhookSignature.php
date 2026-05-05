<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Verify ePošťák webhook payloads using HMAC-SHA256 with timing-safe compare.
 *
 * The server sends two separate headers:
 *   - `X-Webhook-Signature: sha256=<hex>`
 *   - `X-Webhook-Timestamp: <unix_seconds>`
 *
 * The signed string is `${timestamp}.${rawBody}` — hex-encoded HMAC-SHA256
 * computed on the bytes exactly as received off the wire. Do NOT re-serialize
 * the parsed JSON; the round-trip will reorder keys and mutate whitespace.
 *
 * @example
 *   $raw = file_get_contents('php://input');
 *   $result = \EPostak\WebhookSignature::verify(
 *       signature:  $_SERVER['HTTP_X_WEBHOOK_SIGNATURE'] ?? '',
 *       timestamp:  $_SERVER['HTTP_X_WEBHOOK_TIMESTAMP'] ?? '',
 *       body:       $raw,
 *       secret:     getenv('EPOSTAK_WEBHOOK_SECRET'),
 *   );
 *   if (!$result['valid']) {
 *       http_response_code(400);
 *       echo "bad signature: ", $result['reason'];
 *       exit;
 *   }
 *   $event = json_decode($raw, true);
 */
final class WebhookSignature
{
    /**
     * Verify a webhook signature.
     *
     * @param string $signature  Value of the `X-Webhook-Signature` header.
     *                           Must be in the form `sha256=<hex>`. Any
     *                           other algorithm prefix is rejected.
     * @param string $timestamp  Value of the `X-Webhook-Timestamp` header.
     *                           Unix seconds as a decimal string.
     * @param string $body       Raw request body bytes — exactly as received
     *                           off the wire. Do NOT pass the decoded JSON.
     * @param string $secret     Webhook signing secret.
     * @param int    $toleranceSeconds Maximum age of the signature in seconds
     *                           (default 300). Set 0 to disable the timestamp
     *                           check (not recommended for production).
     * @return array{valid: bool, reason: ?string, timestamp: ?int}
     *         `reason` is one of `missing_header`, `malformed_header`,
     *         `unknown_algorithm`, `signature_mismatch`,
     *         `timestamp_outside_tolerance`, or null when valid.
     */
    public static function verify(
        string $signature,
        string $timestamp,
        string $body,
        string $secret,
        int $toleranceSeconds = 300,
    ): array {
        if ($signature === '') {
            return ['valid' => false, 'reason' => 'missing_header', 'timestamp' => null];
        }
        if ($timestamp === '') {
            return ['valid' => false, 'reason' => 'missing_header', 'timestamp' => null];
        }

        // Parse "sha256=<hex>" — reject unknown algorithms strictly
        if (!str_starts_with($signature, 'sha256=')) {
            return ['valid' => false, 'reason' => 'unknown_algorithm', 'timestamp' => null];
        }
        $hex = substr($signature, 7);
        if ($hex === '' || !ctype_xdigit($hex)) {
            return ['valid' => false, 'reason' => 'malformed_header', 'timestamp' => null];
        }

        if (!is_numeric($timestamp) || str_contains($timestamp, '.')) {
            return ['valid' => false, 'reason' => 'malformed_header', 'timestamp' => null];
        }

        $ts = (int) $timestamp;

        if ($toleranceSeconds > 0) {
            $now = time();
            if (abs($now - $ts) > $toleranceSeconds) {
                return [
                    'valid' => false,
                    'reason' => 'timestamp_outside_tolerance',
                    'timestamp' => $ts,
                ];
            }
        }

        $expected = hash_hmac('sha256', $timestamp . '.' . $body, $secret);

        if (hash_equals($expected, strtolower($hex))) {
            return ['valid' => true, 'reason' => null, 'timestamp' => $ts];
        }

        return ['valid' => false, 'reason' => 'signature_mismatch', 'timestamp' => $ts];
    }
}
