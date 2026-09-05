package sk.epostak.sdk.models;

/** Shared partner onboarding contract. */
public record WhiteLabelCustomerAuthorization(
        Boolean confirmed,
        String evidenceReference
) {}
