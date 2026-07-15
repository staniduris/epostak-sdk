package sk.epostak.sdk.models;

/** Verified sender or recipient returned by the Connector business API. */
public record ConnectorBusinessParty(
        String name,
        String country,
        String companyId,
        String taxId,
        String vatId,
        String resolution
) {}
