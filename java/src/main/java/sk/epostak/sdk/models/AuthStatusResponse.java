package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from {@code GET /auth/status} describing the authenticated API key,
 * the firm it resolves to, the current subscription plan, the applicable rate
 * limit, and (for integrator keys) the parent integrator.
 *
 * @param key        information about the authenticated API key
 * @param firm       minimal identification of the firm the key resolves to
 * @param plan       the firm's current plan, expiry, and active flag
 * @param rateLimit  rate-limit configuration enforced for this key
 * @param integrator parent integrator when the key is an integrator subkey, otherwise {@code null}
 */
public record AuthStatusResponse(
        KeyInfo key,
        FirmInfo firm,
        PlanInfo plan,
        RateLimit rateLimit,
        IntegratorInfo integrator
) {
    /**
     * Information about the authenticated API key.
     *
     * @param id          the key UUID
     * @param name        the human-assigned name of the key, or {@code null}
     * @param prefix      the key prefix shown in the dashboard, e.g. {@code "sk_live_abc"}
     * @param permissions list of permission scopes granted to the key
     * @param active      {@code true} if the key is currently active
     * @param createdAt   ISO 8601 creation timestamp
     * @param lastUsedAt  ISO 8601 timestamp of last successful authentication, or {@code null}
     */
    public record KeyInfo(
            String id,
            String name,
            String prefix,
            List<String> permissions,
            boolean active,
            String createdAt,
            String lastUsedAt
    ) {}

    /**
     * Minimal firm identification returned by {@code /auth/status}.
     *
     * @param id           the firm UUID
     * @param peppolStatus Peppol registration status, e.g. {@code "ACTIVE"}
     */
    public record FirmInfo(
            String id,
            String peppolStatus
    ) {}

    /**
     * Current plan for the firm.
     *
     * @param name      plan identifier, e.g. {@code "free"}, {@code "api-enterprise"}
     * @param expiresAt ISO 8601 expiry timestamp, or {@code null} if the plan does not expire
     * @param active    {@code true} when the plan is currently valid (not expired and not free)
     */
    public record PlanInfo(
            String name,
            String expiresAt,
            boolean active
    ) {}

    /**
     * Rate-limit configuration for the authenticated key.
     *
     * @param perMinute max requests per {@code window}
     * @param window    human-readable window, e.g. {@code "60s"}
     */
    public record RateLimit(
            int perMinute,
            String window
    ) {}

    /**
     * Parent integrator information, present only when the key is an integrator subkey.
     *
     * @param id the integrator UUID
     */
    public record IntegratorInfo(
            String id
    ) {}
}
