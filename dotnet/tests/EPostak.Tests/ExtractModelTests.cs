using System.Text.Json;
using System.Net;
using System.Text;
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
              "applied_overrides": ["vendor_dic", "iban"],
              "missing_fields": [{
                "field": "receiverPeppolId",
                "label": "Peppol prijímateľ",
                "required": true,
                "severity": "blocking",
                "reason": "missing",
                "how_to_fix": "Doplňte identifikátor prijímateľa."
              }],
              "field_sources": { "invoice_number": { "source": "ocr", "value": "FAK-001", "confidence": 0.95 } },
              "next_action": { "type": "review_and_send", "endpoint": "/api/v1/documents/send", "method": "POST", "fields": ["receiverPeppolId"] },
              "file_name": "invoice.pdf"
            }
            """;

        var result = JsonSerializer.Deserialize<ExtractResult>(json)!;

        Assert.Equal("outbound", result.Direction);
        Assert.Equal("invoice", result.DocumentType);
        Assert.Equal("receiverPeppolId", Assert.Single(result.SendPayloadMissingFields));
        Assert.False(result.SendReady);
        Assert.Equal(["vendor_dic", "iban"], result.AppliedOverrides);
        Assert.Equal("receiverPeppolId", Assert.Single(result.MissingFields).Field);
        Assert.Equal("blocking", Assert.Single(result.MissingFields).Severity);
        Assert.Equal("Doplňte identifikátor prijímateľa.", Assert.Single(result.MissingFields).HowToFix);
        Assert.Equal("ocr", result.FieldSources["invoice_number"].Source);
        Assert.Equal("/api/v1/documents/send", result.NextAction?.Endpoint);
        Assert.Equal(["receiverPeppolId"], result.NextAction?.Fields);
    }

    [Fact]
    public async Task PayloadExtractSendsCorrectedFieldsAsMultipartJson()
    {
        var handler = new ExtractRequestHandler();
        using var http = new HttpClient(handler);
        using var client = new EPostakClient(new EPostakConfig
        {
            ClientId = "sk_int_test",
            ClientSecret = "sk_int_test",
            BaseUrl = "https://example.test/api/v1",
        }, http);
        await using var stream = new MemoryStream("pdf"u8.ToArray());

        var result = await client.Payloads.ExtractWithFieldsAsync(
            stream,
            "application/pdf",
            new Dictionary<string, object?>
            {
                ["vendor_dic"] = "2020123456",
                ["iban"] = "SK6807200002891987426353",
            },
            "invoice.pdf");

        Assert.Contains("name=fields", handler.Body);
        Assert.Contains("\"vendor_dic\":\"2020123456\"", handler.Body);
        Assert.Equal(["vendor_dic", "iban"], result.AppliedOverrides);
    }

    private sealed class ExtractRequestHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
            {
                return Json("""{"access_token":"token","refresh_token":"refresh","expires_in":3600}""");
            }

            Body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return Json("""{"extraction":{},"confidence":"high","confidence_scores":{},"needs_review":true,"applied_overrides":["vendor_dic","iban"],"file_name":"invoice.pdf"}""");
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }
}
