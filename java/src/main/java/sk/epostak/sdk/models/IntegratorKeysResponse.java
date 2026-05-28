package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from {@code GET /api/v1/integrator/keys}.
 */
public record IntegratorKeysResponse(List<IntegratorKey> keys) {}
