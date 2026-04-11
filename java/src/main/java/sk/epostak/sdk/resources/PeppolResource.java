package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.CompanyLookup;
import sk.epostak.sdk.models.PeppolParticipant;

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
}
