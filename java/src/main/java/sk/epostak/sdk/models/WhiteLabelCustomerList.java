package sk.epostak.sdk.models;

import java.util.List;

/** Shared partner onboarding contract. */
public record WhiteLabelCustomerList(
        List<WhiteLabelCustomer> customers,
        String nextCursor,
        boolean hasMore
) {}
