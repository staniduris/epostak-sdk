package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.BatchLookupRequest;
import sk.epostak.sdk.models.BatchLookupResponse;
import sk.epostak.sdk.models.CapabilitiesRequest;
import sk.epostak.sdk.models.CapabilitiesResponse;
import sk.epostak.sdk.models.CompanyLookup;
import sk.epostak.sdk.models.PeppolParticipant;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * SMP participant lookup and Slovak company lookup.
 * <p>
 * Provides operations for querying the Peppol SMP (Service Metadata Publisher)
 * to check participant capabilities, and for looking up Slovak companies by ICO.
 * <p>
 * Access via {@code client.peppol()}.
 *
 * <pre>{@code
 * // Lookup a Peppol participant
 * PeppolParticipant participant = client.peppol().lookup("0245", "12345678");
 * System.out.println(participant.name());
 *
 * // Search the Peppol directory
 * DirectorySearchResult results = client.peppol().directory().search("Acme", "SK", null, null);
 * }</pre>
 */
public final class PeppolResource {

    private final HttpClient http;
    private final PeppolDirectoryResource directory;

    /**
     * Creates a new Peppol resource.
     *
     * @param http the HTTP client used for API communication
     */
    public PeppolResource(HttpClient http) {
        this.http = http;
        this.directory = new PeppolDirectoryResource(http);
    }

    /**
     * Access the Peppol Business Card directory for searching registered participants.
     *
     * @return the directory sub-resource
     */
    public PeppolDirectoryResource directory() {
        return directory;
    }

    /**
     * Perform an SMP (Service Metadata Publisher) participant lookup to retrieve
     * a participant's name, country, and supported document capabilities.
     *
     * <pre>{@code
     * PeppolParticipant p = client.peppol().lookup("0245", "12345678");
     * for (PeppolParticipant.Capability cap : p.capabilities()) {
     *     System.out.println(cap.documentTypeId());
     * }
     * }</pre>
     *
     * @param scheme     identifier scheme, e.g. {@code "0245"} for Slovak DIČ
     * @param identifier identifier value, e.g. {@code "12345678"}
     * @return the participant with capabilities
     * @throws sk.epostak.sdk.EPostakException if the participant is not found (404) or the request fails
     */
    public PeppolParticipant lookup(String scheme, String identifier) {
        return http.get(
                "/peppol/participants/" + HttpClient.encode(scheme) + "/" + HttpClient.encode(identifier),
                PeppolParticipant.class
        );
    }

    /**
     * Look up a Slovak company by ICO (company registration number). Returns
     * the company name, address, tax identifiers, and Peppol ID if registered.
     *
     * @param ico the Slovak company registration number (ICO), e.g. {@code "12345678"}
     * @return the company lookup result
     * @throws sk.epostak.sdk.EPostakException if the company is not found or the request fails
     */
    public CompanyLookup companyLookup(String ico) {
        return http.get("/company/lookup/" + HttpClient.encode(ico), CompanyLookup.class);
    }

    @SuppressWarnings("unchecked")
    public Map<String, Object> companySearch(String q, Integer limit) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("q", q);
        params.put("limit", limit);
        return http.get("/company/search" + HttpClient.buildQuery(params), Map.class);
    }

    /**
     * Probe whether a participant can receive a specific document type. The response
     * reports the full set of supported document types and business processes, plus
     * the matched document type ID when a specific {@code documentType} is requested.
     *
     * <pre>{@code
     * CapabilitiesResponse caps = client.peppol().capabilities(new CapabilitiesRequest(
     *     "0245", "12345678",
     *     "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"));
     * if (caps.found() && caps.matchedDocumentType() != null) {
     *     // safe to send this document type
     * }
     * }</pre>
     *
     * @param request the participant and document type to probe
     * @return the capabilities response
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CapabilitiesResponse capabilities(CapabilitiesRequest request) {
        return http.post("/peppol/capabilities", request, CapabilitiesResponse.class);
    }

    /**
     * Look up multiple Peppol participants in a single SMP round-trip. Results are
     * returned in the same order as the request.
     *
     * <pre>{@code
     * BatchLookupResponse resp = client.peppol().lookupBatch(List.of(
     *     new BatchLookupRequest.ParticipantId("0245", "12345678"),
     *     new BatchLookupRequest.ParticipantId("0245", "87654321")));
     * System.out.println(resp.found() + "/" + resp.total() + " registered");
     * }</pre>
     *
     * @param participants the participants to look up; max 100 entries
     * @return the batch lookup response with per-participant results
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public BatchLookupResponse lookupBatch(List<BatchLookupRequest.ParticipantId> participants) {
        return http.post(
                "/peppol/participants/batch",
                new BatchLookupRequest(participants),
                BatchLookupResponse.class
        );
    }
}
