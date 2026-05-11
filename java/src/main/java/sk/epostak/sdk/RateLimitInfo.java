package sk.epostak.sdk;

import java.time.Instant;

/**
 * Rate-limit information captured from the most recent API response headers.
 * <p>
 * Every successful (non-429) API response includes three headers:
 * <ul>
 *   <li>{@code X-RateLimit-Limit} — requests allowed per window</li>
 *   <li>{@code X-RateLimit-Remaining} — requests remaining in the current window</li>
 *   <li>{@code X-RateLimit-Reset} — Unix epoch seconds when the window resets</li>
 * </ul>
 * The SDK captures these values and makes them available via
 * {@link EPostak#getLastRateLimit()}.
 *
 * <pre>{@code
 * client.documents().list();
 * RateLimitInfo rl = client.getLastRateLimit();
 * if (rl != null) {
 *     System.out.println("Remaining: " + rl.getRemaining() + "/" + rl.getLimit());
 *     System.out.println("Resets at: " + rl.getResetAt());
 * }
 * }</pre>
 *
 * @param limit     maximum requests allowed in the rate-limit window
 * @param remaining requests remaining in the current window
 * @param resetAt   the instant when the window resets and {@code remaining} refills
 */
public record RateLimitInfo(int limit, int remaining, Instant resetAt) {

    /**
     * Returns the maximum number of requests allowed per rate-limit window.
     *
     * @return rate limit cap
     */
    public int getLimit() { return limit; }

    /**
     * Returns the number of requests remaining in the current window.
     *
     * @return remaining request budget
     */
    public int getRemaining() { return remaining; }

    /**
     * Returns the instant when the rate-limit window resets and the remaining
     * count refills to {@link #getLimit()}.
     *
     * @return reset instant
     */
    public Instant getResetAt() { return resetAt; }
}
