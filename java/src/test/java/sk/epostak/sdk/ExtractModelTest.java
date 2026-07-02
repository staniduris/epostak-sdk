package sk.epostak.sdk;

import com.google.gson.Gson;
import org.junit.jupiter.api.Test;
import sk.epostak.sdk.models.ExtractResult;
import sk.epostak.sdk.models.WebhookQueueResponse;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;

final class ExtractModelTest {
    @Test
    void webhookQueueResponseDeserializesEventsAliasFromLiveEventsPull() {
        String json = """
                {
                  "events": [
                    {
                      "event_id": "evt-live",
                      "firm_id": "firm-1",
                      "event": "document.received",
                      "payload": {
                        "event": "document.received",
                        "event_version": "1",
                        "timestamp": "2026-07-03T00:00:00Z",
                        "data": {}
                      },
                      "created_at": "2026-07-03T00:00:00Z"
                    }
                  ],
                  "has_more": false
                }
                """;

        WebhookQueueResponse result = new Gson().fromJson(json, WebhookQueueResponse.class);

        assertEquals("evt-live", result.items().get(0).eventId());
        assertFalse(result.hasMore());
    }

    @Test
    void extractResultDeserializesOutboundReviewFields() {
        String json = """
                {
                  "direction": "outbound",
                  "document_type": "invoice",
                  "send_payload": { "receiverName": "Odberatel s.r.o." },
                  "send_payload_missing_fields": ["receiverPeppolId"],
                  "send_ready": false,
                  "extraction": { "invoiceNumber": "FAK-001" },
                  "confidence": "high",
                  "confidence_scores": { "invoice_number": 0.95 },
                  "needs_review": true,
                  "missing_fields": [{ "field": "receiverPeppolId", "blocking": true }],
                  "field_sources": { "invoice_number": { "source": "ocr", "value": "FAK-001", "confidence": 0.95 } },
                  "next_action": { "type": "review_and_send", "endpoint": "/api/v1/documents/send", "method": "POST" },
                  "file_name": "invoice.pdf"
                }
                """;

        ExtractResult result = new Gson().fromJson(json, ExtractResult.class);

        assertEquals("outbound", result.direction());
        assertEquals("invoice", result.documentType());
        assertEquals("receiverPeppolId", result.sendPayloadMissingFields().get(0));
        assertFalse(result.sendReady());
        assertEquals("receiverPeppolId", result.missingFields().get(0).field());
        assertEquals("ocr", result.fieldSources().get("invoice_number").source());
        assertEquals("/api/v1/documents/send", result.nextAction().endpoint());
    }
}
