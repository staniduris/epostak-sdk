package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Manage client firms (integrator keys).
 * <p>
 * Provides operations for listing, inspecting, and assigning firms,
 * as well as registering Peppol identifiers. Requires an integrator key
 * ({@code sk_int_*}) for most operations.
 * <p>
 * Access via {@code client.firms()}.
 *
 * <pre>{@code
 * // List all firms, then get detail for each
 * List<FirmSummary> firms = client.firms().list();
 * for (FirmSummary firm : firms) {
 *     FirmDetail detail = client.firms().get(firm.id());
 * }
 * }</pre>
 */
public final class FirmsResource {

    private final HttpClient http;

    /**
     * Creates a new firms resource.
     *
     * @param http the HTTP client used for API communication
     */
    public FirmsResource(HttpClient http) {
        this.http = http;
    }

    /**
     * List all firms accessible to the current API key.
     *
     * @return list of firm summaries, or an empty list if none
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public List<FirmSummary> list() {
        FirmsListWrapper wrapper = http.get("/firms", FirmsListWrapper.class);
        return wrapper != null ? wrapper.firms() : List.of();
    }

    /**
     * Get detailed information for a specific firm, including Peppol identifiers.
     *
     * @param id the firm UUID
     * @return the firm detail
     * @throws sk.epostak.sdk.EPostakException if the firm is not found or the request fails
     */
    public FirmDetail get(String id) {
        return http.get("/firms/" + HttpClient.encode(id), FirmDetail.class);
    }

    /**
     * List documents for a specific firm with filtering and pagination.
     *
     * @param id        the firm UUID
     * @param offset    pagination offset (0-based), or {@code null} for default
     * @param limit     max results per page, or {@code null} for default
     * @param direction filter by direction: {@code "inbound"} or {@code "outbound"}, or {@code null} for all
     * @return paginated list of documents belonging to the firm
     * @throws sk.epostak.sdk.EPostakException if the firm is not found or the request fails
     */
    public InboxListResponse documents(String id, Integer offset, Integer limit, String direction) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("offset", offset);
        params.put("limit", limit);
        params.put("direction", direction);
        return http.get("/firms/" + HttpClient.encode(id) + "/documents" + HttpClient.buildQuery(params), InboxListResponse.class);
    }

    /**
     * List documents for a specific firm with default parameters.
     *
     * @param id the firm UUID
     * @return paginated list of documents belonging to the firm
     * @throws sk.epostak.sdk.EPostakException if the firm is not found or the request fails
     */
    public InboxListResponse documents(String id) {
        return documents(id, null, null, null);
    }

    /**
     * Register a Peppol identifier for a firm, enabling it to send and receive
     * documents on the Peppol network.
     *
     * <pre>{@code
     * PeppolIdentifierResponse resp = client.firms().registerPeppolId(
     *     "firm_uuid", "0245", "12345678");
     * }</pre>
     *
     * @param id         the firm UUID
     * @param scheme     identifier scheme, e.g. {@code "0245"} for Slovak DIČ
     * @param identifier identifier value, e.g. {@code "12345678"}
     * @return the registration response with the full Peppol ID and timestamp
     * @throws sk.epostak.sdk.EPostakException if the firm is not found or registration fails
     */
    public PeppolIdentifierResponse registerPeppolId(String id, String scheme, String identifier) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("scheme", scheme);
        body.put("identifier", identifier);
        return http.post("/firms/" + HttpClient.encode(id) + "/peppol-identifiers", body, PeppolIdentifierResponse.class);
    }

    /**
     * Assign a firm to this integrator by ICO (company registration number).
     *
     * @param ico the Slovak company registration number (ICO)
     * @return the assignment response with firm details and status
     * @throws sk.epostak.sdk.EPostakException if the ICO is invalid or the request fails
     */
    public AssignFirmResponse assign(String ico) {
        Map<String, Object> body = Map.of("ico", ico);
        return http.post("/firms/assign", body, AssignFirmResponse.class);
    }

    /**
     * Batch assign firms to this integrator by ICO (max 50 per request).
     *
     * @param icos list of Slovak company registration numbers (ICO), max 50
     * @return the batch response with per-ICO results and error details
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public BatchAssignResponse assignBatch(List<String> icos) {
        Map<String, Object> body = Map.of("icos", icos);
        return http.post("/firms/assign/batch", body, BatchAssignResponse.class);
    }

    // -- internal wrapper for the list response --------------------------------

    private record FirmsListWrapper(List<FirmSummary> firms) {}

    /**
     * Response from registering a Peppol ID for a firm. Returned as HTTP 201.
     *
     * @param peppolId           the full Peppol participant ID, e.g. {@code "0245:1234567890"}
     * @param registrationStatus registration status, typically {@code "pending"} — SMP
     *                           publication completes asynchronously within 24 hours
     * @param message            human-readable confirmation message
     */
    public record PeppolIdentifierResponse(
            String peppolId,
            String registrationStatus,
            String message
    ) {}
}
