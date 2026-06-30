package sk.epostak.sdk.models;

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
        int notFound,
        List<LookupResult> results
) {
    /**
     * Result of a single participant lookup in a batch.
     *
     * @param index                  zero-based position of this participant in the request
     * @param participant            the {@code {scheme, identifier, id}} triple (with {@code id == null}
     *                               when validation failed up-front)
     * @param found                  {@code true} if the participant was found in the SMP
     * @param accepts                {@code true} when the participant can receive the default/probed document type
     * @param routingStatus          routing status such as {@code "ready"} or {@code "lookup_failed"}
     * @param accessPoint            SMP access point (URL + transport profile), or {@code null}
     * @param certificate            AS4 certificate metadata, or {@code null}
     * @param internal               {@code true} when the participant is hosted on our own AP
     * @param supportedDocumentTypes UBL document type IDs the participant advertises, or {@code null}
     * @param source                 origin of the lookup data: {@code "internal"}, {@code "sml"},
     *                               or {@code "directory"}, or {@code null}
     * @param temporaryFailure       {@code true} when an SMP/SML lookup failure prevented a conclusive result
     * @param error                  error message when validation or lookup failed, or {@code null}
     */
    public record LookupResult(
            int index,
            Participant participant,
            boolean found,
            boolean accepts,
            String routingStatus,
            AccessPoint accessPoint,
            CertificateInfo certificate,
            Boolean internal,
            List<String> supportedDocumentTypes,
            String source,
            Boolean temporaryFailure,
            String error
    ) {
        public LookupResult(
                int index,
                Participant participant,
                boolean found,
                AccessPoint accessPoint,
                Boolean internal,
                List<String> supportedDocumentTypes,
                String source,
                String error
        ) {
            this(index, participant, found, false, null, accessPoint, null, internal, supportedDocumentTypes, source, null, error);
        }

        /**
         * Participant identifier triple echoed back in each batch result.
         *
         * @param scheme     Peppol identifier scheme, e.g. {@code "0245"}
         * @param identifier identifier value
         * @param id         combined form {@code "scheme:identifier"}, or {@code null} on invalid input
         */
        public record Participant(
                String scheme,
                String identifier,
                String id
        ) {}

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
}
