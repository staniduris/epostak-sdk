using System.Net;
using System.Text;
using Xunit;

namespace EPostak.Tests;

public sealed class ErrorFieldsTests
{
    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(request.RequestUri?.AbsolutePath == "/sapi/v1/auth/token"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"token","refresh_token":"refresh","expires_in":3600}""", Encoding.UTF8, "application/json"),
                }
                : new HttpResponseMessage((HttpStatusCode)422)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
    }

    private static async Task<EPostakException> Fail(string body)
    {
        using var http = new HttpClient(new Handler(body));
        using var client = new EPostakClient(new EPostakConfig
        {
            ClientId = "sk_int_test",
            ClientSecret = "sk_int_test",
            BaseUrl = "https://example.test/api/v1",
            MaxRetries = 0,
        }, http);
        return await Assert.ThrowsAsync<EPostakException>(() => client.Enterprise.Documents.GetAsync("x"));
    }

    [Fact]
    public async Task ParsesAdditiveErrorFields()
    {
        var ex = await Fail("""
        {"error":{"code":"DOCUMENT_INVALID","message":"unitPrice must be a number","retryable":false,
         "nextAction":"Correct the indicated document field and retry.","apiCode":"VALIDATION_ERROR",
         "message_sk":"Jednotková cena musí byť číslo",
         "fields":[{"path":"items[0].unitPrice","message":"unitPrice must be a number","message_sk":"Jednotková cena musí byť číslo"}],
         "rule":"PEPPOL-EN16931-R003","fix_hint":"Send unitPrice as a number.","fix_hint_sk":"Pošlite unitPrice ako číslo.",
         "docs":"https://epostak.sk/api/docs","requestId":"request-ubl-validation"}}
        """);
        Assert.Equal("DOCUMENT_INVALID", ex.Code);
        Assert.Equal("unitPrice must be a number", ex.Message);
        Assert.Equal("Correct the indicated document field and retry.", ex.NextAction);
        Assert.False(ex.Retryable);
        Assert.Equal("request-ubl-validation", ex.RequestId);
        Assert.Equal("VALIDATION_ERROR", ex.ApiCode);
        Assert.Equal("Jednotková cena musí byť číslo", ex.MessageSk);
        Assert.Equal("PEPPOL-EN16931-R003", ex.Rule);
        Assert.Equal("Send unitPrice as a number.", ex.FixHint);
        Assert.Equal("Pošlite unitPrice ako číslo.", ex.FixHintSk);
        Assert.Equal("https://epostak.sk/api/docs", ex.Docs);
        Assert.NotNull(ex.Fields);
        var f = Assert.Single(ex.Fields!);
        Assert.Equal("items[0].unitPrice", f.GetProperty("path").GetString());
        Assert.Equal("Jednotková cena musí byť číslo", f.GetProperty("message_sk").GetString());
    }

    [Fact]
    public async Task MissingOrWrongTypedFieldsAreNull()
    {
        var ex = await Fail("""{"error":{"code":"X","message":"m","apiCode":5,"fields":"nope","docs":{}}}""");
        Assert.Equal("X", ex.Code);
        Assert.Null(ex.ApiCode);
        Assert.Null(ex.MessageSk);
        Assert.Null(ex.Fields);
        Assert.Null(ex.Rule);
        Assert.Null(ex.FixHint);
        Assert.Null(ex.FixHintSk);
        Assert.Null(ex.Docs);
    }
}
