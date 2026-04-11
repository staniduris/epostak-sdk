package sk.epostak.sdk.models;

/**
 * Detail of an inbox document including the raw UBL XML payload.
 *
 * @param document the full document object
 * @param payload  the UBL XML content as a string, or {@code null} if not available
 */
public record InboxDocumentDetailResponse(
        Document document,
        String payload
) {}
