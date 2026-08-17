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
        String managedSince
) {}
