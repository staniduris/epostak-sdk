using System.Text.Json;
using System.Text.Json.Serialization;
using EPostak.Models;
using Xunit;

namespace EPostak.Tests;

public sealed class SendDocumentRequestModelTests
{
    private static readonly JsonSerializerOptions SdkJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void SerializesLiveJsonBillingPayloadFields()
    {
        var request = new SendDocumentRequest
        {
            ReceiverPeppolId = "0245:12345678",
            ReceiverName = "Zakaznik s.r.o.",
            ReceiverStreet = "Hlavna 1",
            ReceiverCity = "Bratislava",
            ReceiverPostalCode = "81101",
            PrepaidAmount = 1230,
            Prepayments =
            [
                new Prepayment
                {
                    AdvanceInvoiceRef = "ZAL-2026-0004",
                    TaxDocumentRef = "DDP-2026-0022",
                    SettlementDate = "2026-02-23",
                    AmountWithoutVat = 1000,
                    VatAmount = 230,
                    AmountWithVat = 1230,
                    VatRate = 23,
                    VatCategoryCode = "S"
                }
            ],
            Items =
            [
                new LineItem
                {
                    Description = "Konzultacne sluzby",
                    Quantity = 10,
                    Unit = "HUR",
                    UnitPrice = 50,
                    VatRate = 23,
                    VatCategoryCode = "AE",
                    TaxTreatment = "reverse_charge_domestic",
                    DeliveryDate = "2026-04-01",
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

        var json = JsonSerializer.Serialize(request, SdkJsonOptions);

        Assert.Contains("\"receiverPeppolId\":\"0245:12345678\"", json);
        Assert.Contains("\"receiverStreet\":\"Hlavna 1\"", json);
        Assert.Contains("\"receiverCity\":\"Bratislava\"", json);
        Assert.Contains("\"receiverPostalCode\":\"81101\"", json);
        Assert.Contains("\"prepaidAmount\":1230", json);
        Assert.Contains("\"advanceInvoiceRef\":\"ZAL-2026-0004\"", json);
        Assert.Contains("\"amountWithVat\":1230", json);
        Assert.Contains("\"vatCategoryCode\":\"AE\"", json);
        Assert.Contains("\"taxTreatment\":\"reverse_charge_domestic\"", json);
        Assert.Contains("\"deliveryDate\":\"2026-04-01\"", json);
        Assert.Contains("\"customsTariffCode\":\"72044910\"", json);
        Assert.Contains("\"commodityClassificationCode\":\"72044910\"", json);
        Assert.Contains("\"commodityClassificationListId\":\"HS\"", json);
        Assert.Contains("\"reverseChargeParagraphLetter\":\"f\"", json);
        Assert.Contains("\"controlStatementType\":\"MT\"", json);
        Assert.Contains("\"controlStatementQuantity\":1250", json);
        Assert.Contains("\"controlStatementUnit\":\"kg\"", json);
    }

    [Fact]
    public void SerializesAdvanceDeductionLineWithoutPrepaymentEnvelope()
    {
        var request = new SendDocumentRequest
        {
            ReceiverPeppolId = "0245:12345678",
            ReceiverName = "Zakaznik s.r.o.",
            Items =
            [
                new LineItem
                {
                    Description = "Zuctovanie zalohy",
                    Quantity = 1,
                    Unit = "C62",
                    UnitPrice = -1000,
                    VatRate = 23,
                    LineType = "advance_deduction",
                    AdvanceInvoiceReference = "ZF-2026-001"
                }
            ]
        };

        var json = JsonSerializer.Serialize(request, SdkJsonOptions);

        Assert.Contains("\"receiverName\":\"Zakaznik s.r.o.\"", json);
        Assert.Contains("\"lineType\":\"advance_deduction\"", json);
        Assert.Contains("\"advanceInvoiceReference\":\"ZF-2026-001\"", json);
        Assert.DoesNotContain("\"prepaidAmount\"", json);
        Assert.DoesNotContain("\"prepayments\"", json);
    }
}
