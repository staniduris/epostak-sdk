package sk.epostak.sdk.models;

import java.util.List;

/**
 * Connector repair report grouped into blocking items and warnings.
 */
public record ConnectorRepairReport(
        String summary,
        List<ConnectorRepairItem> blocking,
        List<ConnectorRepairItem> warnings
) {}
