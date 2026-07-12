using System.Net;
using System.Text;
using EPostak.Models;
using Xunit;

namespace EPostak.Tests;

public sealed class ConnectorFirmScopeTests
{
    [Fact]
    public async Task ConnectorV2EndpointsDoNotSendGlobalFirmId()
    {
        foreach (var call in V2Calls())
        {
            var handler = new CaptureHandler();
            using var http = new HttpClient(handler);
            var client = CreateClient(http);

            await call.Invoke(client);

            var request = Assert.Single(handler.ApiRequests);
            Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys);
        }
    }

    [Fact]
    public async Task LegacyConnectorEndpointsStillSendGlobalFirmId()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Connector.PreflightAsync(new ConnectorPreflightRequest
        {
            ReceiverPeppolId = "0245:1234567890",
            Document = [],
        });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("firm-123", request.Headers["X-Firm-Id"]);
    }

    [Fact]
    public void MajorReleaseEnterpriseNamespaceExposesFullPlatformResources()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        Assert.Same(client.Documents, client.Enterprise.Documents);
        Assert.Same(client.Documents.Inbox, client.Enterprise.Inbox);
        Assert.Same(client.Inbound, client.Enterprise.Pull.Inbound);
        Assert.Same(client.Outbound, client.Enterprise.Pull.Outbound);
        Assert.Same(client.Connector, client.Enterprise.Connector);
        Assert.Same(client.Box, client.Enterprise.Box);
        Assert.Same(client.Webhooks, client.Enterprise.Webhooks);
    }

    [Fact]
    public async Task BoxResourceUsesPublicBoxPaths()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Box.ListAsync(new BoxListParams
        {
            Status = "ready",
            Direction = "outbound",
            Limit = 10,
            Offset = 5,
        });
        await client.Box.CreateAsync(new BoxCreateRequest
        {
            PayloadXml = "<Invoice/>",
            ScheduledFor = "2026-07-01T00:00:00.000Z",
            ExternalId = "erp-doc-1",
            Metadata = new Dictionary<string, object?> { ["source"] = "sdk-test" },
        });
        await client.Box.GetAsync("box-1");
        await client.Box.ScheduleAsync("box-1", new BoxScheduleRequest { ScheduledFor = "2026-07-01T00:00:00.000Z" });
        await client.Box.SendNowAsync("box-1");
        await client.Box.RetryAsync("box-1");
        await client.Box.CancelAsync("box-1");

        var paths = handler.ApiRequests.Select(request => (request.Method.Method, request.Uri.PathAndQuery)).ToArray();
        Assert.Equal(
            new[]
            {
                ("GET", "/api/v1/box/items?status=ready&direction=outbound&limit=10&offset=5"),
                ("POST", "/api/v1/box/items"),
                ("GET", "/api/v1/box/items/box-1"),
                ("POST", "/api/v1/box/items/box-1/schedule"),
                ("POST", "/api/v1/box/items/box-1/send-now"),
                ("POST", "/api/v1/box/items/box-1/retry"),
                ("POST", "/api/v1/box/items/box-1/cancel"),
            },
            paths);
        Assert.Contains("\"payloadXml\":\"\\u003CInvoice/\\u003E\"", handler.ApiRequests[1].Body);
        Assert.Contains("\"externalId\":\"erp-doc-1\"", handler.ApiRequests[1].Body);
        Assert.Contains("\"scheduledFor\":\"2026-07-01T00:00:00.000Z\"", handler.ApiRequests[3].Body);
    }

    [Fact]
    public async Task CustomerScopedConnectorSubmitDocumentOmitsFirmIdAndDefaultsToStage()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Enterprise.Connector.Customers.For("erp-customer-1").SubmitDocumentAsync(
            new ConnectorSubmitDocumentRequest
            {
                ExternalId = "FA-1",
                IdempotencyKey = "erp-fa-1",
                Payload = new ConnectorSendRequest { Document = new Dictionary<string, object?> { ["invoiceNumber"] = "FA-1" } },
            });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/connector/autopilot", request.Uri.AbsolutePath);
        Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys);
        Assert.Contains("\"customerRef\":\"erp-customer-1\"", request.Body);
        Assert.Contains("\"mode\":\"stage\"", request.Body);
    }

    [Fact]
    public async Task SapiParticipantDocumentsSendUsesSapiBaseAndParticipantHeader()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Sapi.Participants.For("0245:1234567890").Documents.SendAsync(
            new Dictionary<string, object?> { ["xml"] = "<Invoice/>" },
            "sapi-fa-1");

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/sapi/v1/document/send", request.Uri.AbsolutePath);
        Assert.Equal("0245:1234567890", request.Headers["X-Peppol-Participant-Id"]);
        Assert.Equal("sapi-fa-1", request.Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task JsonModeDocumentsSendRequiresReceiverNameBeforeHttp()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Documents.SendAsync(new SendDocumentRequest
            {
                ReceiverPeppolId = "0245:12345678",
                Items = [new LineItem { Description = "Item", Quantity = 1, UnitPrice = 100, VatRate = 23 }]
            }));

        Assert.Contains("ReceiverName", ex.Message);
        Assert.Empty(handler.ApiRequests);
    }

    [Fact]
    public async Task XmlModeDocumentsSendDoesNotRequireReceiverName()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Documents.SendAsync(new SendDocumentRequest
        {
            ReceiverPeppolId = "0245:12345678",
            Xml = "<Invoice/>",
        });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/documents/send", request.Uri.AbsolutePath);
        Assert.DoesNotContain("receiverName", request.Body);
    }

    [Fact]
    public async Task PeppolCapabilitiesUsesParticipantEnvelope()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Peppol.CapabilitiesAsync(new CapabilitiesRequest
        {
            Scheme = "0245",
            Identifier = "2020305606",
            DocumentType = "urn:invoice",
        });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/peppol/capabilities", request.Uri.AbsolutePath);
        Assert.Contains("\"participant\"", request.Body);
        Assert.Contains("\"scheme\":\"0245\"", request.Body);
        Assert.Contains("\"identifier\":\"2020305606\"", request.Body);
        Assert.Contains("\"documentType\":\"urn:invoice\"", request.Body);
    }

    private static EPostakClient CreateClient(HttpClient http) =>
        new(new EPostakConfig
        {
            ClientId = "sk_int_test",
            ClientSecret = "sk_int_test",
            BaseUrl = "https://example.test/api/v1",
            FirmId = "firm-123",
        }, http);

    private static IEnumerable<Func<EPostakClient, Task>> V2Calls()
    {
        yield return client => client.Connector.MapperAsync(new ConnectorMapperRequest
        {
            TemplateKey = "pohoda-csv-v1",
            SourceType = "csv",
            SourceText = "Doklad",
        });
        yield return client => client.Connector.ZenInputAsync(new ConnectorZenInputRequest { CustomerRef = "cust-1" });
        yield return client => client.Connector.AutopilotAsync(new ConnectorAutopilotRequest { CustomerRef = "cust-1" });
        yield return client => client.Connector.GetAutopilotRunAsync("run-1");
        yield return client => client.Connector.SendAutopilotRunAsync("run-1");
        yield return client => client.Connector.ReconcileAsync(new ConnectorReconcileParams { Status = "exceptions" });
        yield return client => client.Connector.MailboxesAsync();
        yield return client => client.Connector.RepairMailboxAsync(new ConnectorMailboxRepairRequest { CustomerRef = "cust-1" });
        yield return client => client.Connector.UpdateMailboxSendPolicyAsync("cust-1", new ConnectorSendPolicyOptions { Policy = "manual" });
        yield return client => client.Connector.SyncAsync(new ConnectorSyncParams { CustomerRef = "cust-1" });
        yield return client => client.Connector.GetDocumentAsync("doc-1");
        yield return client => client.Connector.GetDocumentUblAsync("doc-1");
        yield return client => client.Connector.GetDocumentEvidenceAsync("doc-1");
        yield return client => client.Connector.GetDocumentEvidenceBundleAsync("doc-1");
        yield return client => client.Connector.RunActionAsync("action-1", new ConnectorActionRequest { Note = "approve" });
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<CapturedRequest> ApiRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : "";

            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
                return Json("""{"access_token":"token","refresh_token":"refresh","expires_in":3600}""");

            ApiRequests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(h => h.Key, h => h.Value.Single()),
                body));

            if (request.RequestUri?.AbsolutePath.EndsWith("/ubl", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<xml/>", Encoding.UTF8, "application/xml"),
                };
            }

            return Json("{}");
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        Dictionary<string, string> Headers,
        string Body);
}
