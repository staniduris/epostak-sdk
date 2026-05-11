package sk.epostak.sdk;

import com.google.gson.Gson;
import sk.epostak.sdk.models.ValidationReport;
import sk.epostak.sdk.resources.*;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;

/**
 * ePošťák API client.
 *
 * <pre>{@code
 * EPostak client = EPostak.builder()
 *     .clientId("sk_live_xxxxx")
 *     .clientSecret("sk_live_xxxxx")
 *     .build();
 *
 * SendDocumentResponse result = client.documents().send(
 *     SendDocumentRequest.builder("0245:1234567890")
 *         .invoiceNumber("FV-2026-001")
 *         .issueDate("2026-04-04")
 *         .dueDate("2026-04-18")
 *         .items(List.of(new SendDocumentRequest.LineItem(
 *             "Consulting", 10, 50, 23)))
 *         .build());
 * }</pre>
 */
public final class EPostak {

    private static final String DEFAULT_BASE_URL = "https://epostak.sk/api/v1";
    private static final String DEFAULT_PUBLIC_VALIDATE_URL = "https://epostak.sk/api/validate";

    private final HttpClient httpClient;
    private final TokenManager tokenManager;
    private final String clientId;
    private final String clientSecret;
    private final String baseUrl;
    private final String firmId;

    private final DocumentsResource documents;
    private final FirmsResource firms;
    private final PeppolResource peppol;
    private final WebhooksResource webhooks;
    private final ReportingResource reporting;
    private final ExtractResource extract;
    private final AccountResource account;
    private final AuthResource auth;
    private final AuditResource audit;
    private final IntegratorResource integrator;
    private final InboundResource inbound;
    private final OutboundResource outbound;

    /** Maximum number of retries on 429/5xx. */
    private final int maxRetries;

    private EPostak(Builder builder) {
        if (builder.clientId == null || builder.clientId.isBlank()) {
            throw new IllegalArgumentException("EPostak: clientId is required");
        }
        if (builder.clientSecret == null || builder.clientSecret.isBlank()) {
            throw new IllegalArgumentException("EPostak: clientSecret is required");
        }

        this.clientId = builder.clientId;
        this.clientSecret = builder.clientSecret;
        this.baseUrl = builder.baseUrl != null ? builder.baseUrl : DEFAULT_BASE_URL;
        this.firmId = builder.firmId;
        this.maxRetries = builder.maxRetries;
        this.tokenManager = builder.tokenManager != null
                ? builder.tokenManager
                : new TokenManager(this.clientId, this.clientSecret, this.baseUrl, this.firmId);
        this.httpClient = new HttpClient(this.baseUrl, this.tokenManager, this.firmId, this.maxRetries);

        this.documents = new DocumentsResource(httpClient);
        this.firms = new FirmsResource(httpClient);
        this.peppol = new PeppolResource(httpClient);
        this.webhooks = new WebhooksResource(httpClient);
        this.reporting = new ReportingResource(httpClient);
        this.extract = new ExtractResource(httpClient);
        this.account = new AccountResource(httpClient);
        this.auth = new AuthResource(httpClient);
        this.audit = new AuditResource(httpClient);
        this.integrator = new IntegratorResource(httpClient);
        this.inbound = new InboundResource(httpClient);
        this.outbound = new OutboundResource(httpClient);
    }

    /**
     * Create a new builder for configuring and constructing an {@link EPostak} client.
     *
     * @return a new builder instance
     */
    public static Builder builder() {
        return new Builder();
    }

    // -- resource accessors ---------------------------------------------------

    /**
     * Send and receive documents via Peppol.
     *
     * @return the documents resource
     */
    public DocumentsResource documents() { return documents; }

    /**
     * Manage client firms (integrator keys).
     *
     * @return the firms resource
     */
    public FirmsResource firms() { return firms; }

    /**
     * SMP lookup and Peppol directory search.
     *
     * @return the Peppol resource
     */
    public PeppolResource peppol() { return peppol; }

    /**
     * Manage webhook subscriptions and pull queue.
     *
     * @return the webhooks resource
     */
    public WebhooksResource webhooks() { return webhooks; }

    /**
     * Document statistics and reports.
     *
     * @return the reporting resource
     */
    public ReportingResource reporting() { return reporting; }

    /**
     * AI-powered OCR extraction from PDFs and images.
     *
     * @return the extract resource
     */
    public ExtractResource extract() { return extract; }

    /**
     * Account and firm information.
     *
     * @return the account resource
     */
    public AccountResource account() { return account; }

    /**
     * OAuth token mint/renew/revoke + key introspection, rotation, IP allowlist.
     *
     * @return the auth resource
     */
    public AuthResource auth() { return auth; }

    /**
     * Per-firm audit feed (cursor-paginated).
     *
     * @return the audit resource
     */
    public AuditResource audit() { return audit; }

    /**
     * Integrator-aggregate endpoints (requires an {@code sk_int_*} key).
     *
     * @return the integrator resource
     */
    public IntegratorResource integrator() { return integrator; }

    /**
     * Pull API — received (inbound) documents.
     * <p>
     * Use this resource for cursor-based polling of inbound Peppol documents.
     * Requires {@code requireApiEligiblePlan} (api-enterprise or integrator-managed).
     *
     * @return the inbound pull API resource
     */
    public InboundResource inbound() { return inbound; }

    /**
     * Pull API — sent (outbound) documents and event stream.
     * <p>
     * Use this resource for cursor-based inspection of outbound Peppol documents
     * and their lifecycle events.
     * Requires {@code requireApiEligiblePlan} (api-enterprise or integrator-managed).
     *
     * @return the outbound pull API resource
     */
    public OutboundResource outbound() { return outbound; }

    /**
     * Returns the rate-limit snapshot captured from the most recent API response.
     * <p>
     * Every API response includes {@code X-RateLimit-Limit},
     * {@code X-RateLimit-Remaining}, and {@code X-RateLimit-Reset} headers.
     * These are captured automatically and made available here so callers can
     * implement back-pressure without parsing headers manually.
     *
     * <pre>{@code
     * client.documents().list();
     * RateLimitInfo rl = client.getLastRateLimit();
     * if (rl != null && rl.getRemaining() < 10) {
     *     long msUntilReset = rl.getResetAt().toEpochMilli() - System.currentTimeMillis();
     *     Thread.sleep(msUntilReset);
     * }
     * }</pre>
     *
     * @return the last observed rate-limit info, or {@code null} if no response has been received
     */
    public RateLimitInfo getLastRateLimit() {
        return httpClient.getLastRateLimit();
    }

    /**
     * Validate a UBL XML document against Peppol BIS 3.0 rules without creating a client.
     * Uses the production public endpoint at {@code https://epostak.sk/api/validate}.
     * <p>
     * Public endpoint — no API key is sent. Rate-limited to 20 requests per minute
     * per IP address.
     *
     * <pre>{@code
     * ValidationReport report = EPostak.validate(ublXml);
     * if (!report.valid()) {
     *     report.errors().forEach(e -> System.err.println(e.rule() + ": " + e.message()));
     * }
     * }</pre>
     *
     * @param xml the UBL 2.1 XML document to validate
     * @return the full 3-layer Peppol BIS 3.0 validation report
     * @throws EPostakException if the request fails
     */
    public static ValidationReport validate(String xml) {
        return validate(xml, DEFAULT_PUBLIC_VALIDATE_URL);
    }

    /**
     * Validate a UBL XML document against Peppol BIS 3.0 rules using a custom endpoint.
     * Useful for staging or on-premise deployments.
     *
     * @param xml the UBL 2.1 XML document to validate
     * @param url full URL of the validate endpoint (e.g. {@code "https://staging.epostak.sk/api/validate"})
     * @return the full 3-layer Peppol BIS 3.0 validation report
     * @throws EPostakException if the request fails
     */
    public static ValidationReport validate(String xml, String url) {
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .timeout(Duration.ofSeconds(30))
                .header("Content-Type", "application/xml")
                .POST(HttpRequest.BodyPublishers.ofString(xml, StandardCharsets.UTF_8))
                .build();

        java.net.http.HttpClient jdkClient = java.net.http.HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(30))
                .build();

        HttpResponse<String> response;
        try {
            response = jdkClient.send(request, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
        } catch (IOException | InterruptedException e) {
            if (e instanceof InterruptedException) Thread.currentThread().interrupt();
            throw new EPostakException(0, e.getMessage());
        }

        if (response.statusCode() >= 400) {
            throw new EPostakException(response.statusCode(), response.body());
        }

        return new Gson().fromJson(response.body(), ValidationReport.class);
    }

    /**
     * Create a new client instance scoped to a specific firm.
     * Useful when an integrator key needs to switch between clients.
     *
     * <pre>{@code
     * EPostak integrator = EPostak.builder()
     *     .clientId("sk_int_xxxxx")
     *     .clientSecret("sk_int_xxxxx")
     *     .build();
     *
     * // Scope to a specific firm
     * EPostak firmClient = integrator.withFirm("firm-uuid-here");
     * firmClient.documents().send(...);
     * }</pre>
     *
     * @param firmId the firm UUID to scope subsequent requests to
     * @return a new EPostak instance with the firm ID set
     */
    public EPostak withFirm(String firmId) {
        return new Builder()
                .clientId(this.clientId)
                .clientSecret(this.clientSecret)
                .baseUrl(this.baseUrl)
                .firmId(firmId)
                .tokenManager(this.tokenManager)
                .maxRetries(this.maxRetries)
                .build();
    }

    // -- builder --------------------------------------------------------------

    /**
     * Builder for constructing an {@link EPostak} client instance.
     * <p>
     * Required fields: {@link #clientId(String)} and {@link #clientSecret(String)}.
     * All other fields have sensible defaults.
     */
    public static final class Builder {
        /** OAuth client ID (the API key, e.g. {@code sk_live_*}). Required. */
        private String clientId;
        /** OAuth client secret. Required. */
        private String clientSecret;
        /** Base URL for the API. Defaults to {@code https://epostak.sk/api/v1}. */
        private String baseUrl;
        /** Firm UUID for integrator key scoping. Optional. */
        private String firmId;
        /** Maximum retries on 429/5xx for GET/DELETE. Defaults to 3. */
        private int maxRetries = 3;
        /** Shared token manager (set internally by withFirm). */
        private TokenManager tokenManager;

        private Builder() {}

        /**
         * Set the OAuth client ID. Required.
         * Use {@code sk_live_*} for direct access or {@code sk_int_*} for integrator access.
         *
         * @param clientId the client ID (API key)
         * @return this builder
         */
        public Builder clientId(String clientId) {
            this.clientId = clientId;
            return this;
        }

        /**
         * Set the OAuth client secret. Required.
         *
         * @param clientSecret the client secret
         * @return this builder
         */
        public Builder clientSecret(String clientSecret) {
            this.clientSecret = clientSecret;
            return this;
        }

        /**
         * Set a shared token manager (used internally by {@link EPostak#withFirm(String)}).
         *
         * @param tokenManager the token manager to reuse
         * @return this builder
         */
        Builder tokenManager(TokenManager tokenManager) {
            this.tokenManager = tokenManager;
            return this;
        }

        /**
         * Override the base URL. Defaults to {@code https://epostak.sk/api/v1}.
         *
         * @param baseUrl the base URL for API requests
         * @return this builder
         */
        public Builder baseUrl(String baseUrl) {
            this.baseUrl = baseUrl;
            return this;
        }

        /**
         * Set the firm UUID to act on behalf of.
         * Required when using integrator keys ({@code sk_int_*}).
         * Each API call will include the {@code X-Firm-Id} header.
         *
         * @param firmId the firm UUID
         * @return this builder
         */
        public Builder firmId(String firmId) {
            this.firmId = firmId;
            return this;
        }

        /**
         * Set the maximum number of retries on 429/5xx responses for GET/DELETE requests.
         * Uses exponential backoff with jitter. Defaults to 3. Set to 0 to disable.
         *
         * @param maxRetries the maximum number of retries
         * @return this builder
         */
        public Builder maxRetries(int maxRetries) {
            this.maxRetries = maxRetries;
            return this;
        }

        /**
         * Build the client instance.
         *
         * @return a new {@link EPostak} client
         * @throws IllegalArgumentException if {@code clientId} or {@code clientSecret} is null or blank
         */
        public EPostak build() {
            return new EPostak(this);
        }
    }
}
