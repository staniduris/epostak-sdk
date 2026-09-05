package sk.epostak.sdk.models;

import java.util.List;

/** Shared partner onboarding contract. */
public record ConsentOfferResponse(
        String id,
        String consentUrl,
        String status,
        String expiresAt,
        String customerReference,
        String integrationPath,
        String relationshipMode,
        List<String> scopes,
        String acceptedAt,
        String revokedAt
) {}
