package sk.epostak.sdk.models;

/**
 * Response from {@code DELETE /api/v1/integrator/keys}.
 */
public record DeactivateIntegratorKeyResponse(boolean success, String message) {}
