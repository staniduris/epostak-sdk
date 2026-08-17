package sk.epostak.sdk.models;

/** Request to migrate a participant using an SMP migration code. Never log it. */
public record WhiteLabelParticipantMigrationRequest(
        String customerRef,
        String dic,
        String companyEmail,
        String migrationCode
) {}
