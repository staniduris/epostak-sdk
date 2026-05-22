using System.Net;
using System.Text;
using EPostak.Models;
using EPostak.Resources;
using Xunit;

namespace EPostak.Tests;

public sealed class OAuthTests
{
    [Fact]
    public async Task ExchangeCodeAsyncReturnsIssuedIntegratorClientSecret()
    {
        const string payload = """
        {
          "client_id": "sk_int_QVN1D...GvUQ",
          "client_secret": "sk_int_QVNabcdefghijklmnopqrstuvwxyz",
          "secret_type": "sk_int",
          "token_type": "client_secret",
          "scope": "firms:manage documents:send documents:read",
          "firm_id": "0c39db7f-c88a-4426-86ab-d637ed552004",
          "firm_name": "BNS Sandbox firma 1",
          "firm_ico": "91782991"
        }
        """;
        var handler = new JsonHandler(payload);
        using var http = new HttpClient(handler);

        OAuthTokenResponse response = await OAuth.ExchangeCodeAsync(
            code: "auth-code",
            codeVerifier: "verifier",
            clientId: "oauth-client-id",
            clientSecret: "oauth-client-secret",
            redirectUri: "https://client.example/callback",
            origin: "https://epostak.example",
            httpClient: http);

        Assert.Equal("sk_int_QVN1D...GvUQ", response.ClientId);
        Assert.Equal("sk_int_QVNabcdefghijklmnopqrstuvwxyz", response.ClientSecret);
        Assert.Equal("sk_int", response.SecretType);
        Assert.Equal("client_secret", response.TokenType);
        Assert.Equal("0c39db7f-c88a-4426-86ab-d637ed552004", response.FirmId);
        Assert.Equal("BNS Sandbox firma 1", response.FirmName);
        Assert.Equal("91782991", response.FirmIco);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://epostak.example/api/oauth/token", handler.Uri?.ToString());
        Assert.Contains("grant_type=authorization_code", handler.RequestBody);
    }

    private sealed class JsonHandler(string payload) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string RequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            RequestBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }
    }
}
