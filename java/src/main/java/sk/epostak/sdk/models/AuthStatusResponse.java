package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from {@code POST /auth/status} describing the authenticated key,
 * the firm it resolves to, the current subscription plan, applicable rate limit,
 * and optional integrator information.
 *
 * @param key        information about the authenticated API key
 * @param firm       the firm the key resolves to
 * @param plan       the current plan identifier, e.g. {@code "starter"}, {@code "business"}
 * @param rateLimit  the rate limit window and remaining budget for this key
 * @param integrator integrator metadata when {@code key.type == "integrator"}, otherwise {@code null}
 */
public record AuthStatusResponse(
        KeyInfo key,
        FirmInfo firm,
        String plan,
        @SerializedName("rate_limit") RateLimit rateLimit,
        IntegratorInfo integrator
) {
    /**
     * Information about the authenticated API key.
     *
     * @param id        the key UUID
     * @param prefix    the human-visible prefix, e.g. {@code "sk_live_abc"}
     * @param type      the key type: {@code "direct"} or {@code "integrator"}
     * @param createdAt ISO 8601 creation timestamp
     * @param lastUsedAt ISO 8601 timestamp of last successful authentication, or {@code null}
     */
    public record KeyInfo(
            String id,
            String prefix,
            String type,
            @SerializedName("created_at") String createdAt,
            @SerializedName("last_used_at") String lastUsedAt
    ) {}

    /**
     * The firm the API key resolves to.
     *
     * @param id           the firm UUID
     * @param name         the legal firm name
     * @param ico          the Slovak ICO (company registration number), or {@code null}
     * @param peppolId     the primary Peppol participant ID, or {@code null}
     * @param peppolStatus Peppol registration status, e.g. {@code "ACTIVE"}
     */
    public record FirmInfo(
            String id,
            String name,
            String ico,
            @SerializedName("peppol_id") String peppolId,
            @SerializedName("peppol_status") String peppolStatus
    ) {}

    /**
     * Rate limit window and remaining budget for the current key.
     *
     * @param limit     the request ceiling per window
     * @param remaining requests remaining in the current window
     * @param resetAt   ISO 8601 timestamp when the window resets
     */
    public record RateLimit(
            int limit,
            int remaining,
            @SerializedName("reset_at") String resetAt
    ) {}

    /**
     * Integrator metadata, present only when {@code key.type == "integrator"}.
     *
     * @param firmsManaged number of client firms managed under this integrator key
     */
    public record IntegratorInfo(
            @SerializedName("firms_managed") int firmsManaged
    ) {}
}
