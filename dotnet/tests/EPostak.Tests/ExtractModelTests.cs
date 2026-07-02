using System.Text.Json;
using EPostak.Models;
using Xunit;

namespace EPostak.Tests;

public sealed class ExtractModelTests
{
    [Fact]
    public void WebhookQueueResponseDeserializesEventsAliasFromLiveEventsPull()
    {
        const string json = """
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

        var result = JsonSerializer.Deserialize<WebhookQueueResponse>(json)!;

        Assert.Equal("evt-live", Assert.Single(result.Items).EventId);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void ExtractResultDeserializesOutboundReviewFields()
    {
        const string json = """
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

        var result = JsonSerializer.Deserialize<ExtractResult>(json)!;

        Assert.Equal("outbound", result.Direction);
        Assert.Equal("invoice", result.DocumentType);
        Assert.Equal("receiverPeppolId", Assert.Single(result.SendPayloadMissingFields));
        Assert.False(result.SendReady);
        Assert.Equal("receiverPeppolId", Assert.Single(result.MissingFields).Field);
        Assert.Equal("ocr", result.FieldSources["invoice_number"].Source);
        Assert.Equal("/api/v1/documents/send", result.NextAction?.Endpoint);
    }
}
