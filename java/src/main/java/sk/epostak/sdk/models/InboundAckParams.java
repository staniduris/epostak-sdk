package sk.epostak.sdk.models;

/**
 * Parameters for acknowledging an inbound document
 * ({@code POST /inbound/documents/{id}/ack}).
 * <p>
 * All fields are optional. The {@code clientReference} field (max 256 chars)
 * is stored server-side and returned in subsequent list/get responses.
 * Acknowledging an already-acknowledged document is idempotent — the server
 * overwrites {@code clientAckedAt} with the latest call timestamp
 * (latest-ack-wins semantics).
 *
 * @param clientReference caller-supplied reference to correlate with an internal
 *                        purchase order or ERP record, or {@code null}
 */
public record InboundAckParams(String clientReference) {

    /**
     * Acknowledge without providing a client reference.
     *
     * @return params with no client reference
     */
    public static InboundAckParams empty() {
        return new InboundAckParams(null);
    }
}
