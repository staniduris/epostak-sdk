using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EPostak.Models;
using EPostak.Resources;
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
        var handler = new CaptureHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/api/v1/connector/webhook/test" => """{"deliveryId":"whd-1","status":"queued","event":{"id":"evt-1","customerRef":"erp-customer-1","documentId":"doc-1","type":"document.received","state":"received","occurredAt":"2026-07-15T10:00:00Z","data":{"customerRef":"erp-customer-1","direction":"inbound","type":"invoice","number":null,"response":null},"test":true}}""",
            "/api/v1/connector/webhook/deliveries" => """{"deliveries":[{"id":"whd-1","webhookId":"wh-1","eventId":"evt-1","customerRef":"erp-customer-1","type":"document.received","status":"SUCCESS","attempts":1,"responseStatus":204,"responseTimeMs":83,"lastAttemptAt":"2026-07-15T10:00:01Z","nextRetryAt":null,"createdAt":"2026-07-15T10:00:00Z"}],"nextCursor":null,"hasMore":false}""",
            "/api/v1/connector/webhook" when request.Method == HttpMethod.Put => $$"""{"webhook":{"id":"wh-1","url":"https://erp.example/epostak","events":["document.received"],"active":true,"failedAttempts":0,"createdAt":"2026-07-15T10:00:00Z","updatedAt":"2026-07-15T10:00:00Z"},"secret":"{{new string('a', 64)}}"}""",
            "/api/v1/connector/webhook" when request.Method == HttpMethod.Get => """{"webhook":{"id":"wh-1","url":"https://erp.example/epostak","events":["document.received"],"active":true,"failedAttempts":0,"createdAt":"2026-07-15T10:00:00Z","updatedAt":"2026-07-15T10:00:00Z"}}""",
            _ => "{}",
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Connector.Advanced.PreflightAsync(new ConnectorPreflightRequest
        {
            ReceiverPeppolId = "0245:1234567890",
            Document = [],
        });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("firm-123", request.Headers["X-Firm-Id"]);
    }

    [Fact]
    public async Task CustomerScopedConnectorEventsAreThePrimaryNoFirmFeed()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Connector.Customers.For("erp-customer-1").Events.ListAsync(
            new ConnectorListParams { Cursor = "cursor-1", Limit = 25 });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/connector/events?customerRef=erp-customer-1&cursor=cursor-1&limit=25", request.Uri.PathAndQuery);
        Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys);
    }

    [Fact]
    public async Task ConnectorWebhookIsGlobalAndNeverSendsFirmId()
    {
        var handler = new CaptureHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/api/v1/connector/webhook/test" => """{"deliveryId":"whd-1","status":"queued","event":{"id":"evt-1","customerRef":"erp-customer-1","documentId":"doc-1","type":"document.received","state":"received","occurredAt":"2026-07-15T10:00:00Z","data":{"customerRef":"erp-customer-1","direction":"inbound","type":"invoice","number":null,"response":null},"test":true}}""",
            "/api/v1/connector/webhook/deliveries" => """{"deliveries":[{"id":"whd-1","webhookId":"wh-1","eventId":"evt-1","customerRef":"erp-customer-1","type":"document.received","status":"SUCCESS","attempts":1,"responseStatus":204,"responseTimeMs":83,"lastAttemptAt":"2026-07-15T10:00:01Z","nextRetryAt":null,"createdAt":"2026-07-15T10:00:00Z"}],"nextCursor":null,"hasMore":false}""",
            "/api/v1/connector/webhook" when request.Method == HttpMethod.Put => $$"""{"webhook":{"id":"wh-1","url":"https://erp.example/epostak","events":["document.received"],"active":true,"failedAttempts":0,"createdAt":"2026-07-15T10:00:00Z","updatedAt":"2026-07-15T10:00:00Z"},"secret":"{{new string('a', 64)}}"}""",
            "/api/v1/connector/webhook" when request.Method == HttpMethod.Get => """{"webhook":{"id":"wh-1","url":"https://erp.example/epostak","events":["document.received"],"active":true,"failedAttempts":0,"createdAt":"2026-07-15T10:00:00Z","updatedAt":"2026-07-15T10:00:00Z"}}""",
            _ => "{}",
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var current = await client.Connector.Webhook.GetAsync();
        var configured = await client.Connector.Webhook.ConfigureAsync(
            " https://erp.example/epostak ",
            ["document.received"]);
        await client.Connector.Webhook.RotateSecretAsync();
        var test = await client.Connector.Webhook.TestAsync("\u00A0\uFEFFerp-customer-1\uFEFF\u00A0");
        var deliveries = await client.Connector.Webhook.DeliveriesAsync("next", 25, "FAILED");
        await client.Connector.Webhook.DeleteAsync();

        Assert.Equal(
            new[]
            {
                (HttpMethod.Get, "/api/v1/connector/webhook"),
                (HttpMethod.Put, "/api/v1/connector/webhook"),
                (HttpMethod.Post, "/api/v1/connector/webhook/rotate-secret"),
                (HttpMethod.Post, "/api/v1/connector/webhook/test"),
                (HttpMethod.Get, "/api/v1/connector/webhook/deliveries?cursor=next&limit=25&status=FAILED"),
                (HttpMethod.Delete, "/api/v1/connector/webhook"),
            },
            handler.ApiRequests.Select(request => (request.Method, request.Uri.PathAndQuery)));
        Assert.All(handler.ApiRequests, request => Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys));
        Assert.Contains("https://erp.example/epostak", handler.ApiRequests[1].Body);
        Assert.Contains("\"customerRef\":\"erp-customer-1\"", handler.ApiRequests[3].Body);
        Assert.Equal("wh-1", current.Webhook?.Id);
        Assert.Equal(new string('a', 64), configured.Secret);
        Assert.Equal("whd-1", test.DeliveryId);
        Assert.True(test.Event.Test);
        Assert.Equal("erp-customer-1", test.Event.CustomerRef);
        Assert.True(test.Event.Data.ContainsKey("response"));
        Assert.Null(test.Event.Data["response"]);
        Assert.Single(deliveries.Deliveries);
        Assert.Equal("erp-customer-1", deliveries.Deliveries[0].CustomerRef);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Connector.Webhook.ConfigureAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Connector.Webhook.TestAsync(" "));
    }

    [Fact]
    public async Task AdvancedLegacyEventsWithoutCustomerRefKeepFirmScope()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Connector.Advanced.EventsAsync(new ConnectorListParams { Limit = 25 });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/connector/events?limit=25", request.Uri.PathAndQuery);
        Assert.Equal("firm-123", request.Headers["X-Firm-Id"]);
    }

    [Fact]
    public async Task ConnectorAdvancedSurfaceOwnsLegacyPaths()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Connector.Advanced.PreflightAsync(new ConnectorPreflightRequest());
        await client.Connector.Advanced.SendAsync(new ConnectorSendRequest(), "send-key");
        await client.Connector.Advanced.StatusAsync("doc-1");
        await client.Connector.Advanced.InboxAsync();
        await client.Connector.Advanced.GetInboxDocumentAsync("doc-1");
        await client.Connector.Advanced.AckAsync("doc-1");
        await client.Connector.Advanced.ListOutboxAsync();
        await client.Connector.Advanced.GetOutboxItemAsync("out-1");
        await client.Connector.Advanced.SendOutboxItemAsync("out-1");
        await client.Connector.Advanced.SendOutboxBatchAsync();
        await client.Connector.Advanced.CancelOutboxItemAsync("out-1");

        Assert.Equal(
            new[]
            {
                "/api/v1/connector/preflight",
                "/api/v1/connector/send",
                "/api/v1/connector/status/doc-1",
                "/api/v1/connector/inbox",
                "/api/v1/connector/inbox/doc-1",
                "/api/v1/connector/inbox/doc-1/ack",
                "/api/v1/connector/outbox",
                "/api/v1/connector/outbox/out-1",
                "/api/v1/connector/outbox/out-1/send",
                "/api/v1/connector/outbox/send",
                "/api/v1/connector/outbox/out-1",
            },
            handler.ApiRequests.Select(request => request.Uri.AbsolutePath));
        Assert.All(handler.ApiRequests, request => Assert.Equal("firm-123", request.Headers["X-Firm-Id"]));
        Assert.Equal("send-key", handler.ApiRequests[1].Headers["Idempotency-Key"]);
    }

    [Fact]
    public void ConnectorCompatibilityAliasesStaySilentAndCustomersCannotBeCreated()
    {
        var preflight = typeof(ConnectorResource).GetMethod(nameof(ConnectorResource.PreflightAsync));
        Assert.NotNull(preflight);

        var customerMethods = typeof(ConnectorCustomersResource)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();
        Assert.Equal(new[] { "For" }, customerMethods);

        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        Assert.Throws<ArgumentException>(() => client.Connector.Customers.For(" "));
        Assert.Empty(handler.ApiRequests);
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
    public async Task CustomerScopedConnectorSubmitDocumentOmitsFirmIdAndDefaultsToSend()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        await client.Connector.Customers.For("erp-customer-1").Documents.SendAsync(
            new ConnectorBusinessDocumentRequest
            {
                ExternalId = "FA-1",
                Type = "invoice",
                Number = "FA-1",
                Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
                Lines = [new ConnectorBusinessLine { Description = "Licence", Quantity = 1, UnitPrice = 100, VatRate = 23 }],
            });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/connector/documents", request.Uri.AbsolutePath);
        Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys);
        Assert.Equal(
            "connector:v1:f7be06badbccd0670a25e6df7fd654fd45ae7291d5f5043257806adc0b107045",
            request.Headers["Idempotency-Key"]);
        Assert.Equal(77, request.Headers["Idempotency-Key"].Length);
        Assert.Contains("\"customerRef\":\"erp-customer-1\"", request.Body);
        Assert.Contains("\"delivery\":\"send\"", request.Body);
        Assert.DoesNotContain("peppol", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerStageFilteredListAndInvoiceResponseUseCanonicalWireContract()
    {
        const string responseJson = """{"id":"doc-in-1","customerRef":"erp-customer-1","response":{"status":"accepted","direction":"sent","delivery":"queued","respondedAt":"2026-07-15T12:00:00Z"},"idempotent":true}""";
        var handler = new SequenceResponseHandler([
            new(HttpStatusCode.Created, """{"id":"doc-stage-1","state":"queued"}"""),
            new(HttpStatusCode.OK, """{"documents":[],"nextCursor":"cur-2","hasMore":true}"""),
            new(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"temporary","message":"retry"}}""", RetryAfter: 0),
            new(HttpStatusCode.OK, responseJson),
        ]);
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var documents = client.Connector.Customers.For("erp-customer-1").Documents;

        await Assert.ThrowsAsync<ArgumentException>(() => documents.RespondAsync(
            "doc-in-1",
            "another-customer",
            new ConnectorInvoiceResponseRequest { Status = "accepted" }));
        Assert.Empty(handler.ApiRequests);

        await documents.StageAsync(
            new ConnectorBusinessDocumentRequest
            {
                ExternalId = "FA-STAGE-1",
                Type = "invoice",
                Number = "FA-STAGE-1",
                Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
                Lines = [new ConnectorBusinessLine { Description = "Licence", Quantity = 1, UnitPrice = 100, VatRate = 23 }],
            },
            "connector-stage-key");
        var page = await documents.ListAsync(new ConnectorBusinessDocumentListParams
        {
            Direction = "inbound",
            State = "received",
            Type = "invoice",
            CreatedAfter = "2026-07-01T00:00:00Z",
            Cursor = "cur-1",
            Limit = 25,
        });
        var result = await documents.RespondAsync(
            "doc-in-1",
            new ConnectorInvoiceResponseRequest
            {
                Status = "accepted",
                Note = "Imported into ERP",
            });

        Assert.Equal(
            new[]
            {
                (HttpMethod.Post, "/api/v1/connector/documents"),
                (HttpMethod.Get, "/api/v1/connector/documents?customerRef=erp-customer-1&direction=inbound&state=received&type=invoice&createdAfter=2026-07-01T00%3A00%3A00Z&cursor=cur-1&limit=25"),
                (HttpMethod.Post, "/api/v1/connector/documents/doc-in-1/respond?customerRef=erp-customer-1"),
                (HttpMethod.Post, "/api/v1/connector/documents/doc-in-1/respond?customerRef=erp-customer-1"),
            },
            handler.ApiRequests.Select(request => (request.Method, request.Uri.PathAndQuery)));
        Assert.All(handler.ApiRequests, request => Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys));
        Assert.Equal("connector-stage-key", handler.ApiRequests[0].Headers["Idempotency-Key"]);
        Assert.Contains("\"customerRef\":\"erp-customer-1\"", handler.ApiRequests[0].Body);
        Assert.Contains("\"delivery\":\"stage\"", handler.ApiRequests[0].Body);
        Assert.DoesNotContain("Idempotency-Key", handler.ApiRequests[2].Headers.Keys);
        Assert.DoesNotContain("Idempotency-Key", handler.ApiRequests[3].Headers.Keys);
        Assert.Equal(handler.ApiRequests[2].Body, handler.ApiRequests[3].Body);
        Assert.Equal("""{"status":"accepted","note":"Imported into ERP"}""", handler.ApiRequests[2].Body);
        Assert.Equal("cur-2", page.NextCursor);
        Assert.True(page.HasMore);
        Assert.Equal("accepted", result.Response.Status);
        Assert.Equal("sent", result.Response.Direction);
        Assert.Equal("queued", result.Response.Delivery);
        Assert.True(result.Idempotent);
    }

    [Fact]
    public async Task CustomerSubmitSnapshotsRequestWithoutMutatingOrCrossLeakingConcurrentScopes()
    {
        var handler = new BlockingTokenCaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var shared = new ConnectorBusinessDocumentRequest
        {
            ExternalId = "FA-shared",
            Number = "FA-shared",
            Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
            Lines = [new ConnectorBusinessLine { Description = "Licence", Quantity = 1, UnitPrice = 100, VatRate = 23 }],
        };

        var send = client.Connector.Customers.For("customer-send").Documents.SendAsync(shared);
        await handler.TokenRequested;
        var stage = client.Connector.Customers.For("customer-stage").Documents.StageAsync(shared);

        Assert.Null(shared.CustomerRef);
        Assert.Null(shared.Delivery);

        handler.ReleaseToken();
        await Task.WhenAll(send, stage);

        Assert.Null(shared.CustomerRef);
        Assert.Null(shared.Delivery);
        Assert.Equal(2, handler.ApiRequests.Count);
        Assert.Contains(handler.ApiRequests, request =>
            request.Body.Contains("\"customerRef\":\"customer-send\"", StringComparison.Ordinal) &&
            request.Body.Contains("\"delivery\":\"send\"", StringComparison.Ordinal));
        Assert.Contains(handler.ApiRequests, request =>
            request.Body.Contains("\"customerRef\":\"customer-stage\"", StringComparison.Ordinal) &&
            request.Body.Contains("\"delivery\":\"stage\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CustomerSendAndStageOmitEmptyPrepaymentsButPreserveNonEmptyValues()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var documents = client.Connector.Customers.For("erp-customer-1").Documents;

        var emptySend = BusinessRequest("FA-empty-send");
        var emptyStage = BusinessRequest("FA-empty-stage");
        var populatedSend = BusinessRequest("FA-populated-send");
        var populatedStage = BusinessRequest("FA-populated-stage");
        populatedSend.Prepayments.Add(new ConnectorBusinessPrepayment
        {
            AdvanceInvoiceRef = "ADV-SEND",
            AmountWithVat = 25,
        });
        populatedStage.Prepayments.Add(new ConnectorBusinessPrepayment
        {
            AdvanceInvoiceRef = "ADV-STAGE",
            AmountWithVat = 50,
        });

        await documents.SendAsync(emptySend);
        await documents.StageAsync(emptyStage);
        await documents.SendAsync(populatedSend);
        await documents.StageAsync(populatedStage);

        Assert.Equal(4, handler.ApiRequests.Count);
        using var emptySendJson = JsonDocument.Parse(handler.ApiRequests[0].Body);
        using var emptyStageJson = JsonDocument.Parse(handler.ApiRequests[1].Body);
        using var populatedSendJson = JsonDocument.Parse(handler.ApiRequests[2].Body);
        using var populatedStageJson = JsonDocument.Parse(handler.ApiRequests[3].Body);

        Assert.False(emptySendJson.RootElement.TryGetProperty("prepayments", out _));
        Assert.False(emptyStageJson.RootElement.TryGetProperty("prepayments", out _));
        Assert.Equal("ADV-SEND", populatedSendJson.RootElement
            .GetProperty("prepayments")[0]
            .GetProperty("advanceInvoiceRef")
            .GetString());
        Assert.Equal("ADV-STAGE", populatedStageJson.RootElement
            .GetProperty("prepayments")[0]
            .GetProperty("advanceInvoiceRef")
            .GetString());
        Assert.Empty(emptySend.Prepayments);
        Assert.Empty(emptyStage.Prepayments);
        Assert.Single(populatedSend.Prepayments);
        Assert.Single(populatedStage.Prepayments);
    }

    [Fact]
    public async Task CompatibilitySubmitAliasKeepsAutopilotStageSemanticsWithoutMutation()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var request = new ConnectorSubmitDocumentRequest
        {
            ExternalId = "legacy-1",
            Payload = new Dictionary<string, object?> { ["invoiceNumber"] = "FA-1" },
        };

        await client.Connector.Customers.For("erp-customer-1").SubmitDocumentAsync(request);

        var sent = Assert.Single(handler.ApiRequests);
        Assert.Equal("/api/v1/connector/autopilot", sent.Uri.AbsolutePath);
        Assert.Contains("\"customerRef\":\"erp-customer-1\"", sent.Body);
        Assert.Contains("\"mode\":\"stage\"", sent.Body);
        Assert.Null(request.CustomerRef);
        Assert.Null(request.Mode);
        Assert.NotNull(typeof(ConnectorCustomerResource)
            .GetMethod(nameof(ConnectorCustomerResource.SubmitDocumentAsync)));
    }

    [Fact]
    public async Task CompatibilityCustomerAdvancedHelpersDoNotMutateOrCrossLeakCallerInputs()
    {
        var handler = new BlockingTokenCaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var shared = new ConnectorAutopilotRequest
        {
            Mode = "shadow",
            Payload = new Dictionary<string, object?> { ["invoiceNumber"] = "FA-1" },
        };

        var first = client.Connector.Customers.For("customer-a").AutopilotAsync(shared);
        await handler.TokenRequested;
        var second = client.Connector.Customers.For("customer-b").AutopilotAsync(shared);
        handler.ReleaseToken();
        await Task.WhenAll(first, second);

        var mapper = new ConnectorMapperRequest { SourceType = "json", SourceJson = [] };
        var sync = new ConnectorSyncParams { Cursor = "cur-1" };
        await client.Connector.Customers.For("customer-a").MapperAsync(mapper);
        await client.Connector.Customers.For("customer-a").SyncAsync(sync);

        Assert.Equal("", shared.CustomerRef);
        Assert.Null(mapper.CustomerRef);
        Assert.Null(sync.CustomerRef);
        Assert.Contains(handler.ApiRequests, request =>
            request.Body.Contains("\"customerRef\":\"customer-a\"", StringComparison.Ordinal));
        Assert.Contains(handler.ApiRequests, request =>
            request.Body.Contains("\"customerRef\":\"customer-b\"", StringComparison.Ordinal));
        Assert.Contains(handler.ApiRequests, request =>
            request.Body.Contains("\"execute\":\"preview\"", StringComparison.Ordinal));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Connector.Customers.For("customer-a").Advanced.MapperAsync(
                new ConnectorMapperRequest { Execute = "send" }));
    }

    [Fact]
    public async Task CanonicalConnectorEventsDeserializeStateWhileLegacyTechnicalEventKeepsStatus()
    {
        var handler = new CaptureHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath != "/api/v1/connector/events") return "{}";
            return request.RequestUri.Query.Contains("customerRef=", StringComparison.Ordinal)
                ? """{"events":[{"id":"evt-1","customerRef":"erp-customer-1","documentId":"11111111-1111-1111-1111-111111111111","type":"document.cancelled","state":"cancelled","occurredAt":"2026-07-14T10:00:00Z","data":{"customerRef":"erp-customer-1","direction":"outbound","type":"invoice","number":null,"response":null}}],"nextCursor":null,"hasMore":false}"""
                : """{"events":[{"id":"legacy-1","documentId":"11111111-1111-1111-1111-111111111111","type":"delivery.updated","status":"DELIVERED","occurredAt":"2026-07-14T10:00:00Z","data":{"transport":"as4"}}],"nextCursor":null,"hasMore":false}""";
        });
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var page = await client.Connector.Customers.For("erp-customer-1").Events.ListAsync();

        var businessEvent = Assert.Single(page.Events);
        Assert.Equal("document.cancelled", businessEvent.Type);
        Assert.Equal("cancelled", businessEvent.State);
        Assert.Equal("erp-customer-1", businessEvent.CustomerRef);
        Assert.Equal("erp-customer-1", businessEvent.Data["customerRef"]?.ToString());
        Assert.True(businessEvent.Data.ContainsKey("number"));
        Assert.Null(businessEvent.Data["number"]);
        Assert.True(businessEvent.Data.ContainsKey("response"));
        Assert.Null(businessEvent.Data["response"]);

        var legacyPage = await client.Connector.Advanced.EventsAsync();
        Assert.Equal("DELIVERED", Assert.Single(legacyPage.Events).Status);
    }

    [Fact]
    public async Task ConnectorBusinessModelsPreserveCanonicalRequestAndResponseFields()
    {
        var handler = new CaptureHandler(request =>
            request.RequestUri?.AbsolutePath == "/api/v1/connector/documents"
                ? """{"id":"11111111-1111-1111-1111-111111111111","customerRef":"erp-customer-1","externalId":"FA-1","direction":"outbound","type":"invoice","number":"FA-1","state":"queued","replayed":false,"currency":"EUR","amounts":{"withoutTax":100,"tax":23,"total":123,"due":73},"sender":{"name":"Sender","country":"SK","companyId":"12345678","resolution":"verified"},"recipient":{"name":"Buyer","country":"SK","taxId":"2120123456","resolution":"verified"},"issueDate":"2026-07-14","dueDate":"2026-07-28","processedAt":"2026-07-14T10:00:00Z","processedReference":"ERP-OK","createdAt":"2026-07-14T09:00:00Z","updatedAt":"2026-07-14T10:00:00Z","response":{"status":"accepted","direction":"sent","reason":"Approved","respondedAt":"2026-07-15T12:00:00Z"},"links":{"self":"/connector/documents/1"}}"""
                : "{}");
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var request = new ConnectorBusinessDocumentRequest
        {
            ExternalId = "FA-1",
            Number = "FA-1",
            BuyerReference = "PO-7",
            PrepaidAmount = 50,
            Recipient = new ConnectorBusinessRecipient
            {
                Country = "SK",
                TaxId = "2120123456",
                Address = new ConnectorBusinessAddress { Street = "Hlavna 1", City = "Bratislava", PostalCode = "81101" },
            },
            Prepayments = [new ConnectorBusinessPrepayment { AdvanceInvoiceRef = "ADV-1", AmountWithVat = 50 }],
            Lines = [new ConnectorBusinessLine
            {
                Description = "Licence",
                Quantity = 1,
                UnitPrice = 100,
                VatRate = 23,
                Discount = 5,
                DeliveryDate = "2026-07-14",
                CustomsTariffCode = "9983",
            }],
            Attachments = [new ConnectorBusinessAttachment { FileName = "terms.pdf", MimeType = "application/pdf", Content = "YQ==" }],
        };

        var result = await client.Connector.Customers.For("erp-customer-1").Documents.SendAsync(request);

        var sent = Assert.Single(handler.ApiRequests);
        Assert.Contains("\"buyerReference\":\"PO-7\"", sent.Body);
        Assert.Contains("\"prepayments\":[", sent.Body);
        Assert.Contains("\"address\":{", sent.Body);
        Assert.Contains("\"discount\":5", sent.Body);
        Assert.Contains("\"deliveryDate\":\"2026-07-14\"", sent.Body);
        Assert.Contains("\"attachments\":[", sent.Body);
        Assert.Equal(73, result.Amounts?.Due);
        Assert.Equal("Sender", result.Sender?.Name);
        Assert.Equal("2026-07-14", result.IssueDate);
        Assert.Equal("ERP-OK", result.ProcessedReference);
        Assert.Equal("accepted", result.Response?.Status);
        Assert.Equal("sent", result.Response?.Direction);
        Assert.Equal("Approved", result.Response?.Reason);
        Assert.Equal("2026-07-15T12:00:00Z", result.Response?.RespondedAt);
    }

    [Fact]
    public async Task AdvancedDocumentArtifactsHaveCanonicalCustomerHomeAndCompatibilityAliases()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var customer = client.Connector.Customers.For("erp-customer-1");

        await customer.Advanced.Documents.UblAsync("doc-1");
        await client.Connector.Advanced.Documents.EvidenceAsync("doc-1");

        Assert.Equal(
            new[]
            {
                "/api/v1/connector/documents/doc-1/ubl",
                "/api/v1/connector/documents/doc-1/evidence",
            },
            handler.ApiRequests.Select(request => request.Uri.AbsolutePath));
        Assert.NotNull(typeof(ConnectorCustomerDocumentsResource)
            .GetMethod(nameof(ConnectorCustomerDocumentsResource.UblAsync)));
    }

    [Fact]
    public async Task DefaultIdempotencyKeySeparatesDelimiterCollisionPairs()
    {
        var first = await SubmitConnectorDocumentAsync("a:b", "c");
        var second = await SubmitConnectorDocumentAsync("a", "b:c");

        Assert.Equal(
            "connector:v1:540e8f1c5ae653a7d7e2fe88f7eb8dcabea924d661b1542ad191bb1848e0c33d",
            first.Headers["Idempotency-Key"]);
        Assert.Equal(
            "connector:v1:e482a79a788392ccae4952360dd438820641e4c162b4952b42d35e78260d70be",
            second.Headers["Idempotency-Key"]);
        Assert.NotEqual(first.Headers["Idempotency-Key"], second.Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task DefaultIdempotencyKeySupportsMaximumInputLengths()
    {
        var request = await SubmitConnectorDocumentAsync(new string('c', 255), new string('e', 255));

        Assert.Equal(
            "connector:v1:7182fd43682e0689adf34c908bc3ec162aaf1687c167fdbff714ff43daa4b111",
            request.Headers["Idempotency-Key"]);
        Assert.Equal(77, request.Headers["Idempotency-Key"].Length);
    }

    [Fact]
    public async Task DefaultIdempotencyKeyNormalizesWhitespaceAndPreservesUnicode()
    {
        var request = await SubmitConnectorDocumentAsync("\u00A0\uFEFFzákazník😀\uFEFF\u00A0", "\uFEFFFA-žltý-1\u00A0");

        Assert.Equal(
            "connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd",
            request.Headers["Idempotency-Key"]);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("zákazník😀", body.RootElement.GetProperty("customerRef").GetString());
        Assert.Equal("FA-žltý-1", body.RootElement.GetProperty("externalId").GetString());
    }

    [Fact]
    public async Task DefaultIdempotencyKeyDoesNotTrimC1NextLineControl()
    {
        var request = await SubmitConnectorDocumentAsync("\u0085zákazník😀\u0085", "\u0085FA-žltý-1\u0085");

        Assert.Equal(
            "connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080",
            request.Headers["Idempotency-Key"]);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("\u0085zákazník😀\u0085", body.RootElement.GetProperty("customerRef").GetString());
        Assert.Equal("\u0085FA-žltý-1\u0085", body.RootElement.GetProperty("externalId").GetString());
    }

    [Fact]
    public async Task ExplicitConnectorIdempotencyKeyIsPreserved()
    {
        var request = await SubmitConnectorDocumentAsync("erp-customer-1", "FA-1", "erp-retry-key");

        Assert.Equal("erp-retry-key", request.Headers["Idempotency-Key"]);

        var client = CreateClient(new HttpClient(new CaptureHandler()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Connector.Customers.For("customer").Documents.SendAsync(
                BusinessRequest("empty-key"),
                ""));
    }

    [Fact]
    public async Task ConnectorRetriesKeyedAndLifecyclePostsButNotConflict()
    {
        var body = BusinessRequest("FA-retry");
        var retryHandler = new SequenceResponseHandler(
            [
                new(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"temporary","message":"retry"}}""", RetryAfter: 0),
                new(HttpStatusCode.Created, """{"id":"doc-1","state":"queued"}"""),
            ],
            attempt =>
            {
                if (attempt == 1) body.Lines[0].Description = "Mutated";
            });
        using var retryHttp = new HttpClient(retryHandler);
        var retryClient = CreateClient(retryHttp);

        await retryClient.Connector.Customers.For("customer").Documents.SendAsync(body);

        Assert.Equal(2, retryHandler.ApiRequests.Count);
        Assert.Equal(retryHandler.ApiRequests[0].Body, retryHandler.ApiRequests[1].Body);
        Assert.Contains("Original", retryHandler.ApiRequests[0].Body);
        Assert.Equal(
            retryHandler.ApiRequests[0].Headers["Idempotency-Key"],
            retryHandler.ApiRequests[1].Headers["Idempotency-Key"]);

        var lifecycleHandler = new SequenceResponseHandler([
            new(HttpStatusCode.ServiceUnavailable, "{}", RetryAfter: 0),
            new(HttpStatusCode.OK, """{"id":"doc-1","state":"cancelled"}"""),
        ]);
        using var lifecycleHttp = new HttpClient(lifecycleHandler);
        var lifecycleClient = CreateClient(lifecycleHttp);
        await lifecycleClient.Connector.Customers.For("customer").Documents.CancelDocumentAsync("doc-1");
        Assert.Equal(2, lifecycleHandler.ApiRequests.Count);

        var conflictHandler = new SequenceResponseHandler([
            new(HttpStatusCode.Conflict, """{"error":{"code":"idempotency_in_flight","message":"busy","retryable":true}}""", RetryAfter: 0),
        ]);
        using var conflictHttp = new HttpClient(conflictHandler);
        var conflictClient = CreateClient(conflictHttp);
        var conflict = await Assert.ThrowsAsync<EPostakException>(() =>
            conflictClient.Connector.Customers.For("customer").Documents.SendAsync(BusinessRequest("FA-conflict")));
        Assert.Equal(409, conflict.Status);
        Assert.Single(conflictHandler.ApiRequests);
    }

    [Fact]
    public async Task ConnectorRetriesTransportFailureButSapiMutationSurfacesOnce()
    {
        var connectorHandler = new TransportFailureHandler(
            failuresBeforeSuccess: 1,
            successBody: """{"id":"doc-transport","state":"queued"}""");
        using var connectorHttp = new HttpClient(connectorHandler);
        var connectorClient = CreateClient(connectorHttp);

        await connectorClient.Connector.Customers.For("erp-customer-1").Documents.StageAsync(
            BusinessRequest("FA-TRANSPORT-1"),
            "connector-transport-key");

        Assert.Equal(2, connectorHandler.ApiRequests.Count);
        var first = connectorHandler.ApiRequests[0];
        var second = connectorHandler.ApiRequests[1];
        Assert.Equal(first.Method, second.Method);
        Assert.Equal(first.Uri.PathAndQuery, second.Uri.PathAndQuery);
        Assert.Equal(first.Body, second.Body);
        Assert.Equal(first.Headers["Idempotency-Key"], second.Headers["Idempotency-Key"]);
        Assert.Equal("connector-transport-key", first.Headers["Idempotency-Key"]);
        Assert.Equal("/api/v1/connector/documents", first.Uri.PathAndQuery);
        Assert.Contains("\"customerRef\":\"erp-customer-1\"", first.Body);
        Assert.Contains("\"delivery\":\"stage\"", first.Body);
        Assert.DoesNotContain("X-Firm-Id", first.Headers.Keys);
        Assert.DoesNotContain("X-Firm-Id", second.Headers.Keys);

        var sapiHandler = new TransportFailureHandler(failuresBeforeSuccess: 10, successBody: "{}");
        using var sapiHttp = new HttpClient(sapiHandler);
        var sapiClient = CreateClient(sapiHttp);
        var error = await Assert.ThrowsAsync<EPostakException>(() =>
            sapiClient.Sapi.Participants.For("0245:1234567890").Documents.SendAsync(
                new Dictionary<string, object?> { ["xml"] = "<Invoice/>" },
                "sapi-transport-key"));
        Assert.Equal(0, error.Status);
        Assert.Single(sapiHandler.ApiRequests);
    }

    [Fact]
    public async Task ConnectorRetryOptInDoesNotChangeSapiSendBehavior()
    {
        var handler = new SequenceResponseHandler([
            new(HttpStatusCode.ServiceUnavailable, """{"error":{"code":"temporary","message":"retry"}}""", RetryAfter: 0),
            new(HttpStatusCode.OK, "{}"),
        ]);
        using var http = new HttpClient(handler);
        var client = CreateClient(http);

        var error = await Assert.ThrowsAsync<EPostakException>(() =>
            client.Sapi.Participants.For("0245:1234567890").Documents.SendAsync(
                new Dictionary<string, object?> { ["xml"] = "<Invoice/>" },
                "sapi-key"));

        Assert.Equal(503, error.Status);
        Assert.Single(handler.ApiRequests);
    }

    [Fact]
    public async Task ApiErrorsPreserveBusinessRetryMetadataAndRetryAfterSeconds()
    {
        var retryHandler = new ErrorResponseHandler(
            HttpStatusCode.Conflict,
            """{"error":{"code":"idempotency_in_flight","message":"Still processing","field":"externalId","nextAction":"retry","retryable":true,"requestId":"req-body"}}""",
            new Dictionary<string, string> { ["Retry-After"] = "7", ["X-Request-Id"] = "req-header" });
        using var retryHttp = new HttpClient(retryHandler);
        var retryClient = CreateClient(retryHttp);

        var retryable = await Assert.ThrowsAsync<EPostakException>(() =>
            retryClient.Connector.Customers.For("customer").Documents.GetAsync("doc-1"));
        Assert.Equal("externalId", retryable.Field);
        Assert.Equal("retry", retryable.NextAction);
        Assert.True(retryable.Retryable);
        Assert.Equal("req-body", retryable.RequestId);
        Assert.Equal(7, retryable.RetryAfter);

        var validationHandler = new ErrorResponseHandler(
            HttpStatusCode.UnprocessableEntity,
            """{"error":{"code":"validation_failed","message":"Fix request","retryable":false}}""");
        using var validationHttp = new HttpClient(validationHandler);
        var validationClient = CreateClient(validationHttp);
        var validation = await Assert.ThrowsAsync<EPostakException>(() =>
            validationClient.Connector.Customers.For("customer").Documents.GetAsync("doc-2"));
        Assert.False(validation.Retryable);
        Assert.Null(validation.RetryAfter);
    }

    [Fact]
    public async Task CustomerDocumentsKeepLegacyGetAsyncContractAndExposeTypedBusinessDetail()
    {
        var legacyMethod = typeof(ConnectorCustomerDocumentsResource).GetMethod(
            nameof(ConnectorCustomerDocumentsResource.GetAsync),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string), typeof(CancellationToken)],
            modifiers: null);
        Assert.NotNull(legacyMethod);
        Assert.Equal(typeof(ConnectorDocumentsResource), legacyMethod.DeclaringType);
        Assert.Equal(typeof(Task<Dictionary<string, object?>>), legacyMethod.ReturnType);

        var typedMethod = typeof(ConnectorCustomerDocumentsResource).GetMethod(
            nameof(ConnectorCustomerDocumentsResource.GetBusinessDocumentAsync),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string), typeof(CancellationToken)],
            modifiers: null);
        Assert.NotNull(typedMethod);
        Assert.Equal(typeof(Task<ConnectorBusinessDocument>), typedMethod.ReturnType);

        var handler = new CaptureHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/api/v1/connector/documents/legacy-detail" => """{"id":"legacy-detail","state":"received"}""",
            "/api/v1/connector/documents/typed-detail" => """{"id":"typed-detail","state":"received"}""",
            _ => "{}",
        });
        using var http = new HttpClient(handler);
        var documents = CreateClient(http).Connector.Customers.For("customer A/1").Documents;

        // These explicit task types compile the old and new consumer contracts.
        Task<Dictionary<string, object?>> legacyCall = documents.GetAsync("legacy-detail");
        var legacy = await legacyCall;
        Task<ConnectorBusinessDocument> typedCall = documents.GetBusinessDocumentAsync("typed-detail");
        var typed = await typedCall;

        Assert.Equal("legacy-detail", legacy["id"]?.ToString());
        Assert.Equal("typed-detail", typed.Id);
        Assert.Equal(
            new[]
            {
                "/api/v1/connector/documents/legacy-detail?customerRef=customer%20A%2F1",
                "/api/v1/connector/documents/typed-detail?customerRef=customer%20A%2F1",
            },
            handler.ApiRequests.Select(request => request.Uri.PathAndQuery));
        Assert.All(handler.ApiRequests, request => Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys));
    }

    [Fact]
    public async Task CustomerScopedDocumentLifecycleSendAndCancelUseNoBody()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var documents = client.Connector.Customers.For("erp-customer-1").Documents;

        await documents.SendDocumentAsync("doc-1");
        await documents.CancelDocumentAsync("doc-2");

        Assert.Equal(
            new[]
            {
                "/api/v1/connector/documents/doc-1/send?customerRef=erp-customer-1",
                "/api/v1/connector/documents/doc-2/cancel?customerRef=erp-customer-1",
            },
            handler.ApiRequests.Select(request => request.Uri.PathAndQuery));
        Assert.All(handler.ApiRequests, request =>
        {
            Assert.Empty(request.Body);
            Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys);
        });
        await Assert.ThrowsAsync<ArgumentException>(() => documents.SendDocumentAsync(" "));
        Assert.Equal(2, handler.ApiRequests.Count);
    }

    [Fact]
    public async Task CustomerPointOperationsAndArtifactsAlwaysBindEncodedCustomerRef()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        var customer = client.Connector.Customers.For("customer A/1");
        const string documentId = "customer-b-doc";

        await customer.Documents.GetAsync(documentId);
        await customer.Documents.AcknowledgeAsync(documentId, "ERP-ACK-1");
        await customer.Documents.SendDocumentAsync(documentId);
        await customer.Documents.CancelDocumentAsync(documentId);
        await customer.Advanced.Documents.UblAsync(documentId);
        await customer.Advanced.Documents.EvidenceAsync(documentId);
        await customer.Advanced.Documents.EvidenceBundleAsync(documentId);
        await customer.Advanced.Documents.SupportPacketAsync(documentId);

        const string query = "?customerRef=customer%20A%2F1";
        Assert.Equal(
            new[]
            {
                "/api/v1/connector/documents/customer-b-doc" + query,
                "/api/v1/connector/documents/customer-b-doc/acknowledge" + query,
                "/api/v1/connector/documents/customer-b-doc/send" + query,
                "/api/v1/connector/documents/customer-b-doc/cancel" + query,
                "/api/v1/connector/documents/customer-b-doc/ubl" + query,
                "/api/v1/connector/documents/customer-b-doc/evidence" + query,
                "/api/v1/connector/documents/customer-b-doc/evidence-bundle" + query,
                "/api/v1/connector/documents/customer-b-doc/support-packet" + query,
            },
            handler.ApiRequests.Select(request => request.Uri.PathAndQuery));
        Assert.Equal("{\"reference\":\"ERP-ACK-1\"}", handler.ApiRequests[1].Body);
        Assert.Empty(handler.ApiRequests[2].Body);
        Assert.Empty(handler.ApiRequests[3].Body);
        Assert.All(handler.ApiRequests, request => Assert.DoesNotContain("X-Firm-Id", request.Headers.Keys));
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

    private static async Task<CapturedRequest> SubmitConnectorDocumentAsync(
        string customerRef,
        string externalId,
        string? idempotencyKey = null)
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = CreateClient(http);
        await client.Connector.Customers.For(customerRef).Documents.SendAsync(
            new ConnectorBusinessDocumentRequest
            {
                ExternalId = externalId,
                Number = externalId,
                Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
                Lines = [new ConnectorBusinessLine { Description = "Licence", Quantity = 1, UnitPrice = 100, VatRate = 23 }],
            },
            idempotencyKey);
        return Assert.Single(handler.ApiRequests);
    }

    private static ConnectorBusinessDocumentRequest BusinessRequest(string externalId) => new()
    {
        ExternalId = externalId,
        Number = externalId,
        Recipient = new ConnectorBusinessRecipient { Country = "SK", TaxId = "2120123456" },
        Lines = [new ConnectorBusinessLine { Description = "Original", Quantity = 1, UnitPrice = 1, VatRate = 23 }],
    };

    private static IEnumerable<Func<EPostakClient, Task>> V2Calls()
    {
        yield return client => client.Connector.Advanced.MapperAsync(new ConnectorMapperRequest
        {
            TemplateKey = "pohoda-csv-v1",
            SourceType = "csv",
            SourceText = "Doklad",
        });
        yield return client => client.Connector.Advanced.ZenInputAsync(new ConnectorZenInputRequest { CustomerRef = "cust-1" });
        yield return client => client.Connector.Advanced.AutopilotAsync(new ConnectorAutopilotRequest { CustomerRef = "cust-1" });
        yield return client => client.Connector.Advanced.GetAutopilotRunAsync("run-1");
        yield return client => client.Connector.Advanced.SendAutopilotRunAsync("run-1");
        yield return client => client.Connector.Advanced.ReconcileAsync(new ConnectorReconcileParams { Status = "exceptions" });
        yield return client => client.Connector.Advanced.MailboxesAsync();
        yield return client => client.Connector.Advanced.RepairMailboxAsync(new ConnectorMailboxRepairRequest { CustomerRef = "cust-1" });
        yield return client => client.Connector.Advanced.UpdateMailboxSendPolicyAsync("cust-1", new ConnectorSendPolicyOptions { Policy = "manual" });
        yield return client => client.Connector.Advanced.SyncAsync(new ConnectorSyncParams { CustomerRef = "cust-1" });
        yield return client => client.Connector.GetDocumentAsync("doc-1");
        yield return client => client.Connector.GetDocumentUblAsync("doc-1");
        yield return client => client.Connector.GetDocumentEvidenceAsync("doc-1");
        yield return client => client.Connector.GetDocumentEvidenceBundleAsync("doc-1");
        yield return client => client.Connector.Advanced.RunActionAsync("action-1", new ConnectorActionRequest { Note = "approve" });
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string>? _apiResponse;

        public CaptureHandler(Func<HttpRequestMessage, string>? apiResponse = null) => _apiResponse = apiResponse;

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

            lock (ApiRequests)
            {
                ApiRequests.Add(new CapturedRequest(
                    request.Method,
                    request.RequestUri!,
                    request.Headers.ToDictionary(h => h.Key, h => h.Value.Single()),
                    body));
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/ubl", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<xml/>", Encoding.UTF8, "application/xml"),
                };
            }

            return Json(_apiResponse?.Invoke(request) ?? "{}");
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }

    private sealed class BlockingTokenCaptureHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _tokenRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseToken = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TokenRequested => _tokenRequested.Task;
        public List<CapturedRequest> ApiRequests { get; } = [];

        public void ReleaseToken() => _releaseToken.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
            {
                _tokenRequested.TrySetResult();
                await _releaseToken.Task.WaitAsync(cancellationToken);
                return Json("""{"access_token":"token","refresh_token":"refresh","expires_in":3600}""");
            }

            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (ApiRequests)
            {
                ApiRequests.Add(new CapturedRequest(
                    request.Method,
                    request.RequestUri!,
                    request.Headers.ToDictionary(h => h.Key, h => h.Value.Single()),
                    body));
            }
            return Json("{}");
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }

    private sealed class ErrorResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly IReadOnlyDictionary<string, string> _headers;

        public ErrorResponseHandler(
            HttpStatusCode status,
            string body,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            _status = status;
            _body = body;
            _headers = headers ?? new Dictionary<string, string>();
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"token","refresh_token":"refresh","expires_in":3600}""",
                        Encoding.UTF8,
                        "application/json"),
                });
            }

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
            foreach (var (name, value) in _headers)
                response.Headers.TryAddWithoutValidation(name, value);
            return Task.FromResult(response);
        }
    }

    private sealed record ResponseSpec(HttpStatusCode Status, string Body, int? RetryAfter = null);

    private sealed class TransportFailureHandler : HttpMessageHandler
    {
        private int _failuresRemaining;
        private readonly string _successBody;

        public TransportFailureHandler(int failuresBeforeSuccess, string successBody)
        {
            _failuresRemaining = failuresBeforeSuccess;
            _successBody = successBody;
        }

        public List<CapturedRequest> ApiRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
            {
                return Json("""{"access_token":"token","refresh_token":"refresh","expires_in":3600}""");
            }

            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ApiRequests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(header => header.Key, header => header.Value.Single()),
                body));

            if (_failuresRemaining > 0)
            {
                _failuresRemaining -= 1;
                throw new HttpRequestException("socket reset");
            }

            return Json(_successBody);
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }

    private sealed class SequenceResponseHandler : HttpMessageHandler
    {
        private readonly Queue<ResponseSpec> _responses;
        private readonly Action<int>? _onAttempt;

        public SequenceResponseHandler(IEnumerable<ResponseSpec> responses, Action<int>? onAttempt = null)
        {
            _responses = new Queue<ResponseSpec>(responses);
            _onAttempt = onAttempt;
        }

        public List<CapturedRequest> ApiRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"token","refresh_token":"refresh","expires_in":3600}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ApiRequests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(header => header.Key, header => header.Value.Single()),
                body));
            _onAttempt?.Invoke(ApiRequests.Count);

            var spec = _responses.Dequeue();
            var response = new HttpResponseMessage(spec.Status)
            {
                Content = new StringContent(spec.Body, Encoding.UTF8, "application/json"),
            };
            if (spec.RetryAfter is { } seconds)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            return response;
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        Dictionary<string, string> Headers,
        string Body);
}
