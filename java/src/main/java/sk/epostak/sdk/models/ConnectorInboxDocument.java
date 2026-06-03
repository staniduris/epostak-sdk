package sk.epostak.sdk.models;

import java.util.Map;

/**
 * One document returned by the Connector inbox.
 */
public record ConnectorInboxDocument(
        String documentId,
        String status,
        String direction,
        String documentType,
        String documentNumber,
        String senderPeppolId,
        String receiverPeppolId,
        String acknowledgedAt,
        String payload,
        String payloadFormat,
        Map<String, String> links
) {}
