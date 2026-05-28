package sk.epostak.sdk.models;

import java.util.List;

/**
 * One integrator API key row from {@code GET /api/v1/integrator/keys}.
 */
public record IntegratorKey(
        String id,
        String keyPrefix,
        String name,
        List<String> scopes,
        List<String> ipAllowlist,
        boolean isActive,
        String lastUsedAt,
        String createdAt
) {}
