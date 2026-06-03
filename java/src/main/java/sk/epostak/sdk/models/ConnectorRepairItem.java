package sk.epostak.sdk.models;

/**
 * One repair-report finding returned by Connector preflight.
 */
public record ConnectorRepairItem(
        String code,
        String field,
        String message,
        String category,
        boolean autoFixable
) {}
