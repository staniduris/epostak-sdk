package sk.epostak.sdk.models;

import java.util.List;
import java.util.Map;

/**
 * Response from {@code POST /connector/preflight}.
 */
public record ConnectorPreflightResponse(
        boolean ready,
        String outcome,
        ConnectorRepairReport repairReport,
        List<ConnectorRepairItem> warnings,
        List<ConnectorSafeFix> safeFixes,
        Map<String, Object> recipient,
        Map<String, Object> documentProfile,
        Map<String, Object> checks
) {}
