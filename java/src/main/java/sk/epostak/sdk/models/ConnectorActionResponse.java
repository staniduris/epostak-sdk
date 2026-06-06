package sk.epostak.sdk.models;

import java.util.Map;

/**
 * Connector action execution response.
 */
public record ConnectorActionResponse(Map<String, Object> action) {
}
