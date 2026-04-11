package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Access received (inbound) documents.
 * <p>
 * Provides listing, detail retrieval, and acknowledgement of documents
 * received via Peppol. Integrator keys can also list documents across all firms.
 * <p>
 * Access via {@code client.documents().inbox()}.
 *
 * <pre>{@code
 * // List new documents, then acknowledge each one
 * InboxListResponse inbox = client.documents().inbox().list(null, 50, "RECEIVED", null);
 * for (Document doc : inbox.documents()) {
 *     process(doc);
 *     client.documents().inbox().acknowledge(doc.getId());
 * }
 * }</pre>
 */
public final class InboxResource {

    private final HttpClient http;

    /**
     * Creates a new inbox resource.
     *
     * @param http the HTTP client used for API communication
     */
    InboxResource(HttpClient http) {
        this.http = http;
    }

    /**
     * List received documents with filtering and pagination.
     *
     * @param offset pagination offset (0-based), or {@code null} for default
     * @param limit  max results per page (1-100, default 20), or {@code null} for default
     * @param status filter by status: {@code "RECEIVED"} or {@code "ACKNOWLEDGED"}, or {@code null} for all
     * @param since  only return documents received after this ISO 8601 timestamp, or {@code null}
     * @return paginated list of inbox documents
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public InboxListResponse list(Integer offset, Integer limit, String status, String since) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("offset", offset);
        params.put("limit", limit);
        params.put("status", status);
        params.put("since", since);
        return http.get("/documents/inbox" + HttpClient.buildQuery(params), InboxListResponse.class);
    }

    /**
     * List received documents with default parameters.
     *
     * @return paginated list of inbox documents (offset 0, limit 20, all statuses)
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public InboxListResponse list() {
        return list(null, null, null, null);
    }

    /**
     * Get full detail of an inbox document including the UBL XML payload.
     *
     * @param id the inbox document UUID
     * @return the document detail with UBL XML content
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public InboxDocumentDetailResponse get(String id) {
        return http.get("/documents/inbox/" + HttpClient.encode(id), InboxDocumentDetailResponse.class);
    }

    /**
     * Mark an inbox document as processed (acknowledged). This is an idempotent
     * operation -- acknowledging an already-acknowledged document is a no-op.
     *
     * @param id the inbox document UUID
     * @return the acknowledgement confirmation with timestamp
     * @throws sk.epostak.sdk.EPostakException if the document is not found or the request fails
     */
    public AcknowledgeResponse acknowledge(String id) {
        return http.post("/documents/inbox/" + HttpClient.encode(id) + "/acknowledge", null, AcknowledgeResponse.class);
    }

    /**
     * List received documents across all firms. Requires an integrator key ({@code sk_int_*}).
     *
     * @param offset pagination offset (0-based), or {@code null} for default
     * @param limit  max results per page (1-200, default 50), or {@code null} for default
     * @param status filter by status: {@code "RECEIVED"} or {@code "ACKNOWLEDGED"}, or {@code null} for all
     * @param since  only return documents received after this ISO 8601 timestamp, or {@code null}
     * @param firmId filter to a specific firm UUID, or {@code null} for all firms
     * @return paginated list of documents across all firms
     * @throws sk.epostak.sdk.EPostakException if the request fails or the key is not an integrator key
     */
    public InboxAllResponse listAll(Integer offset, Integer limit, String status, String since, String firmId) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("offset", offset);
        params.put("limit", limit);
        params.put("status", status);
        params.put("since", since);
        params.put("firm_id", firmId);
        return http.get("/documents/inbox/all" + HttpClient.buildQuery(params), InboxAllResponse.class);
    }

    /**
     * List received documents across all firms with default parameters.
     *
     * @return paginated list of documents across all firms (offset 0, limit 50, all statuses)
     * @throws sk.epostak.sdk.EPostakException if the request fails or the key is not an integrator key
     */
    public InboxAllResponse listAll() {
        return listAll(null, null, null, null, null);
    }
}
