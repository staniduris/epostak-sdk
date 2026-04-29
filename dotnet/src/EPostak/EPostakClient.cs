using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EPostak.Models;
using EPostak.Resources;

namespace EPostak;

/// <summary>
/// Main entry point for the ePošťák API. Provides access to all API resources:
/// auth, audit, documents, firms, Peppol lookups, webhooks, reporting, OCR
/// extraction, and account info.
/// </summary>
/// <example>
/// Basic usage with a direct API key:
/// <code>
/// var client = new EPostakClient(new EPostakConfig { ApiKey = "sk_live_xxxxx" });
/// var result = await client.Documents.SendAsync(new SendDocumentRequest
/// {
///     ReceiverPeppolId = "0192:12345678",
///     Items = new() { new() { Description = "Service", Quantity = 1, UnitPrice = 100m, VatRate = 23m } }
/// });
/// </code>
/// Integrator usage with firm switching:
/// <code>
/// var integrator = new EPostakClient(new EPostakConfig { ApiKey = "sk_int_xxxxx" });
/// var firmClient = integrator.WithFirm("firm-uuid-here");
/// var inbox = await firmClient.Documents.Inbox.ListAsync();
/// </code>
/// </example>
public sealed class EPostakClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly EPostakConfig _config;

    /// <summary>OAuth token mint/renew/revoke + key introspection, rotation, IP allowlist.</summary>
    public AuthResource Auth { get; }

    /// <summary>Per-firm audit feed (cursor-paginated).</summary>
    public AuditResource Audit { get; }

    /// <summary>Send, receive, and manage e-invoicing documents via the Peppol network.</summary>
    public DocumentsResource Documents { get; }

    /// <summary>Manage client firms and their Peppol identifiers. For integrator keys, supports multi-tenant firm assignment.</summary>
    public FirmsResource Firms { get; }

    /// <summary>Peppol network lookups: SMP participant discovery, directory search, and Slovak company registry.</summary>
    public PeppolResource Peppol { get; }

    /// <summary>Manage push webhook subscriptions and poll the pull queue for events.</summary>
    public WebhooksResource Webhooks { get; }

    /// <summary>Document statistics and usage reports for the current billing period.</summary>
    public ReportingResource Reporting { get; }

    /// <summary>AI-powered OCR extraction from PDFs and images into structured invoice data and UBL XML.</summary>
    public ExtractResource Extract { get; }

    /// <summary>Account information including firm details, subscription plan, and usage counters.</summary>
    public AccountResource Account { get; }

    /// <summary>
    /// Create a new ePosťak client with the given configuration.
    /// Uses an internal <see cref="HttpClient"/> instance that is disposed when the client is disposed.
    /// </summary>
    /// <param name="config">API configuration including the API key and optional firm ID.</param>
    /// <example>
    /// <code>
    /// var client = new EPostakClient(new EPostakConfig { ApiKey = "sk_live_xxxxx" });
    /// </code>
    /// </example>
    public EPostakClient(EPostakConfig config) : this(config, new HttpClient(), ownsHttpClient: true) { }

    /// <summary>
    /// Create a new ePosťak client with a shared <see cref="HttpClient"/>.
    /// Use this overload when you manage the HttpClient lifetime yourself (e.g. via IHttpClientFactory).
    /// The client will NOT dispose the provided HttpClient.
    /// </summary>
    /// <param name="config">API configuration including the API key and optional firm ID.</param>
    /// <param name="httpClient">A shared HttpClient instance. The caller is responsible for its lifetime.</param>
    /// <example>
    /// <code>
    /// var httpClient = httpClientFactory.CreateClient();
    /// var client = new EPostakClient(new EPostakConfig { ApiKey = "sk_live_xxxxx" }, httpClient);
    /// </code>
    /// </example>
    public EPostakClient(EPostakConfig config, HttpClient httpClient) : this(config, httpClient, ownsHttpClient: false) { }

    private EPostakClient(EPostakConfig config, HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(config));

        _config = config;
        _http = httpClient;
        _ownsHttpClient = ownsHttpClient;

        var requestor = new HttpRequestor(_http, config.ApiKey, config.BaseUrl, config.FirmId, config.MaxRetries);

        Auth = new AuthResource(requestor, _http, config.BaseUrl);
        Audit = new AuditResource(requestor);
        Documents = new DocumentsResource(requestor);
        Firms = new FirmsResource(requestor);
        Peppol = new PeppolResource(requestor);
        Webhooks = new WebhooksResource(requestor);
        Reporting = new ReportingResource(requestor);
        Extract = new ExtractResource(requestor);
        Account = new AccountResource(requestor);
    }

    /// <summary>
    /// Create a new client instance scoped to a specific firm. The new client shares the same
    /// underlying <see cref="HttpClient"/> and API key, but all requests include the firm's
    /// <c>X-Firm-Id</c> header. Only meaningful with integrator keys (<c>sk_int_*</c>).
    /// </summary>
    /// <param name="firmId">The firm UUID to scope requests to.</param>
    /// <returns>A new <see cref="EPostakClient"/> instance scoped to the specified firm.</returns>
    /// <example>
    /// <code>
    /// var integrator = new EPostakClient(new EPostakConfig { ApiKey = "sk_int_xxxxx" });
    /// var firms = await integrator.Firms.ListAsync();
    /// var firmClient = integrator.WithFirm(firms[0].Id);
    /// var inbox = await firmClient.Documents.Inbox.ListAsync();
    /// </code>
    /// </example>
    public EPostakClient WithFirm(string firmId)
    {
        ArgumentNullException.ThrowIfNull(firmId);
        return new EPostakClient(
            new EPostakConfig
            {
                ApiKey = _config.ApiKey,
                BaseUrl = _config.BaseUrl,
                FirmId = firmId,
                MaxRetries = _config.MaxRetries,
            },
            _http,
            ownsHttpClient: false);
    }

    /// <summary>
    /// Default public validate endpoint. Used by <see cref="Validate(string)"/> and
    /// derived from the client's base URL by the instance method <see cref="ValidateAsync"/>.
    /// </summary>
    private const string DefaultPublicValidateUrl = "https://epostak.sk/api/validate";

    /// <summary>
    /// Validate a UBL XML document against Peppol BIS 3.0 rules using this client's
    /// configured base URL. No API key is sent -- the public <c>/api/validate</c>
    /// endpoint is authentication-free and rate-limited to 20 requests/minute per IP.
    /// </summary>
    /// <param name="xml">UBL 2.1 XML document to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full 3-layer Peppol BIS 3.0 validation report (XSD / EN 16931 / Peppol BIS).</returns>
    /// <example>
    /// <code>
    /// var report = await client.ValidateAsync(ublXml);
    /// if (!report.Valid)
    ///     foreach (var e in report.Errors)
    ///         Console.Error.WriteLine($"{e.Rule}: {e.Message}");
    /// </code>
    /// </example>
    public Task<ValidationReport> ValidateAsync(string xml, CancellationToken ct = default)
        => Validate(xml, DeriveValidateUrl(_config.BaseUrl), _http, ct);

    /// <summary>
    /// Validate a UBL XML document against Peppol BIS 3.0 rules without constructing a client.
    /// Uses the production public endpoint <c>https://epostak.sk/api/validate</c>.
    /// </summary>
    /// <remarks>Public endpoint -- no API key is sent. Rate-limited to 20 requests/minute per IP.</remarks>
    /// <param name="xml">UBL 2.1 XML document to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full 3-layer Peppol BIS 3.0 validation report.</returns>
    /// <example>
    /// <code>
    /// var report = await EPostakClient.Validate(ublXml);
    /// Console.WriteLine($"Valid: {report.Valid} ({report.Summary.Errors} errors)");
    /// </code>
    /// </example>
    public static Task<ValidationReport> Validate(string xml, CancellationToken ct = default)
        => Validate(xml, DefaultPublicValidateUrl, ct);

    /// <summary>
    /// Validate a UBL XML document against a custom validate endpoint. Useful for staging
    /// or on-premise deployments.
    /// </summary>
    /// <param name="xml">UBL 2.1 XML document to validate.</param>
    /// <param name="url">Full URL of the validate endpoint (e.g. <c>https://staging.epostak.sk/api/validate</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full 3-layer Peppol BIS 3.0 validation report.</returns>
    public static async Task<ValidationReport> Validate(string xml, string url, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        return await Validate(xml, url, http, ct).ConfigureAwait(false);
    }

    private static async Task<ValidationReport> Validate(string xml, string url, HttpClient http, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(xml, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" } }
            }
        };

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new EPostakException($"Network error: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new EPostakException((int)response.StatusCode, body);
            }

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return (await JsonSerializer.DeserializeAsync<ValidationReport>(stream, HttpRequestor.JsonOptions, ct).ConfigureAwait(false))!;
        }
    }

    /// <summary>
    /// Derive the public validate URL from a configured enterprise base URL, preserving
    /// scheme and host so staging and custom deployments keep working.
    /// </summary>
    private static string DeriveValidateUrl(string baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl))
            return DefaultPublicValidateUrl;

        var v1 = baseUrl.IndexOf("/api/v1", StringComparison.Ordinal);
        if (v1 >= 0)
            return baseUrl[..v1] + "/api/validate";

        var legacy = baseUrl.IndexOf("/api/enterprise", StringComparison.Ordinal);
        if (legacy >= 0)
            return baseUrl[..legacy] + "/api/validate";

        return baseUrl.EndsWith('/')
            ? baseUrl + "validate"
            : baseUrl + "/validate";
    }

    /// <summary>
    /// Dispose the client. If the client owns its <see cref="HttpClient"/>
    /// (created via the single-parameter constructor), the HttpClient is also disposed.
    /// Clients created via <see cref="WithFirm"/> never dispose the shared HttpClient.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
