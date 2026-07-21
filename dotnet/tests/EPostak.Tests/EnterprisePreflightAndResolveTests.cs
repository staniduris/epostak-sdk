using System.Net;
using System.Text;
using EPostak.Models;
using Xunit;

namespace EPostak.Tests;

public sealed class EnterprisePreflightAndResolveTests
{
    [Fact]
    public async Task PreflightSendsUblDocumentAndDeserializesCurrentResponse()
    {
        var handler = new ApiContractHandler();
        using var http = new HttpClient(handler);
        using var client = CreateClient(http);

        var result = await client.Documents.PreflightAsync(new PreflightRequest
        {
            ReceiverPeppolId = "0245:2020298610",
            DocumentTypeId = "urn:invoice",
            ProcessId = "urn:process",
            UblDocument = "<Invoice/>",
        });

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/documents/preflight", request.Uri.AbsolutePath);
        Assert.Contains("\"processId\":\"urn:process\"", request.Body);
        Assert.Contains("\"ublDocument\":\"\\u003CInvoice/\\u003E\"", request.Body);

        Assert.Equal("sendable_with_warnings", result.Decision);
        Assert.True(result.NetworkReady);
        Assert.True(result.Valid);
        Assert.True(result.CanSend);
        Assert.True(result.ParticipantExists);
        Assert.Equal("BR-DEMO", Assert.Single(result.Warnings).Code);
        Assert.Null(Assert.Single(result.Errors).Location);
        Assert.Equal("No additional detail", Assert.Single(result.Checks).Message);
        Assert.True(result.Recipient?.Certificate?.Present);
        Assert.Equal("urn:process", result.DocumentProfile?.ProcessId);
        Assert.Equal(620, result.Trace?.DurationMs);
        Assert.True(result.ValidationPassed);
        Assert.Equal("0245:2020298610", result.ReceiverPeppolId);
        Assert.True(result.Registered);
        Assert.True(result.SupportsDocumentType);
        Assert.Equal("https://ap.example/as4", result.SmpUrl);
    }

    [Fact]
    public async Task PreflightPreservesNullValidationStateWhenNoDocumentWasChecked()
    {
        var handler = new ApiContractHandler(preflightResponse: """
            {
              "decision":"sendable",
              "networkReady":true,
              "valid":null,
              "canSend":true,
              "participantExists":true,
              "errors":[],
              "warnings":[],
              "checks":[{"name":"validation","status":"skipped","durationMs":0,"message":"No document supplied"}],
              "recipientFound":true,
              "recipientAcceptsDocumentType":true,
              "validationPassed":null,
              "validationErrors":[]
            }
            """);
        using var http = new HttpClient(handler);
        using var client = CreateClient(http);

        var result = await client.Documents.PreflightAsync(new PreflightRequest
        {
            ReceiverPeppolId = "0245:2020298610",
        });

        Assert.Null(result.Valid);
        Assert.Null(result.ValidationPassed);
        Assert.Equal("No document supplied", Assert.Single(result.Checks).Message);
    }

    [Fact]
    public async Task ResolveReturnsTypedErpInfo()
    {
        var handler = new ApiContractHandler();
        using var http = new HttpClient(handler);
        using var client = CreateClient(http);

        Task<ErpInfo> resolveTask = client.Peppol.ResolveErpInfoAsync(new Dictionary<string, string?>
        {
            ["ico"] = "12345678",
            ["documentTypeId"] = "urn:invoice",
        });
        var result = await resolveTask;

        var request = Assert.Single(handler.ApiRequests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/peppol/participants/resolve", request.Uri.AbsolutePath);
        Assert.Contains("ico=12345678", request.Uri.Query);
        Assert.Contains("documentTypeId=urn%3Ainvoice", request.Uri.Query);

        Assert.Equal("ico", result.Query?.Type);
        Assert.Equal("sendable", result.NextAction);
        Assert.Equal("SK2020123456", result.Company?.IcDph);
        Assert.Equal("Bratislava", result.Company?.Address?.City);
        Assert.True(result.Participant?.Registered);
        Assert.True(result.Participant?.Certificate?.Present);
        Assert.Equal("urn:invoice", Assert.Single(result.Participant!.SupportedDocumentTypes!));
        Assert.True(result.Capability?.NetworkReady);
    }

    [Fact]
    public async Task ResolveKeepsLegacyDictionaryReturnType()
    {
        var handler = new ApiContractHandler();
        using var http = new HttpClient(handler);
        using var client = CreateClient(http);

        Task<Dictionary<string, object?>> resolveTask = client.Peppol.ResolveAsync(
            new Dictionary<string, string?> { ["ico"] = "12345678" });
        var result = await resolveTask;

        Assert.Equal("sendable", result["nextAction"]?.ToString());
        Assert.Equal("/api/v1/peppol/participants/resolve", Assert.Single(handler.ApiRequests).Uri.AbsolutePath);
    }

    private static EPostakClient CreateClient(HttpClient http) =>
        new(new EPostakConfig
        {
            ClientId = "sk_int_test",
            ClientSecret = "sk_int_test",
            BaseUrl = "https://example.test/api/v1",
            FirmId = "firm-123",
        }, http);

    private sealed class ApiContractHandler : HttpMessageHandler
    {
        private readonly string _preflightResponse;

        public ApiContractHandler(string? preflightResponse = null)
        {
            _preflightResponse = preflightResponse ?? """
                {
                  "decision":"sendable_with_warnings",
                  "networkReady":true,
                  "valid":true,
                  "canSend":true,
                  "participantExists":true,
                  "errors":[{"category":"SYSTEM","code":"DEMO","severity":"warning","message":"Demo","location":null}],
                  "warnings":[{"category":"VALIDATION","code":"BR-DEMO","severity":"warning","message":"Buyer reference is recommended","location":"/Invoice/cbc:BuyerReference"}],
                  "checks":[{"name":"validation","status":"warning","durationMs":180,"message":"No additional detail"}],
                  "recipient":{"peppolId":"0245:2020298610","scheme":"0245","identifier":"2020298610","source":"sml","accessPoint":{"url":"https://ap.example/as4","transportProfile":"peppol-transport-as4-v2_0"},"certificate":{"present":true,"serviceExpirationDate":"2099-01-01T00:00:00Z"},"supportedDocumentTypes":["urn:invoice"]},
                  "documentProfile":{"documentTypeId":"urn:invoice","processId":"urn:process","ruleset":"Peppol BIS Billing 3.0","rulesetVersion":"current","inputMode":"ubl"},
                  "trace":{"traceId":"trace-1","checkedAt":"2026-07-21T09:00:00Z","durationMs":620,"validationMs":180,"participantLookupMs":420},
                  "recipientFound":true,
                  "recipientAcceptsDocumentType":true,
                  "validationPassed":true,
                  "validationErrors":[]
                }
                """;
        }

        public List<CapturedRequest> ApiRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token")
                return Json("""{"access_token":"token","refresh_token":"refresh","expires_in":3600}""");

            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ApiRequests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));

            return request.RequestUri?.AbsolutePath switch
            {
                "/api/v1/documents/preflight" => Json(_preflightResponse),
                "/api/v1/peppol/participants/resolve" => Json("""
                    {
                      "query":{"type":"ico","value":"12345678"},
                      "nextAction":"sendable",
                      "company":{"ico":"12345678","dic":"2020123456","icDph":"SK2020123456","name":"Demo s.r.o.","address":{"street":"Hlavna 1","city":"Bratislava","zip":"81101","country":"SK"},"active":true,"inPeppol":true,"source":"merged"},
                      "participant":{"peppolId":"0245:2020123456","scheme":"0245","identifier":"2020123456","registered":true,"source":"sml","accessPoint":{"url":"https://ap.example/as4","transportProfile":"peppol-transport-as4-v2_0"},"certificate":{"present":true,"serviceExpirationDate":"2099-01-01T00:00:00Z"},"supportedDocumentTypes":["urn:invoice"]},
                      "capability":{"documentTypeId":"urn:invoice","processId":"urn:process","accepts":true,"routingStatus":"ready","networkReady":true}
                    }
                    """),
                _ => Json("{}"),
            };
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
