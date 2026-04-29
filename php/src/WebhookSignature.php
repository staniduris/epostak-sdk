<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Verify ePošťák webhook payloads using HMAC-SHA256 with timing-safe compare.
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
 *   $raw = file_get_contents('php://input');
 *   $result = \EPostak\WebhookSignature::verify(
 *       $raw,
 *       $_SERVER['HTTP_X_EPOSTAK_SIGNATURE'] ?? '',
 *       getenv('EPOSTAK_WEBHOOK_SECRET'),
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
     * @param string|resource $payload          Raw request body bytes — exactly
     *                                          as received off the wire. May be
     *                                          a string or a stream resource (the
     *                                          resource will be drained with
     *                                          `stream_get_contents`).
     * @param string          $signatureHeader  Value of the `X-Epostak-Signature`
     *                                          header.
     * @param string          $secret           Webhook signing secret.
     * @param int             $toleranceSeconds Maximum age of the signature in
     *                                          seconds (default 300). Set 0 to
     *                                          disable the timestamp check (not
     *                                          recommended).
     * @return array{valid: bool, reason: ?string, timestamp: ?int} Verification
     *         result. `reason` is one of `missing_header`, `malformed_header`,
     *         `no_v1_signature`, `signature_mismatch`,
     *         `timestamp_outside_tolerance`, or null when valid.
     */
    public static function verify(
        $payload,
        string $signatureHeader,
        string $secret,
        int $toleranceSeconds = 300,
    ): array {
        if ($signatureHeader === '') {
            return ['valid' => false, 'reason' => 'missing_header', 'timestamp' => null];
        }

        $timestamp = null;
        $v1Signatures = [];
        foreach (explode(',', $signatureHeader) as $part) {
            $part = trim($part);
            $eq = strpos($part, '=');
            if ($eq === false) {
                continue;
            }
            $k = substr($part, 0, $eq);
            $v = substr($part, $eq + 1);
            if ($k === 't') {
                $timestamp = $v;
            } elseif ($k === 'v1') {
                $v1Signatures[] = $v;
            }
        }

        if ($timestamp === null) {
            return ['valid' => false, 'reason' => 'malformed_header', 'timestamp' => null];
        }
        if (count($v1Signatures) === 0) {
            return ['valid' => false, 'reason' => 'no_v1_signature', 'timestamp' => null];
        }
        if (!is_numeric($timestamp)) {
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

        $payloadBytes = is_resource($payload)
            ? (string) stream_get_contents($payload)
            : (string) $payload;

        $signed = $timestamp . '.' . $payloadBytes;
        $expected = hash_hmac('sha256', $signed, $secret);

        foreach ($v1Signatures as $candidate) {
            if (hash_equals($expected, strtolower($candidate))) {
                return ['valid' => true, 'reason' => null, 'timestamp' => $ts];
            }
        }

        return ['valid' => false, 'reason' => 'signature_mismatch', 'timestamp' => $ts];
    }
}
