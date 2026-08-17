package sk.epostak.sdk.models;

/** Request to register a participant using the one-time FS SR verification token. Never log it. */
public record WhiteLabelParticipantRegistrationRequest(
        String customerRef,
        String dic,
        String companyEmail,
        String verificationToken
) {}
