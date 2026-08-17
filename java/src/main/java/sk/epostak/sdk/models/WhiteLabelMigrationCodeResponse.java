package sk.epostak.sdk.models;

/** Outgoing migration operation and the ephemeral migration code, when available. */
public record WhiteLabelMigrationCodeResponse(
        WhiteLabelParticipantOperation operation,
        String migrationCode
) {}
