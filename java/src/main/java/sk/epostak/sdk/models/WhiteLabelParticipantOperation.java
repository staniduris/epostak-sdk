package sk.epostak.sdk.models;

/** Asynchronous White Label participant registration or migration operation. */
public record WhiteLabelParticipantOperation(
        String id,
        String operationType,
        String status,
        String customerRef,
        String dic,
        String peppolId,
        String legalName,
        String companyEmail,
        String firmId,
        String participantId,
        boolean reviewRequired,
        WhiteLabelOperationError error,
        String createdAt,
        String completedAt
) {}
