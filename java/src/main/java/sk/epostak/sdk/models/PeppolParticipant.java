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
 * @param accepts                {@code true} when the participant can receive the default/probed document type
 * @param routingStatus          routing status such as {@code "ready"} or {@code "document_type_not_supported"}
 * @param participantId          Peppol participant ID, e.g. {@code "0245:12345678"}
 * @param scheme                 Peppol identifier scheme, e.g. {@code "0245"}
 * @param identifier             identifier value within the scheme
 * @param accessPoint            SMP access point metadata (URL + transport profile), or {@code null}
 *                               when the source did not expose it (e.g. Peppol Directory)
 * @param certificate            AS4 certificate metadata, or {@code null}
 * @param supportedDocumentTypes list of UBL document type IDs the participant advertises, or {@code null}
 * @param source                 origin of the lookup data: {@code "internal"}, {@code "sml"}, or {@code "directory"}
 * @param internal               {@code true} when the participant is hosted on our own AP
 * @param temporaryFailure       {@code true} when an SMP/SML lookup failure prevented a conclusive result
 */
public record PeppolParticipant(
        boolean found,
        boolean accepts,
        String routingStatus,
        String participantId,
        String scheme,
        String identifier,
        AccessPoint accessPoint,
        CertificateInfo certificate,
        List<String> supportedDocumentTypes,
        String source,
        Boolean internal,
        Boolean temporaryFailure
) {
    public PeppolParticipant(
            boolean found,
            String participantId,
            String scheme,
            AccessPoint accessPoint,
            List<String> supportedDocumentTypes,
            String source,
            Boolean internal
    ) {
        this(
                found,
                false,
                null,
                participantId,
                scheme,
                null,
                accessPoint,
                null,
                supportedDocumentTypes,
                source,
                internal,
                null
        );
    }

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

    /**
     * AS4 certificate metadata.
     *
     * @param present               whether certificate metadata was present in the SMP response
     * @param fingerprintSha256     SHA-256 certificate fingerprint, when available
     * @param subject               certificate subject, when available
     * @param issuer                certificate issuer, when available
     * @param serialNumber          certificate serial number, when available
     * @param notBefore             certificate not-before timestamp, when available
     * @param notAfter              certificate not-after timestamp, when available
     * @param serviceActivationDate SMP service activation timestamp, when available
     * @param serviceExpirationDate SMP service expiration timestamp, when available
     * @param valid                 whether the endpoint certificate is currently valid
     * @param expiresAt             certificate expiration timestamp, when available
     * @param error                 certificate parsing/validation error code, when present
     */
    public record CertificateInfo(
            Boolean present,
            String fingerprintSha256,
            String subject,
            String issuer,
            String serialNumber,
            String notBefore,
            String notAfter,
            String serviceActivationDate,
            String serviceExpirationDate,
            Boolean valid,
            String expiresAt,
            String error
    ) {}
}
