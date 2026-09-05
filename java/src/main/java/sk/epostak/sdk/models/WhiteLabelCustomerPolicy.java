package sk.epostak.sdk.models;

/** Shared partner onboarding contract. */
public record WhiteLabelCustomerPolicy(
        String sendMode,
        String ublHandling,
        boolean ocrAutoSend,
        String attachSourceFile,
        String locale,
        int version
) {}
