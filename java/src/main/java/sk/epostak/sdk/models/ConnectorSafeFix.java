package sk.epostak.sdk.models;

/**
 * A safe automatic fix considered by Connector preflight.
 */
public record ConnectorSafeFix(
        String code,
        String field,
        String message,
        boolean applied
) {}
