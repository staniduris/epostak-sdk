using System.Security.Cryptography;
using System.Text;

namespace EPostak;

/// <summary>
/// Reason an ePošťák webhook signature was rejected. <c>None</c> means the
/// signature was valid.
/// </summary>
public enum WebhookSignatureFailureReason
{
    /// <summary>Signature verified successfully.</summary>
    None,
    /// <summary>The signature header was missing or empty.</summary>
    MissingHeader,
    /// <summary>The signature header could not be parsed (no <c>t=</c> or non-numeric timestamp).</summary>
    MalformedHeader,
    /// <summary>The signature header did not contain a valid <c>sha256=</c> prefix or was empty after the prefix.</summary>
    NoV1Signature,
    /// <summary>The computed HMAC did not match any provided <c>v1=</c> value.</summary>
    SignatureMismatch,
    /// <summary>The header timestamp is outside the configured tolerance window.</summary>
    TimestampOutsideTolerance,
}

/// <summary>
/// Result of <see cref="WebhookSignature.Verify(byte[], string, string, int)"/>.
/// <para>
/// <see cref="Valid"/> is <c>true</c> only when the body/signature/secret line up
/// AND the timestamp is within tolerance. On failure, <see cref="Reason"/>
/// explains why so callers can log it; the helper never throws on bad signatures.
/// </para>
/// </summary>
public sealed class WebhookSignatureResult
{
    /// <summary>Whether the signature is valid AND the timestamp is within tolerance.</summary>
    public bool Valid { get; init; }

    /// <summary>Reason the signature was rejected — <see cref="WebhookSignatureFailureReason.None"/> when <see cref="Valid"/> is true.</summary>
    public WebhookSignatureFailureReason Reason { get; init; }

    /// <summary>Parsed timestamp from the header, in seconds since the epoch. Null when the header was unparseable.</summary>
    public long? Timestamp { get; init; }
}

/// <summary>
/// Verify ePošťák webhook payload signatures using HMAC-SHA256 with timing-safe
/// comparison.
/// <para>
/// The server sends two separate headers:
/// <list type="bullet">
///   <item><c>X-Webhook-Signature: sha256=&lt;hex&gt;</c></item>
///   <item><c>X-Webhook-Timestamp: &lt;unix_seconds&gt;</c></item>
/// </list>
/// The signed string is <c>${timestamp}.${rawBody}</c>, hex-encoded HMAC-SHA256,
/// computed on the bytes exactly as received off the wire — do NOT re-serialize
/// parsed JSON, the round-trip will reorder keys and mutate whitespace.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var result = EPostak.WebhookSignature.Verify(
///     payload: requestBodyBytes,
///     signature: request.Headers["X-Webhook-Signature"],
///     timestamp: request.Headers["X-Webhook-Timestamp"],
///     secret: Environment.GetEnvironmentVariable("EPOSTAK_WEBHOOK_SECRET")!);
/// if (!result.Valid)
///     return Results.BadRequest($"bad signature: {result.Reason}");
/// </code>
/// </example>
public static class WebhookSignature
{
    /// <summary>Default timestamp tolerance, in seconds (5 minutes), matching the server-side replay window.</summary>
    public const int DefaultToleranceSeconds = 300;

    /// <summary>
    /// Verify an ePošťák webhook signature against a raw byte payload.
    /// The server sends <c>X-Webhook-Signature: sha256=&lt;hex&gt;</c> and
    /// <c>X-Webhook-Timestamp: &lt;unix_seconds&gt;</c> as two separate headers.
    /// </summary>
    /// <param name="payload">Raw request body, exactly as bytes were received off the wire.</param>
    /// <param name="signature">Value of the <c>X-Webhook-Signature</c> header (e.g. <c>sha256=abc123</c>).</param>
    /// <param name="timestamp">Value of the <c>X-Webhook-Timestamp</c> header (Unix seconds as a string).</param>
    /// <param name="secret">The webhook signing secret captured at creation time.</param>
    /// <param name="toleranceSeconds">
    /// Maximum age of the signature in seconds. Defaults to <see cref="DefaultToleranceSeconds"/>
    /// (5 minutes). Set <c>0</c> to disable the timestamp check (not recommended in production).
    /// </param>
    public static WebhookSignatureResult Verify(
        byte[] payload,
        string signature,
        string timestamp,
        string secret,
        int toleranceSeconds = DefaultToleranceSeconds)
    {
        if (string.IsNullOrEmpty(signature))
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.MissingHeader };

        if (string.IsNullOrEmpty(timestamp))
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.MalformedHeader };

        if (!long.TryParse(timestamp, out var ts))
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.MalformedHeader };

        // Parse "sha256=<hex>" strictly.
        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.NoV1Signature, Timestamp = ts };

        var hexPart = signature[prefix.Length..];
        if (string.IsNullOrEmpty(hexPart))
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.NoV1Signature, Timestamp = ts };

        if (toleranceSeconds > 0)
        {
            var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(nowSec - ts) > toleranceSeconds)
            {
                return new WebhookSignatureResult
                {
                    Valid = false,
                    Reason = WebhookSignatureFailureReason.TimestampOutsideTolerance,
                    Timestamp = ts,
                };
            }
        }

        // Signed string: "<timestamp>." + payload bytes.
        var tsPrefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var signed = new byte[tsPrefix.Length + payload.Length];
        Buffer.BlockCopy(tsPrefix, 0, signed, 0, tsPrefix.Length);
        Buffer.BlockCopy(payload, 0, signed, tsPrefix.Length, payload.Length);

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var expectedBytes = HMACSHA256.HashData(keyBytes, signed);

        byte[] candidateBytes;
        try
        {
            candidateBytes = Convert.FromHexString(hexPart);
        }
        catch (FormatException)
        {
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.SignatureMismatch, Timestamp = ts };
        }

        if (candidateBytes.Length != expectedBytes.Length)
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.SignatureMismatch, Timestamp = ts };

        if (CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes))
            return new WebhookSignatureResult { Valid = true, Reason = WebhookSignatureFailureReason.None, Timestamp = ts };

        return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.SignatureMismatch, Timestamp = ts };
    }

    /// <summary>
    /// Verify an ePošťák webhook signature against a UTF-8 string payload.
    /// The server sends <c>X-Webhook-Signature: sha256=&lt;hex&gt;</c> and
    /// <c>X-Webhook-Timestamp: &lt;unix_seconds&gt;</c> as two separate headers.
    /// </summary>
    /// <param name="payload">Raw request body as a UTF-8 string.</param>
    /// <param name="signature">Value of the <c>X-Webhook-Signature</c> header (e.g. <c>sha256=abc123</c>).</param>
    /// <param name="timestamp">Value of the <c>X-Webhook-Timestamp</c> header (Unix seconds as a string).</param>
    /// <param name="secret">The webhook signing secret captured at creation time.</param>
    /// <param name="toleranceSeconds">Maximum age of the signature in seconds (default 300).</param>
    public static WebhookSignatureResult Verify(
        string payload,
        string signature,
        string timestamp,
        string secret,
        int toleranceSeconds = DefaultToleranceSeconds)
        => Verify(Encoding.UTF8.GetBytes(payload ?? ""), signature, timestamp, secret, toleranceSeconds);
}
