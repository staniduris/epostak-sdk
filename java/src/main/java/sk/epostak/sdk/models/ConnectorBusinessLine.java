package sk.epostak.sdk.models;

/** One invoice or credit-note line in normal business terms. */
public record ConnectorBusinessLine(
        String description,
        double quantity,
        double unitPrice,
        double vatRate,
        String unit,
        String taxTreatment,
        Double discount,
        String deliveryDate,
        String lineType,
        String advanceInvoiceReference,
        String customsTariffCode,
        String commodityClassificationCode,
        String commodityClassificationListId,
        String reverseChargeParagraphLetter,
        String controlStatementType,
        Double controlStatementQuantity,
        String controlStatementUnit
) {
    public ConnectorBusinessLine(String description, double quantity, double unitPrice, double vatRate) {
        this(description, quantity, unitPrice, vatRate, null, null, null, null, null,
                null, null, null, null, null, null, null, null);
    }

    public ConnectorBusinessLine(
            String description,
            double quantity,
            double unitPrice,
            double vatRate,
            String unit,
            String taxTreatment
    ) {
        this(description, quantity, unitPrice, vatRate, unit, taxTreatment, null, null, null,
                null, null, null, null, null, null, null, null);
    }
}
