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
    /// <summary>No <c>v1=</c> signature was present in the header.</summary>
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
/// Header format: <c>t=&lt;unix_seconds&gt;,v1=&lt;hex_signature&gt;</c>. Multiple
/// <c>v1=</c> signatures may appear (during secret rotation); any of them passing
/// is sufficient.
/// </para>
/// <para>
/// The signed string is <c>${t}.${rawBody}</c>, hex-encoded HMAC-SHA256, computed
/// on the bytes exactly as received off the wire — do NOT re-serialize parsed
/// JSON, the round-trip will reorder keys and mutate whitespace.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var result = EPostak.WebhookSignature.Verify(
///     payload: requestBodyBytes,
///     signatureHeader: request.Headers["X-Epostak-Signature"],
///     secret: Environment.GetEnvironmentVariable("EPOSTAK_WEBHOOK_SECRET")!);
/// if (!result.Valid)
///     return Results.BadRequest($"bad signature: {result.Reason}");
/// </code>
/// </example>
public static class WebhookSignature
{
    /// <summary>Default timestamp tolerance, in seconds (5 minutes), matching the server-side replay window.</summary>
    public const int DefaultToleranceSeconds = 300;

    /// <summary>Verify an ePošťák webhook signature against a raw byte payload.</summary>
    /// <param name="payload">Raw request body, exactly as bytes were received off the wire.</param>
    /// <param name="signatureHeader">Value of the <c>X-Epostak-Signature</c> header.</param>
    /// <param name="secret">The webhook signing secret captured at creation time.</param>
    /// <param name="toleranceSeconds">
    /// Maximum age of the signature in seconds. Defaults to <see cref="DefaultToleranceSeconds"/>
    /// (5 minutes). Set <c>0</c> to disable the timestamp check (not recommended in production).
    /// </param>
    public static WebhookSignatureResult Verify(
        byte[] payload,
        string signatureHeader,
        string secret,
        int toleranceSeconds = DefaultToleranceSeconds)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.MissingHeader };
        }

        string? timestampStr = null;
        var v1Signatures = new List<string>();
        foreach (var rawPart in signatureHeader.Split(','))
        {
            var part = rawPart.Trim();
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var k = part[..eq];
            var v = part[(eq + 1)..];
            if (k == "t") timestampStr = v;
            else if (k == "v1") v1Signatures.Add(v);
        }

        if (timestampStr is null)
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.MalformedHeader };

        if (!long.TryParse(timestampStr, out var ts))
            return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.MalformedHeader };

        if (v1Signatures.Count == 0)
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

        // Signed string: "<t>." + payload bytes.
        var prefix = Encoding.UTF8.GetBytes($"{timestampStr}.");
        var signed = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, signed, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, signed, prefix.Length, payload.Length);

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var expectedBytes = HMACSHA256.HashData(keyBytes, signed);

        foreach (var candidate in v1Signatures)
        {
            byte[] candidateBytes;
            try
            {
                candidateBytes = Convert.FromHexString(candidate);
            }
            catch (FormatException)
            {
                continue;
            }
            if (candidateBytes.Length != expectedBytes.Length) continue;
            if (CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes))
                return new WebhookSignatureResult { Valid = true, Reason = WebhookSignatureFailureReason.None, Timestamp = ts };
        }

        return new WebhookSignatureResult { Valid = false, Reason = WebhookSignatureFailureReason.SignatureMismatch, Timestamp = ts };
    }

    /// <summary>Verify an ePošťák webhook signature against a UTF-8 string payload.</summary>
    /// <param name="payload">Raw request body as a UTF-8 string.</param>
    /// <param name="signatureHeader">Value of the <c>X-Epostak-Signature</c> header.</param>
    /// <param name="secret">The webhook signing secret captured at creation time.</param>
    /// <param name="toleranceSeconds">Maximum age of the signature in seconds (default 300).</param>
    public static WebhookSignatureResult Verify(
        string payload,
        string signatureHeader,
        string secret,
        int toleranceSeconds = DefaultToleranceSeconds)
        => Verify(Encoding.UTF8.GetBytes(payload ?? ""), signatureHeader, secret, toleranceSeconds);
}
