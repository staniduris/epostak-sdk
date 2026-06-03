package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Response from {@code POST /connector/send}.
 */
public record ConnectorSendResponse(
        String documentId,
        String status,
        String outcome,
        Map<String, Object> links
) {}
