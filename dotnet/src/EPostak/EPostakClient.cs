using EPostak.Resources;

namespace EPostak;

/// <summary>
/// Main entry point for the ePosťak Enterprise API. Provides access to all API
/// resources: documents, firms, Peppol lookups, webhooks, reporting, OCR extraction, and account info.
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

        var requestor = new HttpRequestor(_http, config.ApiKey, config.BaseUrl, config.FirmId);

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
            },
            _http,
            ownsHttpClient: false);
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
