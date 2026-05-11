namespace EPostak;

/// <summary>
/// Rate-limit information extracted from the most recent API response.
/// Populated from the <c>X-RateLimit-Limit</c>, <c>X-RateLimit-Remaining</c>,
/// and <c>X-RateLimit-Reset</c> response headers.
/// </summary>
/// <remarks>
/// Not every API response carries these headers — <see cref="EPostakClient.LastRateLimit"/>
/// returns the last observed value, or <c>null</c> if no rate-limit headers have
/// been seen yet in this client's lifetime.
/// </remarks>
public sealed class RateLimitInfo
{
    /// <summary>Maximum requests allowed in the current window (from <c>X-RateLimit-Limit</c>).</summary>
    public int Limit { get; init; }

    /// <summary>Requests remaining in the current window (from <c>X-RateLimit-Remaining</c>).</summary>
    public int Remaining { get; init; }

    /// <summary>
    /// UTC timestamp when the current rate-limit window resets
    /// (from <c>X-RateLimit-Reset</c>, a Unix epoch in seconds).
    /// </summary>
    public DateTimeOffset ResetAt { get; init; }
}
