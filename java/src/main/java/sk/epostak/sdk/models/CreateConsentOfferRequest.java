package sk.epostak.sdk.models;

import java.util.List;

/** Shared partner onboarding contract. */
public record CreateConsentOfferRequest(
        String targetIdentifierType,
        String targetIdentifier,
        String customerReference,
        String integrationPath,
        String relationshipMode,
        List<String> scopes
) {}
