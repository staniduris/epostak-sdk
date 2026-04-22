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
 * ePosťak Enterprise API client.
 *
 * <pre>{@code
 * EPostak client = EPostak.builder()
 *     .apiKey("sk_live_xxxxx")
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

    private static final String DEFAULT_BASE_URL = "https://epostak.sk/api/enterprise";
    private static final String DEFAULT_PUBLIC_VALIDATE_URL = "https://epostak.sk/api/validate";

    private final HttpClient httpClient;
    private final String apiKey;
    private final String baseUrl;
    private final String firmId;

    private final DocumentsResource documents;
    private final FirmsResource firms;
    private final PeppolResource peppol;
    private final WebhooksResource webhooks;
    private final ReportingResource reporting;
    private final ExtractResource extract;
    private final AccountResource account;

    /** Maximum number of retries on 429/5xx. */
    private final int maxRetries;

    private EPostak(Builder builder) {
        if (builder.apiKey == null || builder.apiKey.isBlank()) {
            throw new IllegalArgumentException("EPostak: apiKey is required");
        }

        this.apiKey = builder.apiKey;
        this.baseUrl = builder.baseUrl != null ? builder.baseUrl : DEFAULT_BASE_URL;
        this.firmId = builder.firmId;
        this.maxRetries = builder.maxRetries;
        this.httpClient = new HttpClient(this.baseUrl, this.apiKey, this.firmId, this.maxRetries);

        this.documents = new DocumentsResource(httpClient);
        this.firms = new FirmsResource(httpClient);
        this.peppol = new PeppolResource(httpClient);
        this.webhooks = new WebhooksResource(httpClient);
        this.reporting = new ReportingResource(httpClient);
        this.extract = new ExtractResource(httpClient);
        this.account = new AccountResource(httpClient);
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
     * Derive the public validate URL from an enterprise base URL, preserving host
     * and scheme so staging and custom deployments keep working.
     */
    private static String deriveValidateUrl(String enterpriseBaseUrl) {
        if (enterpriseBaseUrl == null) return DEFAULT_PUBLIC_VALIDATE_URL;
        int idx = enterpriseBaseUrl.indexOf("/api/enterprise");
        if (idx >= 0) {
            return enterpriseBaseUrl.substring(0, idx) + "/api/validate";
        }
        // Fallback: append /validate to whatever base was supplied
        return enterpriseBaseUrl.endsWith("/")
                ? enterpriseBaseUrl + "validate"
                : enterpriseBaseUrl + "/validate";
    }

    /**
     * Create a new client instance scoped to a specific firm.
     * Useful when an integrator key needs to switch between clients.
     *
     * <pre>{@code
     * EPostak integrator = EPostak.builder()
     *     .apiKey("sk_int_xxxxx")
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
                .apiKey(this.apiKey)
                .baseUrl(this.baseUrl)
                .firmId(firmId)
                .maxRetries(this.maxRetries)
                .build();
    }

    // -- builder --------------------------------------------------------------

    /**
     * Builder for constructing an {@link EPostak} client instance.
     * <p>
     * The only required field is {@link #apiKey(String)}. All other fields have sensible defaults.
     */
    public static final class Builder {
        /** API key for authentication. Required. */
        private String apiKey;
        /** Base URL for the API. Defaults to {@code https://epostak.sk/api/enterprise}. */
        private String baseUrl;
        /** Firm UUID for integrator key scoping. Optional. */
        private String firmId;
        /** Maximum retries on 429/5xx for GET/DELETE. Defaults to 3. */
        private int maxRetries = 3;

        private Builder() {}

        /**
         * Set the API key. Required.
         * Use {@code sk_live_*} for direct access or {@code sk_int_*} for integrator access.
         *
         * @param apiKey the API key
         * @return this builder
         */
        public Builder apiKey(String apiKey) {
            this.apiKey = apiKey;
            return this;
        }

        /**
         * Override the base URL. Defaults to {@code https://epostak.sk/api/enterprise}.
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
         * @throws IllegalArgumentException if {@code apiKey} is null or blank
         */
        public EPostak build() {
            return new EPostak(this);
        }
    }
}
