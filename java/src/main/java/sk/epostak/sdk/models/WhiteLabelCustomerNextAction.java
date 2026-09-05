package sk.epostak.sdk.models;

/** Shared partner onboarding contract. */
public record WhiteLabelCustomerNextAction(
        String code,
        String message,
        String url
) {}
