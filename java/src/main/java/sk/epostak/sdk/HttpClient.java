package sk.epostak.sdk;

import com.google.gson.Gson;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.reflect.TypeToken;

import java.lang.reflect.Type;

import java.io.IOException;
import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpHeaders;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ThreadLocalRandom;

/**
 * Internal HTTP wrapper around {@link java.net.http.HttpClient}.
 * <p>
 * Handles authentication headers ({@code Authorization: Bearer} and optional
 * {@code X-Firm-Id}), JSON serialization via Gson, error normalization into
 * {@link EPostakException}, and 204 No Content responses. Not intended for
 * direct use -- access the API via resource classes instead.
 */
public final class HttpClient {

    private static final Gson GSON = new Gson();
    private static final Duration TIMEOUT = Duration.ofSeconds(30);
    private static final Set<String> RETRYABLE_METHODS = Set.of("GET", "DELETE");

    /** Base URL for API requests, e.g. {@code "https://epostak.sk/api/v1"}. */
    private final String baseUrl;
    /** Token manager that provides JWT access tokens. */
    private final TokenManager tokenManager;
    /** Optional firm UUID sent as {@code X-Firm-Id} header. */
    private final String firmId;
    /** Maximum number of retries on 429/5xx for GET/DELETE requests. */
    private final int maxRetries;
    /** Underlying JDK HTTP client. */
    private final java.net.http.HttpClient client;

    /**
     * Creates a new HTTP client.
     *
     * @param baseUrl      the API base URL
     * @param tokenManager the token manager that provides JWT bearer tokens
     * @param firmId       optional firm UUID for the {@code X-Firm-Id} header, or {@code null}
     * @param maxRetries   maximum number of retries on 429/5xx (default 3)
     */
    HttpClient(String baseUrl, TokenManager tokenManager, String firmId, int maxRetries) {
        this.baseUrl = baseUrl;
        this.tokenManager = tokenManager;
        this.firmId = firmId;
        this.maxRetries = maxRetries;
        this.client = java.net.http.HttpClient.newBuilder()
                .connectTimeout(TIMEOUT)
                .build();
    }

    /**
     * Creates a new HTTP client with default retry count (3).
     *
     * @param baseUrl      the API base URL
     * @param tokenManager the token manager that provides JWT bearer tokens
     * @param firmId       optional firm UUID for the {@code X-Firm-Id} header, or {@code null}
     */
    HttpClient(String baseUrl, TokenManager tokenManager, String firmId) {
        this(baseUrl, tokenManager, firmId, 3);
    }

    /**
     * Returns the configured base URL (e.g. {@code "https://epostak.sk/api/v1"}).
     *
     * @return the base URL string
     */
    public String getBaseUrl() {
        return baseUrl;
    }

    /**
     * Returns the token manager used for {@code Authorization: Bearer} headers.
     *
     * @return the token manager
     */
    public TokenManager getTokenManager() {
        return tokenManager;
    }

    /**
     * Returns the optional firm UUID forwarded as {@code X-Firm-Id}, or {@code null}.
     *
     * @return the firm UUID or {@code null}
     */
    public String getFirmId() {
        return firmId;
    }

    // -- convenience request methods ------------------------------------------

    /**
     * Perform a GET request and deserialize the response.
     *
     * @param <T>  the response type
     * @param path the API path (appended to base URL)
     * @param type the response class to deserialize to
     * @return the deserialized response, or {@code null} for 204 responses
     * @throws EPostakException if the request fails
     */
    public <T> T get(String path, Class<T> type) {
        return request("GET", path, null, type);
    }

    /**
     * Perform a GET request and deserialize the response using a Gson
     * {@link TypeToken}. Use this when the response type is generic
     * (e.g. {@link sk.epostak.sdk.models.CursorPage} of
     * {@link sk.epostak.sdk.models.AuditEvent}) and a raw {@link Class} is
     * insufficient.
     *
     * @param <T>       the response type
     * @param path      the API path (appended to base URL)
     * @param typeToken the response type token to deserialize into
     * @return the deserialized response
     * @throws EPostakException if the request fails
     */
    public <T> T getTyped(String path, TypeToken<T> typeToken) {
        return requestTyped("GET", path, null, typeToken, Map.of());
    }

    /**
     * Perform a POST request with a JSON body and deserialize the response.
     *
     * @param <T>  the response type
     * @param path the API path (appended to base URL)
     * @param body the request body to serialize as JSON, or {@code null}
     * @param type the response class to deserialize to
     * @return the deserialized response, or {@code null} for 204 responses
     * @throws EPostakException if the request fails
     */
    public <T> T post(String path, Object body, Class<T> type) {
        return request("POST", path, body, type);
    }

    /**
     * Perform a POST request with a JSON body and an explicit {@code Idempotency-Key}
     * header for safe replay of mutating endpoints.
     *
     * @param <T>            the response type
     * @param path           the API path (appended to base URL)
     * @param body           the request body to serialize as JSON, or {@code null}
     * @param type           the response class to deserialize to
     * @param idempotencyKey opaque idempotency key (1-255 chars), or {@code null}
     * @return the deserialized response
     * @throws EPostakException if the request fails
     */
    public <T> T postIdempotent(String path, Object body, Class<T> type, String idempotencyKey) {
        Map<String, String> headers = idempotencyKey != null
                ? Map.of("Idempotency-Key", idempotencyKey)
                : Map.of();
        return request("POST", path, body, type, headers);
    }

    /**
     * Perform a PATCH request with a JSON body and deserialize the response.
     *
     * @param <T>  the response type
     * @param path the API path (appended to base URL)
     * @param body the request body to serialize as JSON
     * @param type the response class to deserialize to
     * @return the deserialized response, or {@code null} for 204 responses
     * @throws EPostakException if the request fails
     */
    public <T> T patch(String path, Object body, Class<T> type) {
        return request("PATCH", path, body, type);
    }

    /**
     * Perform a PUT request with a JSON body and deserialize the response.
     *
     * @param <T>  the response type
     * @param path the API path (appended to base URL)
     * @param body the request body to serialize as JSON
     * @param type the response class to deserialize to
     * @return the deserialized response, or {@code null} for 204 responses
     * @throws EPostakException if the request fails
     */
    public <T> T put(String path, Object body, Class<T> type) {
        return request("PUT", path, body, type);
    }

    /**
     * Perform a DELETE request and deserialize the response.
     *
     * @param <T>  the response type
     * @param path the API path (appended to base URL)
     * @param type the response class to deserialize to
     * @return the deserialized response, or {@code null} for 204 responses
     * @throws EPostakException if the request fails
     */
    public <T> T delete(String path, Class<T> type) {
        return request("DELETE", path, null, type);
    }

    /**
     * Perform a DELETE request that returns no body (HTTP 204).
     *
     * @param path the API path (appended to base URL)
     * @throws EPostakException if the request fails
     */
    public void deleteVoid(String path) {
        request("DELETE", path, null, Void.class);
    }

    /**
     * Perform a POST request that returns no body (HTTP 204).
     *
     * @param path the API path (appended to base URL)
     * @param body the request body to serialize as JSON
     * @throws EPostakException if the request fails
     */
    public void postVoid(String path, Object body) {
        request("POST", path, body, Void.class);
    }

    /**
     * POST with {@code multipart/form-data} for single file uploads.
     *
     * @param <T>       the response type
     * @param path      the API path (appended to base URL)
     * @param fileBytes raw file content
     * @param fileName  the file name with extension
     * @param mimeType  the MIME type of the file
     * @param type      the response class to deserialize to
     * @return the deserialized response
     * @throws EPostakException if the request fails
     */
    public <T> T postMultipart(String path, byte[] fileBytes, String fileName, String mimeType, Class<T> type) {
        String boundary = "----EPostakBoundary" + System.currentTimeMillis();

        StringBuilder sb = new StringBuilder();
        sb.append("--").append(boundary).append("\r\n");
        sb.append("Content-Disposition: form-data; name=\"file\"; filename=\"")
                .append(fileName).append("\"\r\n");
        sb.append("Content-Type: ").append(mimeType).append("\r\n\r\n");

        byte[] header = sb.toString().getBytes(StandardCharsets.UTF_8);
        byte[] footer = ("\r\n--" + boundary + "--\r\n").getBytes(StandardCharsets.UTF_8);
        byte[] body = new byte[header.length + fileBytes.length + footer.length];
        System.arraycopy(header, 0, body, 0, header.length);
        System.arraycopy(fileBytes, 0, body, header.length, fileBytes.length);
        System.arraycopy(footer, 0, body, header.length + fileBytes.length, footer.length);

        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(Duration.ofSeconds(120))
                .header("Authorization", "Bearer " + tokenManager.getAccessToken())
                .header("Content-Type", "multipart/form-data; boundary=" + boundary)
                .POST(HttpRequest.BodyPublishers.ofByteArray(body));
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }

        return execute(builder.build(), type);
    }

    /**
     * POST with {@code multipart/form-data} for batch file uploads.
     *
     * @param <T>   the response type
     * @param path  the API path (appended to base URL)
     * @param files list of files to upload
     * @param type  the response class to deserialize to
     * @return the deserialized response
     * @throws EPostakException if the request fails or multipart body construction fails
     */
    public <T> T postMultipartBatch(String path, List<FileUpload> files, Class<T> type) {
        String boundary = "----EPostakBoundary" + System.currentTimeMillis();

        java.io.ByteArrayOutputStream baos = new java.io.ByteArrayOutputStream();
        try {
            for (FileUpload file : files) {
                String header = "--" + boundary + "\r\n"
                        + "Content-Disposition: form-data; name=\"files\"; filename=\""
                        + file.fileName() + "\"\r\n"
                        + "Content-Type: " + file.mimeType() + "\r\n\r\n";
                baos.write(header.getBytes(StandardCharsets.UTF_8));
                baos.write(file.data());
                baos.write("\r\n".getBytes(StandardCharsets.UTF_8));
            }
            baos.write(("--" + boundary + "--\r\n").getBytes(StandardCharsets.UTF_8));
        } catch (IOException e) {
            throw new EPostakException(0, "Failed to build multipart body");
        }

        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(Duration.ofSeconds(120))
                .header("Authorization", "Bearer " + tokenManager.getAccessToken())
                .header("Content-Type", "multipart/form-data; boundary=" + boundary)
                .POST(HttpRequest.BodyPublishers.ofByteArray(baos.toByteArray()));
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }

        return execute(builder.build(), type);
    }

    /**
     * GET that returns raw bytes (for PDF downloads).
     *
     * @param path the API path (appended to base URL)
     * @return the raw response body as a byte array
     * @throws EPostakException if the request fails
     */
    public byte[] getBytes(String path) {
        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(TIMEOUT)
                .header("Authorization", "Bearer " + tokenManager.getAccessToken())
                .GET();
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }

        HttpResponse<byte[]> response;
        try {
            response = client.send(builder.build(), HttpResponse.BodyHandlers.ofByteArray());
        } catch (IOException | InterruptedException e) {
            if (e instanceof InterruptedException) Thread.currentThread().interrupt();
            throw new EPostakException(0, e.getMessage());
        }

        if (response.statusCode() >= 400) {
            handleError(response.statusCode(), new String(response.body(), StandardCharsets.UTF_8), response.headers());
        }
        return response.body();
    }

    /**
     * GET that returns the response body as a raw string (for UBL XML downloads).
     *
     * @param path the API path (appended to base URL)
     * @return the raw response body as a string
     * @throws EPostakException if the request fails
     */
    public String getString(String path) {
        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(TIMEOUT)
                .header("Authorization", "Bearer " + tokenManager.getAccessToken())
                .GET();
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }

        HttpResponse<String> response;
        try {
            response = client.send(builder.build(), HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
        } catch (IOException | InterruptedException e) {
            if (e instanceof InterruptedException) Thread.currentThread().interrupt();
            throw new EPostakException(0, e.getMessage());
        }

        if (response.statusCode() >= 400) {
            handleError(response.statusCode(), response.body(), response.headers());
        }
        return response.body();
    }

    /**
     * POST a raw string body with a custom {@code Content-Type} and deserialize the JSON response.
     * Used for endpoints that accept raw XML (e.g. document parsing, validation).
     *
     * @param <T>         the response type
     * @param path        the API path (appended to base URL)
     * @param body        the raw body string
     * @param contentType the {@code Content-Type} header value, e.g. {@code "application/xml"}
     * @param type        the response class to deserialize to
     * @return the deserialized response
     * @throws EPostakException if the request fails
     */
    public <T> T postRaw(String path, String body, String contentType, Class<T> type) {
        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(TIMEOUT)
                .header("Authorization", "Bearer " + tokenManager.getAccessToken())
                .header("Content-Type", contentType)
                .POST(HttpRequest.BodyPublishers.ofString(body, StandardCharsets.UTF_8));
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }
        return execute(builder.build(), type);
    }

    // -- internal -------------------------------------------------------------

    private <T> T request(String method, String path, Object body, Class<T> type) {
        return request(method, path, body, type, Map.of());
    }

    private <T> T request(String method, String path, Object body, Class<T> type, Map<String, String> extraHeaders) {
        return requestTyped(method, path, body, TypeToken.get(type), extraHeaders);
    }

    @SuppressWarnings("unchecked")
    private <T> T requestTyped(
            String method,
            String path,
            Object body,
            TypeToken<T> typeToken,
            Map<String, String> extraHeaders
    ) {
        HttpRequest.BodyPublisher publisher = (body != null)
                ? HttpRequest.BodyPublishers.ofString(GSON.toJson(body), StandardCharsets.UTF_8)
                : HttpRequest.BodyPublishers.noBody();

        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(TIMEOUT)
                .header("Authorization", "Bearer " + tokenManager.getAccessToken())
                .method(method, publisher);

        if (body != null) {
            builder.header("Content-Type", "application/json");
        }
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }
        for (Map.Entry<String, String> e : extraHeaders.entrySet()) {
            builder.header(e.getKey(), e.getValue());
        }

        return (T) executeTyped(builder.build(), typeToken.getType());
    }

    private <T> T execute(HttpRequest request, Class<T> type) {
        @SuppressWarnings("unchecked")
        T result = (T) executeTyped(request, type);
        return result;
    }

    private Object executeTyped(HttpRequest request, Type responseType) {
        boolean retryable = RETRYABLE_METHODS.contains(request.method());

        for (int attempt = 0; attempt <= maxRetries; attempt++) {
            HttpResponse<String> response;
            try {
                response = client.send(request, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
            } catch (IOException | InterruptedException e) {
                if (e instanceof InterruptedException) Thread.currentThread().interrupt();
                throw new EPostakException(0, e.getMessage());
            }

            int status = response.statusCode();

            // Retry on 429 or 5xx for safe methods
            if (retryable && attempt < maxRetries && (status == 429 || status >= 500)) {
                long delayMs = calculateDelayMs(attempt, response);
                try {
                    Thread.sleep(delayMs);
                } catch (InterruptedException ie) {
                    Thread.currentThread().interrupt();
                    throw new EPostakException(0, "Retry interrupted");
                }
                continue;
            }

            if (status >= 400) {
                handleError(status, response.body(), response.headers());
            }

            // 204 No Content or empty body
            if (status == 204 || response.body() == null || response.body().isEmpty()) {
                return null;
            }

            return GSON.fromJson(response.body(), responseType);
        }

        throw new EPostakException(0, "Max retries exceeded");
    }

    /**
     * Calculate the backoff delay in milliseconds.
     * Uses exponential backoff with jitter: min(base_delay * 2^attempt + jitter, 30s).
     * Respects the Retry-After header on 429 responses.
     */
    private long calculateDelayMs(int attempt, HttpResponse<?> response) {
        double baseDelay = 0.5;
        double maxDelay = 30.0;

        if (response.statusCode() == 429) {
            var retryAfter = response.headers().firstValue("Retry-After");
            if (retryAfter.isPresent()) {
                try {
                    double seconds = Double.parseDouble(retryAfter.get());
                    return (long) (Math.min(seconds, maxDelay) * 1000);
                } catch (NumberFormatException ignored) {
                    // Fall through to exponential backoff
                }
            }
        }

        double jitter = ThreadLocalRandom.current().nextDouble(); // 0–1s of jitter
        double delay = Math.min(baseDelay * Math.pow(2, attempt) + jitter, maxDelay);
        return (long) (delay * 1000);
    }

    private void handleError(int status, String body, HttpHeaders headers) {
        String message = "API request failed";
        String code = null;
        Object details = null;
        String type = null;
        String title = null;
        String detail = null;
        String instance = null;
        String requestId = null;
        String requiredScope = null;

        try {
            JsonElement parsed = JsonParser.parseString(body);
            if (parsed.isJsonObject()) {
                JsonObject obj = parsed.getAsJsonObject();
                JsonElement errorEl = obj.get("error");

                // RFC 7807: { type, title, status, detail, instance, ... } — no `error`.
                boolean isProblem = errorEl == null && (obj.has("title") || obj.has("detail"));

                if (isProblem) {
                    if (obj.has("type") && obj.get("type").isJsonPrimitive()) type = obj.get("type").getAsString();
                    if (obj.has("title") && obj.get("title").isJsonPrimitive()) {
                        title = obj.get("title").getAsString();
                        message = title;
                    }
                    if (obj.has("detail") && obj.get("detail").isJsonPrimitive()) {
                        detail = obj.get("detail").getAsString();
                        if (title == null) message = detail;
                    }
                    if (obj.has("instance") && obj.get("instance").isJsonPrimitive()) instance = obj.get("instance").getAsString();
                    if (obj.has("code") && obj.get("code").isJsonPrimitive()) code = obj.get("code").getAsString();
                    if (obj.has("errors")) details = GSON.fromJson(obj.get("errors"), Object.class);
                } else if (errorEl != null) {
                    if (errorEl.isJsonPrimitive()) {
                        message = errorEl.getAsString();
                    } else if (errorEl.isJsonObject()) {
                        JsonObject errorObj = errorEl.getAsJsonObject();
                        if (errorObj.has("message")) {
                            message = errorObj.get("message").getAsString();
                        }
                        if (errorObj.has("code")) {
                            code = errorObj.get("code").getAsString();
                        }
                        if (errorObj.has("details")) {
                            details = GSON.fromJson(errorObj.get("details"), Object.class);
                        }
                        if (errorObj.has("requestId") && errorObj.get("requestId").isJsonPrimitive()) {
                            requestId = errorObj.get("requestId").getAsString();
                        }
                        if (errorObj.has("required_scope") && errorObj.get("required_scope").isJsonPrimitive()) {
                            requiredScope = errorObj.get("required_scope").getAsString();
                        }
                    }
                } else if (obj.has("message") && obj.get("message").isJsonPrimitive()) {
                    message = obj.get("message").getAsString();
                }

                // Body-level requestId / required_scope.
                if (requestId == null && obj.has("requestId") && obj.get("requestId").isJsonPrimitive()) {
                    requestId = obj.get("requestId").getAsString();
                }
                if (requiredScope == null && obj.has("required_scope") && obj.get("required_scope").isJsonPrimitive()) {
                    requiredScope = obj.get("required_scope").getAsString();
                }
            }
        } catch (Exception ignored) {
            // Keep defaults
        }

        // Header fallbacks for requestId + WWW-Authenticate-derived requiredScope.
        if (headers != null) {
            if (requestId == null) {
                requestId = headers.firstValue("x-request-id").orElse(null);
            }
            if (requiredScope == null) {
                String www = headers.firstValue("www-authenticate").orElse(null);
                requiredScope = parseRequiredScopeFromHeader(www);
            }
        }

        throw new EPostakException(status, message, code, details, type, title, detail, instance, requestId, requiredScope);
    }

    /**
     * Extract the {@code scope="..."} value from a
     * {@code WWW-Authenticate: Bearer error="insufficient_scope" scope="..."}
     * header. Returns {@code null} if the header is absent or refers to a
     * different OAuth error.
     */
    private static String parseRequiredScopeFromHeader(String www) {
        if (www == null || www.isEmpty()) return null;
        if (!www.toLowerCase().contains("insufficient_scope")) return null;
        int idx = www.toLowerCase().indexOf("scope");
        while (idx >= 0) {
            int eq = www.indexOf('=', idx);
            if (eq < 0) return null;
            int q1 = www.indexOf('"', eq);
            if (q1 < 0) return null;
            int q2 = www.indexOf('"', q1 + 1);
            if (q2 < 0) return null;
            String key = www.substring(idx, eq).trim().toLowerCase();
            if (key.equals("scope")) {
                return www.substring(q1 + 1, q2);
            }
            idx = www.toLowerCase().indexOf("scope", q2 + 1);
        }
        return null;
    }

    // -- utility methods ------------------------------------------------------

    /**
     * URL-encode a string value using UTF-8.
     *
     * @param value the value to encode
     * @return the URL-encoded string
     */
    public static String encode(String value) {
        return URLEncoder.encode(value, StandardCharsets.UTF_8);
    }

    /**
     * Build a URL query string from a map of parameters. {@code null} values are skipped.
     *
     * @param params the parameter map (keys are parameter names, values are parameter values)
     * @return the query string starting with {@code "?"}, or an empty string if no non-null values
     */
    public static String buildQuery(Map<String, Object> params) {
        if (params == null || params.isEmpty()) return "";
        StringBuilder sb = new StringBuilder("?");
        boolean first = true;
        for (Map.Entry<String, Object> entry : params.entrySet()) {
            if (entry.getValue() == null) continue;
            if (!first) sb.append('&');
            sb.append(encode(entry.getKey())).append('=').append(encode(String.valueOf(entry.getValue())));
            first = false;
        }
        return first ? "" : sb.toString();
    }

    /**
     * Simple record for batch file uploads.
     *
     * @param data     raw file content as a byte array
     * @param fileName the file name with extension
     * @param mimeType the MIME type of the file
     */
    public record FileUpload(byte[] data, String fileName, String mimeType) {}
}
