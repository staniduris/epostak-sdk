package sk.epostak.sdk;

import com.google.gson.Gson;
import org.junit.jupiter.api.Test;
import sk.epostak.sdk.models.DocumentEventsResponse;
import sk.epostak.sdk.models.SendDocumentRequest;
import sk.epostak.sdk.models.SendDocumentResponse;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;

final class SendDocumentRequestModelTest {
    @Test
    void serializesLiveJsonBillingPayloadFields() {
        SendDocumentRequest request = SendDocumentRequest.builder("0245:12345678")
                .receiverName("Zakaznik s.r.o.")
                .receiverStreet("Hlavna 1")
                .receiverCity("Bratislava")
                .receiverPostalCode("81101")
                .prepaidAmount(1230.0)
                .prepayments(List.of(new SendDocumentRequest.Prepayment(
                        "ZAL-2026-0004",
                        "DDP-2026-0022",
                        "2026-02-23",
                        1000.0,
                        230.0,
                        1230.0,
                        23.0,
                        "S"
                )))
                .items(List.of(new SendDocumentRequest.LineItem(
                        "Konzultacne sluzby",
                        10,
                        "HUR",
                        50,
                        23,
                        null,
                        "AE",
                        null,
                        "reverse_charge_domestic",
                        "2026-04-01",
                        null,
                        null,
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

        assertEquals(true, json.contains("\"receiverPeppolId\":\"0245:12345678\""));
        assertEquals(true, json.contains("\"receiverStreet\":\"Hlavna 1\""));
        assertEquals(true, json.contains("\"receiverCity\":\"Bratislava\""));
        assertEquals(true, json.contains("\"receiverPostalCode\":\"81101\""));
        assertEquals(true, json.contains("\"prepaidAmount\":1230.0"));
        assertEquals(true, json.contains("\"advanceInvoiceRef\":\"ZAL-2026-0004\""));
        assertEquals(true, json.contains("\"amountWithVat\":1230.0"));
        assertEquals(true, json.contains("\"vatCategoryCode\":\"AE\""));
        assertEquals(true, json.contains("\"taxTreatment\":\"reverse_charge_domestic\""));
        assertEquals(true, json.contains("\"deliveryDate\":\"2026-04-01\""));
        assertEquals(true, json.contains("\"customsTariffCode\":\"72044910\""));
        assertEquals(true, json.contains("\"commodityClassificationCode\":\"72044910\""));
        assertEquals(true, json.contains("\"commodityClassificationListId\":\"HS\""));
        assertEquals(true, json.contains("\"reverseChargeParagraphLetter\":\"f\""));
        assertEquals(true, json.contains("\"controlStatementType\":\"MT\""));
        assertEquals(true, json.contains("\"controlStatementQuantity\":1250.0"));
        assertEquals(true, json.contains("\"controlStatementUnit\":\"kg\""));
        assertEquals(false, json.contains("receiver_peppol_id"));
        assertEquals(false, json.contains("unit_price"));
    }

    @Test
    void serializesAdvanceDeductionLineWithoutPrepaymentEnvelope() {
        SendDocumentRequest request = SendDocumentRequest.builder("0245:12345678")
                .receiverName("Zakaznik s.r.o.")
                .items(List.of(new SendDocumentRequest.LineItem(
                        "Zuctovanie zalohy",
                        1,
                        "C62",
                        -1000,
                        23,
                        null,
                        null,
                        null,
                        null,
                        null,
                        "advance_deduction",
                        "ZF-2026-001",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null
                )))
                .build();

        String json = new Gson().toJson(request);

        assertEquals(true, json.contains("\"receiverName\":\"Zakaznik s.r.o.\""));
        assertEquals(true, json.contains("\"lineType\":\"advance_deduction\""));
        assertEquals(true, json.contains("\"advanceInvoiceReference\":\"ZF-2026-001\""));
        assertEquals(false, json.contains("\"prepaidAmount\""));
        assertEquals(false, json.contains("\"prepayments\""));
    }

    @Test
    void serializesSelfBillingProcessFieldsAndReadsReplayMetadata() {
        SendDocumentRequest request = SendDocumentRequest.builder()
                .processId("urn:peppol:bis:billing_with_response")
                .documentType("self_billing_credit_note")
                .supplierPeppolId("0245:2123038963")
                .supplierName("Dodavatel s.r.o.")
                .supplierIcDph("SK2123038963")
                .precedingInvoiceRef("FAK-2026-0001")
                .items(List.of(new SendDocumentRequest.LineItem("Oprava", 1, 10, 23)))
                .build();

        Gson gson = new Gson();
        String json = gson.toJson(request);
        SendDocumentResponse response = gson.fromJson("""
                {"documentId":"doc-1","submissionId":"doc-1","messageId":"msg-1",\
                "status":"DELIVERED","duplicate":true,"links":{"events":"/events"}}
                """, SendDocumentResponse.class);
        DocumentEventsResponse events = gson.fromJson("""
                {"process_id":"urn:process","documentId":"doc-1","events":[{\
                "process_id":"urn:process","id":"evt-1","eventType":"document.sent",\
                "actor":"system","detail":null,"meta":{},"occurredAt":"2026-07-19T08:00:00Z"}],\
                "pagination":{"limit":20,"nextCursor":null,"hasMore":false}}
                """, DocumentEventsResponse.class);

        assertEquals(true, json.contains("\"processId\":\"urn:peppol:bis:billing_with_response\""));
        assertEquals(true, json.contains("\"documentType\":\"self_billing_credit_note\""));
        assertEquals(true, json.contains("\"supplierPeppolId\":\"0245:2123038963\""));
        assertEquals(true, json.contains("\"precedingInvoiceRef\":\"FAK-2026-0001\""));
        assertEquals(true, response.duplicate());
        assertEquals("/events", response.links().get("events"));
        assertEquals("urn:process", events.processId());
        assertEquals(false, events.pagination().hasMore());
        assertEquals("urn:process", events.events().get(0).processId());
    }
}
