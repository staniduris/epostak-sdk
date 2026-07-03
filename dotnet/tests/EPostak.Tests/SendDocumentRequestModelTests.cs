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
                    VatRate = 23
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
    }
}
