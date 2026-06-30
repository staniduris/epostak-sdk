package sk.epostak.sdk.models;

import java.util.Map;

public record BoxCreateRequest(
        String payloadXml,
        String scheduledFor,
        String externalId,
        Map<String, Object> metadata
) {}
