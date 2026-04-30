package sk.epostak.sdk.resources;

import com.google.gson.Gson;
import sk.epostak.sdk.EPostakException;
import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.AuthStatusResponse;
import sk.epostak.sdk.models.IpAllowlistResponse;
import sk.epostak.sdk.models.RevokeResponse;
import sk.epostak.sdk.models.RotateSecretResponse;
import sk.epostak.sdk.models.TokenResponse;
import sk.epostak.sdk.models.TokenStatusResponse;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Resource for the OAuth {@code client_credentials} flow and key management
 * (Wave 3.1+). Mints short-lived JWT access tokens, exchanges refresh tokens,
 * revokes tokens, introspects the calling key, rotates the secret, and reads
 * or replaces the per-key IP allowlist.
 * <p>
 * Access via {@code client.auth()}.
 *
 * <pre>{@code
 * EPostak client = EPostak.builder()
 *     .clientId("sk_live_xxxxx")
 *     .clientSecret("sk_live_xxxxx")
 *     .build();
 *
 * TokenResponse tokens = client.auth().token("sk_live_xxxxx", "sk_live_xxxxx");
 * System.out.println(tokens.accessToken() + " (expires_in=" + tokens.expiresIn() + ")");
 *
 * // Later, before the access token expires:
 * TokenResponse renewed = client.auth().renew(tokens.refreshToken());
 *
 * // On logout / key rotation:
 * client.auth().revoke(tokens.refreshToken(), "refresh_token");
 * }</pre>
 */
public final class AuthResource {

    private static final Gson GSON = new Gson();
    private static final Duration TIMEOUT = Duration.ofSeconds(30);

    private final HttpClient http;
    private final IpAllowlistResource ipAllowlist;

    /**
     * Creates a new auth resource.
     *
     * @param http the HTTP client used for API communication
     */
    public AuthResource(HttpClient http) {
        this.http = http;
        this.ipAllowlist = new IpAllowlistResource(http);
    }

    /**
     * Per-key IP allowlist sub-resource.
     *
     * @return the IP allowlist resource
     */
    public IpAllowlistResource ipAllowlist() {
        return ipAllowlist;
    }

    /**
     * Mint an OAuth access token via the {@code client_credentials} grant.
     * <p>
     * The JWT returned is not firm-scoped; use the {@code X-Firm-Id} header
     * on subsequent API calls to scope requests to a specific firm.
     *
     * @param clientId     the OAuth client ID (API key)
     * @param clientSecret the OAuth client secret
     * @param scope        optional space-separated scope subset, or {@code null} for the key's own scopes
     * @return the access + refresh token pair, scope, and {@code expires_in} (seconds)
     * @throws EPostakException if the request fails
     */
    public TokenResponse token(String clientId, String clientSecret, String scope) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("grant_type", "client_credentials");
        body.put("client_id", clientId);
        body.put("client_secret", clientSecret);
        if (scope != null) body.put("scope", scope);

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(http.getBaseUrl() + "/auth/token"))
                .timeout(TIMEOUT)
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(GSON.toJson(body), StandardCharsets.UTF_8))
                .build();

        return sendDirect(request, TokenResponse.class);
    }

    /**
     * Mint a token using just clientId + clientSecret (no scope subset).
     *
     * @param clientId     the OAuth client ID (API key)
     * @param clientSecret the OAuth client secret
     * @return the access + refresh token pair
     * @throws EPostakException if the request fails
     */
    public TokenResponse token(String clientId, String clientSecret) {
        return token(clientId, clientSecret, null);
    }

    /**
     * Exchange a refresh token for a new access + refresh pair. The old
     * refresh token is invalidated server-side, so always replace your
     * stored refresh token with the value returned by this call.
     *
     * @param refreshToken the refresh token returned by a previous token mint or renew
     * @return a fresh token pair
     * @throws EPostakException if the request fails
     */
    public TokenResponse renew(String refreshToken) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("grant_type", "refresh_token");
        body.put("refresh_token", refreshToken);
        return http.post("/auth/renew", body, TokenResponse.class);
    }

    /**
     * Revoke an access or refresh token. Idempotent — a 200 is returned even
     * if the token is unknown or already revoked, so this is safe to call
     * unconditionally on logout. Pass {@code tokenTypeHint} when you know
     * which variant the token is; the server will skip the auto-detect path.
     *
     * @param token         the token to revoke
     * @param tokenTypeHint {@code "access_token"} or {@code "refresh_token"}, or {@code null} to auto-detect
     * @return the revocation result
     * @throws EPostakException if the request fails
     */
    public RevokeResponse revoke(String token, String tokenTypeHint) {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("token", token);
        if (tokenTypeHint != null) body.put("token_type_hint", tokenTypeHint);
        return http.post("/auth/revoke", body, RevokeResponse.class);
    }

    /**
     * Revoke a token without specifying a type hint (server auto-detects).
     *
     * @param token the token to revoke
     * @return the revocation result
     * @throws EPostakException if the request fails
     */
    public RevokeResponse revoke(String token) {
        return revoke(token, null);
    }

    /**
     * Introspect the calling API key without revealing the plaintext secret.
     * Returns the key metadata, the firm it is bound to, the current plan,
     * and — for integrator keys — the integrator summary.
     *
     * @return the auth status snapshot
     * @throws EPostakException if the request fails
     */
    public AuthStatusResponse status() {
        return http.get("/auth/status", AuthStatusResponse.class);
    }

    /**
     * Introspect the calling JWT access token. Returns token validity,
     * the firm it is scoped to, the key type, granted scopes, and expiry
     * timing. This endpoint is also available at the SAPI alias
     * {@code /sapi/v1/auth/status}.
     *
     * <pre>{@code
     * TokenStatusResponse ts = client.auth().tokenStatus();
     * if (ts.shouldRefresh()) {
     *     TokenResponse renewed = client.auth().renew(savedRefreshToken);
     * }
     * }</pre>
     *
     * @return the token status including {@code firmId}, {@code keyType}, and {@code scope}
     * @throws EPostakException if the token is invalid, expired, or revoked
     */
    public TokenStatusResponse tokenStatus() {
        return http.get("/auth/token/status", TokenStatusResponse.class);
    }

    /**
     * Rotate the calling API key. The previous key is deactivated immediately
     * and the new plaintext key is returned ONCE — store it in your secret
     * manager before continuing. Integrator keys ({@code sk_int_*}) are
     * rejected with HTTP 403; rotate those through the integrator dashboard
     * instead.
     *
     * @return the new plaintext key + prefix + confirmation message
     * @throws EPostakException if the rotation is rejected (403 for integrator keys) or the request fails
     */
    public RotateSecretResponse rotateSecret() {
        return http.post("/auth/rotate-secret", null, RotateSecretResponse.class);
    }

    // -- helpers --------------------------------------------------------------

    /**
     * Send a fully-built HttpRequest using a private JDK client and parse JSON.
     * Used for the {@code /auth/token} call where the bearer key and firm id
     * may differ from the client's defaults.
     */
    private <T> T sendDirect(HttpRequest request, Class<T> type) {
        java.net.http.HttpClient jdk = java.net.http.HttpClient.newBuilder()
                .connectTimeout(TIMEOUT)
                .build();
        HttpResponse<String> response;
        try {
            response = jdk.send(request, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
        } catch (IOException | InterruptedException e) {
            if (e instanceof InterruptedException) Thread.currentThread().interrupt();
            throw new EPostakException(0, e.getMessage());
        }
        int status = response.statusCode();
        if (status >= 400) {
            throw new EPostakException(status, response.body());
        }
        if (status == 204 || response.body() == null || response.body().isEmpty()) {
            return null;
        }
        return GSON.fromJson(response.body(), type);
    }

    /**
     * Sub-resource for managing the per-key IP allowlist. An empty list means
     * no IP restriction; non-empty means callers from non-matching IPs are
     * rejected with HTTP 403.
     */
    public static final class IpAllowlistResource {

        private final HttpClient http;

        IpAllowlistResource(HttpClient http) {
            this.http = http;
        }

        /**
         * Read the current IP allowlist for the calling API key.
         *
         * @return the current allowlist (possibly empty)
         * @throws EPostakException if the request fails
         */
        public IpAllowlistResponse get() {
            return http.get("/auth/ip-allowlist", IpAllowlistResponse.class);
        }

        /**
         * Replace the IP allowlist for the calling API key. Pass an empty
         * list to clear the restriction. Maximum 50 entries; each must be a
         * bare IP or a valid CIDR.
         *
         * @param cidrs IPs / CIDR blocks to allow (max 50)
         * @return the persisted allowlist as the server stored it
         * @throws EPostakException if the request fails
         */
        public IpAllowlistResponse update(List<String> cidrs) {
            Map<String, Object> body = new LinkedHashMap<>();
            body.put("ip_allowlist", cidrs);
            return http.put("/auth/ip-allowlist", body, IpAllowlistResponse.class);
        }
    }
}
