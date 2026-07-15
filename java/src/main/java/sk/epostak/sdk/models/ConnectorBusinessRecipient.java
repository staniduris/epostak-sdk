package sk.epostak.sdk.models;

/** Ordinary business identifiers used to resolve a receiving company. */
public record ConnectorBusinessRecipient(
        String country,
        String name,
        String companyId,
        String taxId,
        String vatId,
        String networkId,
        ConnectorBusinessAddress address
) {
    public ConnectorBusinessRecipient(
            String country,
            String name,
            String companyId,
            String taxId,
            String vatId,
            String networkId
    ) {
        this(country, name, companyId, taxId, vatId, networkId, null);
    }

    public ConnectorBusinessRecipient {
        if (country == null || country.isBlank()) {
            throw new IllegalArgumentException("Connector recipient.country is required");
        }
        if (java.util.stream.Stream.of(companyId, taxId, vatId, networkId)
                .allMatch(value -> value == null || value.isBlank())) {
            throw new IllegalArgumentException(
                    "Connector recipient requires companyId, taxId, vatId, or networkId"
            );
        }
    }

    public static ConnectorBusinessRecipient byTaxId(String country, String taxId) {
        return new ConnectorBusinessRecipient(country, null, null, taxId, null, null, null);
    }

    public static ConnectorBusinessRecipient byCompanyId(String country, String companyId) {
        return new ConnectorBusinessRecipient(country, null, companyId, null, null, null, null);
    }
}
