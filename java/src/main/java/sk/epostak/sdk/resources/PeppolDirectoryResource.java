package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.DirectorySearchResult;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Peppol Business Card directory search.
 * <p>
 * Provides full-text search of the Peppol directory to find registered
 * participants by name or country.
 * <p>
 * Access via {@code client.peppol().directory()}.
 *
 * <pre>{@code
 * DirectorySearchResult result = client.peppol().directory().search("Acme", "SK", 0, 20);
 * for (DirectorySearchResult.DirectoryEntry entry : result.results()) {
 *     System.out.println(entry.name() + " - " + entry.peppolId());
 * }
 * }</pre>
 */
public final class PeppolDirectoryResource {

    private final HttpClient http;

    /**
     * Creates a new Peppol directory resource.
     *
     * @param http the HTTP client used for API communication
     */
    PeppolDirectoryResource(HttpClient http) {
        this.http = http;
    }

    /**
     * Search the Peppol Business Card directory with filtering and pagination.
     *
     * @param q        full-text search query (company name, etc.), or {@code null}
     * @param country  ISO 3166-1 alpha-2 country code filter (e.g. {@code "SK"}, {@code "CZ"}), or {@code null}
     * @param page     page number (0-based), or {@code null} for default (0)
     * @param pageSize results per page, or {@code null} for default
     * @return paginated search results with matching directory entries
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public DirectorySearchResult search(String q, String country, Integer page, Integer pageSize) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("q", q);
        params.put("country", country);
        params.put("page", page);
        params.put("page_size", pageSize);
        return http.get("/peppol/directory/search" + HttpClient.buildQuery(params), DirectorySearchResult.class);
    }

    /**
     * Search the Peppol directory with default parameters.
     *
     * @return the first page of directory entries
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public DirectorySearchResult search() {
        return search(null, null, null, null);
    }
}
