package sk.epostak.sdk;

import com.google.gson.Gson;
import org.junit.jupiter.api.Test;
import sk.epostak.sdk.models.SendDocumentRequest;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;

final class SendDocumentRequestModelTest {
    @Test
    void serializesSelfBillingCreditNoteJsonPayloadFields() {
        SendDocumentRequest request = SendDocumentRequest.builder()
                .documentType("self_billing_credit_note")
                .supplierPeppolId("0245:2123038963")
                .supplierName("Dodavatel s.r.o.")
                .supplierIcDph("SK2123038963")
                .precedingInvoiceRef("SB-2026-001")
                .invoiceNumber("SBCN-2026-001")
                .prepayments(List.of(new SendDocumentRequest.Prepayment(
                        "2651700004",
                        "2601800022",
                        "2026-02-23",
                        838.25
                )))
                .items(List.of(new SendDocumentRequest.LineItem(
                        "Oprava mnozstva",
                        1,
                        "C62",
                        100,
                        23,
                        null,
                        "AE",
                        null,
                        "reverse_charge_domestic",
                        "2026-04-01",
                        "advance_deduction",
                        "ZF-2026-001",
                        "72044910",
                        "72044910",
                        "HS",
                        "f",
                        "MT",
                        1250.0,
                        "kg"
                )))
                .build();

        String json = new Gson().toJson(request);

        assertEquals(true, json.contains("\"documentType\":\"self_billing_credit_note\""));
        assertEquals(true, json.contains("\"supplierPeppolId\":\"0245:2123038963\""));
        assertEquals(true, json.contains("\"supplierIcDph\":\"SK2123038963\""));
        assertEquals(true, json.contains("\"precedingInvoiceRef\":\"SB-2026-001\""));
        assertEquals(true, json.contains("\"invoiceNumber\":\"SBCN-2026-001\""));
        assertEquals(true, json.contains("\"advanceInvoiceRef\":\"2651700004\""));
        assertEquals(true, json.contains("\"taxDocumentRef\":\"2601800022\""));
        assertEquals(true, json.contains("\"amountWithVat\":838.25"));
        assertEquals(true, json.contains("\"unitPrice\":100.0"));
        assertEquals(true, json.contains("\"vatRate\":23.0"));
        assertEquals(true, json.contains("\"vatCategoryCode\":\"AE\""));
        assertEquals(true, json.contains("\"taxTreatment\":\"reverse_charge_domestic\""));
        assertEquals(true, json.contains("\"deliveryDate\":\"2026-04-01\""));
        assertEquals(true, json.contains("\"lineType\":\"advance_deduction\""));
        assertEquals(true, json.contains("\"advanceInvoiceReference\":\"ZF-2026-001\""));
        assertEquals(true, json.contains("\"customsTariffCode\":\"72044910\""));
        assertEquals(true, json.contains("\"commodityClassificationCode\":\"72044910\""));
        assertEquals(true, json.contains("\"commodityClassificationListId\":\"HS\""));
        assertEquals(true, json.contains("\"reverseChargeParagraphLetter\":\"f\""));
        assertEquals(true, json.contains("\"controlStatementType\":\"MT\""));
        assertEquals(true, json.contains("\"controlStatementQuantity\":1250.0"));
        assertEquals(true, json.contains("\"controlStatementUnit\":\"kg\""));
    }
}
