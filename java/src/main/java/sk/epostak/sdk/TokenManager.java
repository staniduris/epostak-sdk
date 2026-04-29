package sk.epostak.sdk;

import com.google.gson.Gson;
import sk.epostak.sdk.models.TokenResponse;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Thread-safe OAuth token manager for the ePošťák SAPI.
 * <p>
 * Mints a JWT via {@code POST /sapi/v1/auth/token} on first use, caches it,
 * and auto-refreshes via {@code POST /sapi/v1/auth/renew} 60 seconds before
 * expiry. If renewal fails, falls back to a full re-mint.
 */
public final class TokenManager {

    private static final Gson GSON = new Gson();
    private static final Duration TIMEOUT = Duration.ofSeconds(30);
    private static final long REFRESH_BUFFER_MS = 60_000;

    private final String clientId;
    private final String clientSecret;
    private final String baseUrl;
    private final String firmId;

    private final java.net.http.HttpClient jdkClient;

    private String accessToken;
    private String refreshToken;
    private long expiresAtMs;

    /**
     * @param clientId     the API key (e.g. {@code sk_live_*} or {@code sk_int_*})
     * @param clientSecret the API secret (same value as clientId for ePošťák keys)
     * @param baseUrl      the full base URL including {@code /api/v1} suffix
     * @param firmId       optional firm UUID for integrator keys, or {@code null}
     */
    public TokenManager(String clientId, String clientSecret, String baseUrl, String firmId) {
        this.clientId = clientId;
        this.clientSecret = clientSecret;
        this.baseUrl = baseUrl;
        this.firmId = firmId;
        this.jdkClient = java.net.http.HttpClient.newBuilder()
                .connectTimeout(TIMEOUT)
                .build();
    }

    /**
     * Returns a valid JWT access token. Mints or refreshes automatically.
     * Thread-safe via synchronized.
     *
     * @return a valid JWT bearer token
     * @throws EPostakException if minting/renewal fails
     */
    public synchronized String getAccessToken() {
        if (accessToken != null && System.currentTimeMillis() < expiresAtMs - REFRESH_BUFFER_MS) {
            return accessToken;
        }

        if (refreshToken != null && accessToken != null) {
            try {
                doRenew();
                return accessToken;
            } catch (Exception ignored) {
                // refresh failed — fall through to full mint
            }
        }

        doMint();
        return accessToken;
    }

    /**
     * Returns the SAPI base URL (strips {@code /api/v1} suffix from baseUrl).
     */
    private String sapiBaseUrl() {
        return baseUrl.replaceAll("/api/v1/?$", "");
    }

    private void doMint() {
        String url = sapiBaseUrl() + "/sapi/v1/auth/token";

        Map<String, String> body = new LinkedHashMap<>();
        body.put("grant_type", "client_credentials");
        body.put("client_id", clientId);
        body.put("client_secret", clientSecret);

        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .timeout(TIMEOUT)
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(GSON.toJson(body), StandardCharsets.UTF_8));
        if (firmId != null) {
            builder.header("X-Firm-Id", firmId);
        }

        TokenResponse resp = send(builder.build());
        applyTokenResponse(resp);
    }

    private void doRenew() {
        String url = sapiBaseUrl() + "/sapi/v1/auth/renew";

        Map<String, String> body = new LinkedHashMap<>();
        body.put("grant_type", "refresh_token");
        body.put("refresh_token", refreshToken);

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .timeout(TIMEOUT)
                .header("Content-Type", "application/json")
                .header("Authorization", "Bearer " + accessToken)
                .POST(HttpRequest.BodyPublishers.ofString(GSON.toJson(body), StandardCharsets.UTF_8))
                .build();

        TokenResponse resp = send(request);
        applyTokenResponse(resp);
    }

    private TokenResponse send(HttpRequest request) {
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

        return GSON.fromJson(response.body(), TokenResponse.class);
    }

    private void applyTokenResponse(TokenResponse data) {
        this.accessToken = data.accessToken();
        this.refreshToken = data.refreshToken();
        this.expiresAtMs = System.currentTimeMillis() + data.expiresIn() * 1000;
    }
}
