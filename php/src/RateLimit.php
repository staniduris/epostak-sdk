<?php

declare(strict_types=1);

namespace EPostak;

/**
 * Rate-limit snapshot captured from the most recent API response headers.
 *
 * Obtained via `$client->getLastRateLimit()`. Returns `null` if no request
 * has been made yet or the last response did not include rate-limit headers.
 *
 * @example
 *   $client->documents->list();
 *   $rl = $client->getLastRateLimit();
 *   if ($rl !== null && $rl->remaining < 10) {
 *       echo 'Low rate-limit headroom, backing off…';
 *   }
 */
final class RateLimit
{
    /**
     * @param int|null                   $limit     Max requests allowed in the current window (X-RateLimit-Limit).
     * @param int|null                   $remaining Requests remaining in the current window (X-RateLimit-Remaining).
     * @param \DateTimeImmutable|null    $resetAt   Time at which the window resets (X-RateLimit-Reset).
     */
    public function __construct(
        public readonly ?int $limit,
        public readonly ?int $remaining,
        public readonly ?\DateTimeImmutable $resetAt,
    ) {
    }
}
