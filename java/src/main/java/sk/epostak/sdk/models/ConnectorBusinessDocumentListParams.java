package sk.epostak.sdk.models;

public record ConnectorBusinessDocumentListParams(
        String direction,
        String state,
        String type,
        String createdAfter,
        String cursor,
        Integer limit
) {
    public static ConnectorBusinessDocumentListParams empty() {
        return new ConnectorBusinessDocumentListParams(null, null, null, null, null, null);
    }
}
