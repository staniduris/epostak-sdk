package sk.epostak.sdk.resources;

import com.google.gson.reflect.TypeToken;
import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.CursorPage;
import sk.epostak.sdk.models.InboundAckParams;
import sk.epostak.sdk.models.InboundDocument;
import sk.epostak.sdk.models.InboundListParams;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Pull API — inbound (received) documents.
 * <p>
 * Provides cursor-paginated listing, single document retrieval, raw UBL download,
 * and idempotent acknowledgement of inbound Peppol documents.
 * <p>
 * Requires {@code requireApiEligiblePlan} (api-enterprise or integrator-managed)
 * and scope {@code documents:read} for read operations, {@code documents:write}
 * for {@link #ack(String, InboundAckParams)}.
 * <p>
 * Access via {@code client.inbound()}.
 *
 * <pre>{@code
 * CursorPage<InboundDocument> page = client.inbound().list(
 *     InboundListParams.builder()
 *         .since("2026-05-01T00:00:00Z")
 *         .limit(100)
 *         .build());
 *
 * for (InboundDocument doc : page.items()) {
 *     String xml = client.inbound().getUbl(doc.getId());
 *     processInvoice(xml);
 *     client.inbound().ack(doc.getId(), InboundAckParams.empty());
 * }
 * }</pre>
 */
public final class InboundResource {

    private static final TypeToken<CursorPage<InboundDocument>> PAGE_TYPE =
            new TypeToken<CursorPage<InboundDocument>>() {};

    private final HttpClient http;

    /**
     * Creates a new inbound resource.
     *
     * @param http the HTTP client used for API communication
     */
    public InboundResource(HttpClient http) {
        this.http = http;
    }

    /**
     * List inbound documents with cursor pagination. Newest first.
     *
     * @param params optional filters; pass {@link InboundListParams#empty()} for defaults
     * @return one page of inbound documents with next-cursor
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<InboundDocument> list(InboundListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("since", params.since());
            qp.put("limit", params.limit());
            qp.put("kind", params.kind());
            qp.put("sender", params.sender());
            qp.put("cursor", params.cursor());
        }
        String path = "/inbound/documents" + HttpClient.buildQuery(qp);
        return http.getTyped(path, PAGE_TYPE);
    }

    /**
     * List inbound documents with default parameters.
     *
     * @return first page of inbound documents (server default limit, no filters)
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<InboundDocument> list() {
        return list(InboundListParams.empty());
    }

    /**
     * Retrieve a single inbound document by ID.
     *
     * @param id the inbound document UUID
     * @return the inbound document detail
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public InboundDocument get(String id) {
        return http.get("/inbound/documents/" + HttpClient.encode(id), InboundDocument.class);
    }

    /**
     * Download the raw UBL 2.1 XML for an inbound document.
     * <p>
     * Returns {@code 404} when the document has no stored UBL (legacy rows).
     *
     * @param id the inbound document UUID
     * @return the raw UBL XML string
     * @throws sk.epostak.sdk.EPostakException if the document or its UBL is not found
     */
    public String getUbl(String id) {
        return http.getString("/inbound/documents/" + HttpClient.encode(id) + "/ubl");
    }

    /**
     * Acknowledge an inbound document (mark it as processed).
     * <p>
     * This operation is idempotent. Re-acknowledging an already-acknowledged document
     * overwrites the {@code clientAckedAt} timestamp with the latest call time
     * (latest-ack-wins semantics).
     * <p>
     * Requires scope {@code documents:write}.
     *
     * @param id     the inbound document UUID
     * @param params optional params including a {@code clientReference} (max 256 chars)
     * @return the updated inbound document with {@code clientAckedAt} set
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public InboundDocument ack(String id, InboundAckParams params) {
        Map<String, Object> body = new LinkedHashMap<>();
        if (params != null && params.clientReference() != null) {
            body.put("client_reference", params.clientReference());
        }
        return http.post(
                "/inbound/documents/" + HttpClient.encode(id) + "/ack",
                body.isEmpty() ? null : body,
                InboundDocument.class
        );
    }

    /**
     * Acknowledge an inbound document without providing a client reference.
     *
     * @param id the inbound document UUID
     * @return the updated inbound document with {@code clientAckedAt} set
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public InboundDocument ack(String id) {
        return ack(id, InboundAckParams.empty());
    }
}
