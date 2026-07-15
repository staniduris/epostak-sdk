package sk.epostak.sdk.models;

/** Optional postal address used for receiver resolution and generated UBL. */
public record ConnectorBusinessAddress(String street, String city, String postalCode) {}
