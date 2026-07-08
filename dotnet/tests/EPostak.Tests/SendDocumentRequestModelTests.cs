using System.Text.Json;
using EPostak.Models;
using Xunit;

namespace EPostak.Tests;

public sealed class SendDocumentRequestModelTests
{
    [Fact]
    public void SerializesSelfBillingCreditNoteJsonPayloadFields()
    {
        var request = new SendDocumentRequest
        {
            DocumentType = "self_billing_credit_note",
            SupplierPeppolId = "0245:2123038963",
            SupplierName = "Dodavatel s.r.o.",
            SupplierIcDph = "SK2123038963",
            PrecedingInvoiceRef = "SB-2026-001",
            InvoiceNumber = "SBCN-2026-001",
            Prepayments =
            [
                new Prepayment
                {
                    AdvanceInvoiceRef = "2651700004",
                    TaxDocumentRef = "2601800022",
                    SettlementDate = "2026-02-23",
                    AmountWithVat = 838.25m
                }
            ],
            Items =
            [
                new LineItem
                {
                    Description = "Oprava mnozstva",
                    Quantity = 1,
                    UnitPrice = 100,
                    VatRate = 23,
                    VatCategoryCode = "AE",
                    TaxTreatment = "reverse_charge_domestic",
                    DeliveryDate = "2026-04-01",
                    LineType = "advance_deduction",
                    AdvanceInvoiceReference = "ZF-2026-001",
                    CustomsTariffCode = "72044910",
                    CommodityClassificationCode = "72044910",
                    CommodityClassificationListId = "HS",
                    ReverseChargeParagraphLetter = "f",
                    ControlStatementType = "MT",
                    ControlStatementQuantity = 1250,
                    ControlStatementUnit = "kg"
                }
            ]
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"documentType\":\"self_billing_credit_note\"", json);
        Assert.Contains("\"supplierPeppolId\":\"0245:2123038963\"", json);
        Assert.Contains("\"supplierIcDph\":\"SK2123038963\"", json);
        Assert.Contains("\"precedingInvoiceRef\":\"SB-2026-001\"", json);
        Assert.Contains("\"invoiceNumber\":\"SBCN-2026-001\"", json);
        Assert.Contains("\"advanceInvoiceRef\":\"2651700004\"", json);
        Assert.Contains("\"taxDocumentRef\":\"2601800022\"", json);
        Assert.Contains("\"amountWithVat\":838.25", json);
        Assert.Contains("\"unitPrice\":100", json);
        Assert.Contains("\"vatRate\":23", json);
        Assert.Contains("\"vatCategoryCode\":\"AE\"", json);
        Assert.Contains("\"taxTreatment\":\"reverse_charge_domestic\"", json);
        Assert.Contains("\"deliveryDate\":\"2026-04-01\"", json);
        Assert.Contains("\"lineType\":\"advance_deduction\"", json);
        Assert.Contains("\"advanceInvoiceReference\":\"ZF-2026-001\"", json);
        Assert.Contains("\"customsTariffCode\":\"72044910\"", json);
        Assert.Contains("\"commodityClassificationCode\":\"72044910\"", json);
        Assert.Contains("\"commodityClassificationListId\":\"HS\"", json);
        Assert.Contains("\"reverseChargeParagraphLetter\":\"f\"", json);
        Assert.Contains("\"controlStatementType\":\"MT\"", json);
        Assert.Contains("\"controlStatementQuantity\":1250", json);
        Assert.Contains("\"controlStatementUnit\":\"kg\"", json);
    }
}
