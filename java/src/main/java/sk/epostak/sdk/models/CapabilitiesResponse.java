package sk.epostak.sdk.models;

import java.util.List;

/**
 * Response from {@code POST /peppol/capabilities}.
 * <p>
 * When the participant is not registered the server returns HTTP 404 with
 * {@code {"found":false, "accepts":false, "reason":"..."}}. On a successful
 * lookup the payload reports whether the participant accepts the probed
 * document type, and lists all advertised document types.
 *
 * @param found                  {@code true} when the participant was found in the SMP
 * @param accepts                {@code true} when the participant advertises the requested
 *                               {@code documentType} (or any type, when no specific type was
 *                               requested)
 * @param participant            participant identifier block ({@code scheme}, {@code identifier},
 *                               {@code id}), or {@code null}
 * @param accessPoint            SMP access point metadata, or {@code null}
 * @param internal               {@code true} when the participant is hosted on our own AP
 * @param supportedDocumentTypes list of UBL document type IDs the participant accepts,
 *                               or {@code null} when {@code found == false}
 * @param matchedDocumentType    the document type ID that matched the requested
 *                               {@code documentType}, or {@code null} if no specific type
 *                               was requested or no match was found
 * @param source                 origin of the lookup data: {@code "internal"}, {@code "sml"},
 *                               or {@code "directory"}, or {@code null}
 * @param reason                 human-readable reason the participant was not accepted,
 *                               present only on 404 responses; otherwise {@code null}
 */
public record CapabilitiesResponse(
        boolean found,
        boolean accepts,
        ParticipantId participant,
        AccessPoint accessPoint,
        Boolean internal,
        List<String> supportedDocumentTypes,
        String matchedDocumentType,
        String source,
        String reason
) {
    /**
     * Compact participant identifier triple.
     *
     * @param scheme     Peppol identifier scheme, e.g. {@code "0245"}
     * @param identifier identifier value, e.g. {@code "12345678"}
     * @param id         combined form {@code "scheme:identifier"}
     */
    public record ParticipantId(
            String scheme,
            String identifier,
            String id
    ) {}

    /**
     * SMP access point metadata.
     *
     * @param url              AS4 endpoint URL
     * @param transportProfile transport profile identifier, e.g. {@code "peppol-transport-as4-v2_0"}
     */
    public record AccessPoint(
            String url,
            String transportProfile
    ) {}
}
