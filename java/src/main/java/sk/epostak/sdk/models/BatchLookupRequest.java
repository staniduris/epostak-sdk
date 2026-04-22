package sk.epostak.sdk.models;

import java.util.List;

/**
 * Request body for {@code POST /peppol/participants/batch}, performing SMP
 * lookups for multiple participants in a single call.
 *
 * @param participants list of participant identifiers to look up; max 100 entries
 */
public record BatchLookupRequest(
        List<ParticipantId> participants
) {
    /**
     * A single participant identifier in a batch SMP lookup.
     *
     * @param scheme     Peppol identifier scheme, e.g. {@code "0245"}
     * @param identifier identifier value, e.g. {@code "12345678"}
     */
    public record ParticipantId(
            String scheme,
            String identifier
    ) {}
}
