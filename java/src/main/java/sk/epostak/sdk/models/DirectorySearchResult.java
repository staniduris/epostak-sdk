package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * Paginated Peppol directory search results.
 *
 * @param items    the list of matching directory entries on this page
 * @param page     current page number (0-based)
 * @param pageSize number of results per page (serialized as {@code page_size})
 * @param hasNext  {@code true} when another page of results is available
 *                 (serialized as {@code has_next})
 */
public record DirectorySearchResult(
        List<DirectoryEntry> items,
        int page,
        @SerializedName("page_size") int pageSize,
        @SerializedName("has_next") boolean hasNext
) {
    /**
     * A single entry in the Peppol Business Card directory.
     *
     * @param participantId    the Peppol participant ID
     * @param name             the registered company name
     * @param countryCode      ISO 3166-1 alpha-2 country code
     * @param registrationDate registration date in ISO 8601 format (YYYY-MM-DD), or {@code null}
     */
    public record DirectoryEntry(
            String participantId,
            String name,
            String countryCode,
            String registrationDate
    ) {}
}
