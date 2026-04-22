package sk.epostak.sdk.models;

import java.util.List;

/**
 * Peppol SMP participant lookup result.
 * <p>
 * When the participant is not registered on the Peppol network, the server
 * responds with HTTP 404 and body {@code {"found": false}}. The SDK converts
 * this into a {@link sk.epostak.sdk.EPostakException} with status 404.
 * On success, the body contains the participant ID, SMP access point endpoint,
 * optional supported document types, and the data source.
 *
 * @param found                  always {@code true} on successful lookup
 * @param participantId          Peppol participant ID, e.g. {@code "12345678"} (without scheme prefix)
 * @param scheme                 Peppol identifier scheme, e.g. {@code "0245"}
 * @param accessPoint            SMP access point metadata (URL + transport profile), or {@code null}
 *                               when the source did not expose it (e.g. Peppol Directory)
 * @param supportedDocumentTypes list of UBL document type IDs the participant advertises, or {@code null}
 * @param source                 origin of the lookup data: {@code "internal"}, {@code "sml"}, or {@code "directory"}
 * @param internal               {@code true} when the participant is hosted on our own AP
 */
public record PeppolParticipant(
        boolean found,
        String participantId,
        String scheme,
        AccessPoint accessPoint,
        List<String> supportedDocumentTypes,
        String source,
        Boolean internal
) {
    /**
     * SMP access point metadata.
     *
     * @param url              AS4 endpoint URL
     * @param transportProfile transport profile identifier, e.g. {@code "peppol-transport-as4-v2_0"}, or {@code null}
     */
    public record AccessPoint(
            String url,
            String transportProfile
    ) {}
}
