package sk.epostak.sdk.models;

/** Participant managed by the authenticated White Label integrator. */
public record WhiteLabelParticipant(
        String id,
        String customerRef,
        String firmId,
        String operationId,
        String legalName,
        String ico,
        String dic,
        String icDph,
        String peppolId,
        String status,
        String authorizationSource,
        String endpointProfile,
        String managedSince,
        String vatRegType,
        boolean isVatPayer
) {
    /** Retains the original constructor for source compatibility. */
    public WhiteLabelParticipant(String id, String customerRef, String firmId,
            String operationId, String legalName, String ico, String dic,
            String icDph, String peppolId, String status, String authorizationSource,
            String endpointProfile, String managedSince) {
        this(id, customerRef, firmId, operationId, legalName, ico, dic, icDph,
                peppolId, status, authorizationSource, endpointProfile, managedSince,
                null, false);
    }
}
