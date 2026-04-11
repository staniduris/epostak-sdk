package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Paginated Peppol directory search results.
 *
 * @param results  the list of matching directory entries on this page
 * @param total    total number of matching entries
 * @param page     current page number (0-based)
 * @param pageSize number of results per page
 */
public record DirectorySearchResult(
        List<DirectoryEntry> results,
        int total,
        int page,
        @SerializedName("page_size") int pageSize
) {
    /**
     * A single entry in the Peppol Business Card directory.
     *
     * @param peppolId     the Peppol participant ID
     * @param name         the registered company name
     * @param country      ISO 3166-1 alpha-2 country code
     * @param registeredAt ISO 8601 timestamp of registration
     */
    public record DirectoryEntry(
            @SerializedName("peppol_id") String peppolId,
            String name,
            String country,
            @SerializedName("registered_at") String registeredAt
    ) {}
}
