package sk.epostak.sdk.models;

/** Structured settled prepayment on a final invoice. */
public record ConnectorBusinessPrepayment(
        String advanceInvoiceRef,
        String taxDocumentRef,
        String settlementDate,
        Double amountWithoutVat,
        Double vatAmount,
        double amountWithVat,
        Double vatRate,
        String taxTreatment
) {}
