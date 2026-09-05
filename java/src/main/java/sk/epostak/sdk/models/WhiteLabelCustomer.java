package sk.epostak.sdk.models;

/** Shared partner onboarding contract. */
public record WhiteLabelCustomer(
        String id,
        String firmId,
        String customerRef,
        String relationship,
        String name,
        String country,
        String companyId,
        String taxId,
        String vatId,
        String status,
        WhiteLabelCustomerActivation activation,
        WhiteLabelCustomerNextAction nextAction,
        WhiteLabelCustomerPolicy policy,
        int version,
        String createdAt,
        String updatedAt
) {}
