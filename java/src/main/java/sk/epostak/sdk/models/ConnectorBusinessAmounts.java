package sk.epostak.sdk.models;

/** Normalized monetary totals returned by the Connector business API. */
public record ConnectorBusinessAmounts(Double withoutTax, Double tax, Double total, Double due) {}
