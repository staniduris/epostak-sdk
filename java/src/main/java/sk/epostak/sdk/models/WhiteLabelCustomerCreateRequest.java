package sk.epostak.sdk.models;

/** Shared partner onboarding contract. */
public record WhiteLabelCustomerCreateRequest(
        String customerRef,
        String relationship,
        String country,
        String companyId,
        String taxId,
        String vatId,
        String contactEmail,
        String returnUrl,
        WhiteLabelCustomerAuthorization customerAuthorization
) {}
