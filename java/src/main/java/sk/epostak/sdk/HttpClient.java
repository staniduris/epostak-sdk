package sk.epostak.sdk;

import com.google.gson.Gson;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import java.io.IOException;
import java.net.URI;
import java.net.URLEncoder;
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

    /** Base URL for API requests, e.g. {@code "https://epostak.sk/api/enterprise"}. */
    private final String baseUrl;
    /** Bearer token for authentication. */
    private final String apiKey;
    /** Optional firm UUID sent as {@code X-Firm-Id} header. */
    private final String firmId;
    /** Maximum number of retries on 429/5xx for GET/DELETE requests. */
    private final int maxRetries;
    /** Underlying JDK HTTP client. */
    private final java.net.http.HttpClient client;

    /**
     * Creates a new HTTP client.
     *
     * @param baseUrl    the API base URL
     * @param apiKey     the API key for Bearer authentication
     * @param firmId     optional firm UUID for the {@code X-Firm-Id} header, or {@code null}
     * @param maxRetries maximum number of retries on 429/5xx (default 3)
     */
    HttpClient(String baseUrl, String apiKey, String firmId, int maxRetries) {
        this.baseUrl = baseUrl;
        this.apiKey = apiKey;
        this.firmId = firmId;
        this.maxRetries = maxRetries;
        this.client = java.net.http.HttpClient.newBuilder()
                .connectTimeout(TIMEOUT)
                .build();
    }

    /**
     * Creates a new HTTP client with default retry count (3).
     *
     * @param baseUrl the API base URL
     * @param apiKey  the API key for Bearer authentication
     * @param firmId  optional firm UUID for the {@code X-Firm-Id} header, or {@code null}
     */
    HttpClient(String baseUrl, String apiKey, String firmId) {
        this(baseUrl, apiKey, firmId, 3);
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
                .header("Authorization", "Bearer " + apiKey)
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
                .header("Authorization", "Bearer " + apiKey)
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
                .header("Authorization", "Bearer " + apiKey)
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
            handleError(response.statusCode(), new String(response.body(), StandardCharsets.UTF_8));
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
                .header("Authorization", "Bearer " + apiKey)
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
            handleError(response.statusCode(), response.body());
        }
        return response.body();
    }

    // -- internal -------------------------------------------------------------

    private <T> T request(String method, String path, Object body, Class<T> type) {
        HttpRequest.BodyPublisher publisher = (body != null)
                ? HttpRequest.BodyPublishers.ofString(GSON.toJson(body), StandardCharsets.UTF_8)
                : HttpRequest.BodyPublishers.noBody();

        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(baseUrl + path))
                .timeout(TIMEOUT)
                .header("Authorization", "Bearer " + apiKey)
                .method(method, publisher);

        if (body != null) {
            builder.header("Content-Type", "application/json");
        }
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }

        return execute(builder.build(), type);
    }

    private <T> T execute(HttpRequest request, Class<T> type) {
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
                handleError(status, response.body());
            }

            // 204 No Content or empty body
            if (status == 204 || response.body() == null || response.body().isEmpty()) {
                return null;
            }

            return GSON.fromJson(response.body(), type);
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

    private void handleError(int status, String body) {
        String message = "API request failed";
        String code = null;
        Object details = null;

        try {
            JsonElement parsed = JsonParser.parseString(body);
            if (parsed.isJsonObject()) {
                JsonObject obj = parsed.getAsJsonObject();
                JsonElement errorEl = obj.get("error");
                if (errorEl != null) {
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
                    }
                }
            }
        } catch (Exception ignored) {
            // Keep defaults
        }

        throw new EPostakException(status, message, code, details);
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
