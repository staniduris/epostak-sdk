package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Response from a batch SMP participant lookup.
 *
 * @param total    total number of participants queried
 * @param found    number of participants that were found in the SMP
 * @param notFound number of participants that were not found in the SMP
 * @param results  per-participant results in request order
 */
public record BatchLookupResponse(
        int total,
        int found,
        @SerializedName("not_found") int notFound,
        List<LookupResult> results
) {
    /**
     * Result of a single participant lookup in a batch.
     *
     * @param scheme      the scheme that was queried
     * @param identifier  the identifier that was queried
     * @param found       {@code true} if the participant was found
     * @param participant the resolved participant details, or {@code null} when {@code found == false}
     * @param error       error message when lookup failed for reasons other than "not found",
     *                    or {@code null}
     */
    public record LookupResult(
            String scheme,
            String identifier,
            boolean found,
            PeppolParticipant participant,
            String error
    ) {}
}
