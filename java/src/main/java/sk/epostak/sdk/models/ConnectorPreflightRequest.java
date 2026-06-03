package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Request body for {@code POST /connector/preflight}.
 *
 * @param receiverPeppolId receiver's Peppol participant ID, e.g. {@code "0245:2123456789"}
 * @param document ERP invoice JSON, mapped fields, or UBL/XML envelope metadata
 */
public record ConnectorPreflightRequest(
        String receiverPeppolId,
        Map<String, Object> document
) {}
