package sk.epostak.sdk.resources;

import com.google.gson.reflect.TypeToken;
import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.CursorPage;
import sk.epostak.sdk.models.OutboundDocument;
import sk.epostak.sdk.models.OutboundEvent;
import sk.epostak.sdk.models.OutboundEventsParams;
import sk.epostak.sdk.models.OutboundListParams;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Pull API — outbound (sent) documents and event stream.
 * <p>
 * Provides cursor-paginated listing of sent documents, single document retrieval,
 * raw UBL download, and a cursor stream of all outbound document events.
 * <p>
 * Requires {@code requireApiEligiblePlan} (api-enterprise or integrator-managed)
 * and scope {@code documents:read}.
 * <p>
 * Access via {@code client.outbound()}.
 *
 * <pre>{@code
 * // Walk all sent invoices delivered this month
 * String cursor = null;
 * do {
 *     CursorPage<OutboundDocument> page = client.outbound().list(
 *         OutboundListParams.builder()
 *             .kind("invoice")
 *             .status("delivered")
 *             .cursor(cursor)
 *             .build());
 *     page.items().forEach(d ->
 *         System.out.println(d.getId() + " delivered " + d.getDeliveredAt()));
 *     cursor = page.nextCursor();
 * } while (cursor != null);
 * }</pre>
 */
public final class OutboundResource {

    private static final TypeToken<CursorPage<OutboundDocument>> DOC_PAGE_TYPE =
            new TypeToken<CursorPage<OutboundDocument>>() {};

    private static final TypeToken<CursorPage<OutboundEvent>> EVENT_PAGE_TYPE =
            new TypeToken<CursorPage<OutboundEvent>>() {};

    private final HttpClient http;

    /**
     * Creates a new outbound resource.
     *
     * @param http the HTTP client used for API communication
     */
    public OutboundResource(HttpClient http) {
        this.http = http;
    }

    /**
     * List outbound documents with cursor pagination. Newest first.
     *
     * @param params optional filters; pass {@link OutboundListParams#empty()} for defaults
     * @return one page of outbound documents with next-cursor
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<OutboundDocument> list(OutboundListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("since", params.since());
            qp.put("limit", params.limit());
            qp.put("kind", params.kind());
            qp.put("status", params.status());
            qp.put("business_status", params.businessStatus());
            qp.put("recipient", params.recipient());
            qp.put("cursor", params.cursor());
        }
        String path = "/outbound/documents" + HttpClient.buildQuery(qp);
        return http.getTyped(path, DOC_PAGE_TYPE);
    }

    /**
     * List outbound documents with default parameters.
     *
     * @return first page of outbound documents (server default limit, no filters)
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<OutboundDocument> list() {
        return list(OutboundListParams.empty());
    }

    /**
     * Retrieve a single outbound document by ID, including delivery attempt history.
     * <p>
     * The detail view includes {@code attemptHistory} which is absent in list items.
     * The server probes both {@code Invoice} and {@code PeppolDocument} tables.
     *
     * @param id the outbound document UUID
     * @return the outbound document detail
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public OutboundDocument get(String id) {
        return http.get("/outbound/documents/" + HttpClient.encode(id), OutboundDocument.class);
    }

    /**
     * Download the raw UBL 2.1 XML for an outbound document.
     * <p>
     * Returns {@code 404} when no UBL is stored for the document.
     *
     * @param id the outbound document UUID
     * @return the raw UBL XML string
     * @throws sk.epostak.sdk.EPostakException if the document or its UBL is not found
     */
    public String getUbl(String id) {
        return http.getString("/outbound/documents/" + HttpClient.encode(id) + "/ubl");
    }

    /**
     * Download the raw AS4 MDN receipt for an outbound document.
     *
     * @param id the outbound document UUID
     * @return raw AS4 MDN bytes
     */
    public byte[] getMdn(String id) {
        return http.getBytes("/outbound/documents/" + HttpClient.encode(id) + "/mdn");
    }

    /**
     * Stream outbound document events with cursor pagination.
     * <p>
     * Each event records a state transition on an outbound document.
     * Pass {@link OutboundEventsParams#forDocument(String)} to scope the stream
     * to a single document.
     *
     * @param params optional filter by {@code documentId} and cursor
     * @return one page of outbound events with next-cursor
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<OutboundEvent> events(OutboundEventsParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("document_id", params.documentId());
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        String path = "/outbound/events" + HttpClient.buildQuery(qp);
        return http.getTyped(path, EVENT_PAGE_TYPE);
    }

    /**
     * Stream all outbound events with default parameters.
     *
     * @return first page of outbound events (server default limit)
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<OutboundEvent> events() {
        return events(OutboundEventsParams.empty());
    }
}
