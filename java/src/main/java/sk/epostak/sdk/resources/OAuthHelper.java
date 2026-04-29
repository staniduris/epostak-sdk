package sk.epostak.sdk.resources;

import com.google.gson.Gson;
import sk.epostak.sdk.EPostakException;
import sk.epostak.sdk.models.TokenResponse;

import java.io.IOException;
import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.time.Duration;
import java.util.Base64;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Stateless helpers for the <strong>integrator-initiated</strong> OAuth
 * {@code authorization_code} + PKCE flow. Use these from your own backend when
 * you want to onboard an end-user firm into ePošťák from inside your own
 * application — the user clicks a "Connect ePošťák" button in your UI, lands
 * on the ePošťák {@code /oauth/authorize} consent page, and ePošťák redirects
 * back to your {@code redirect_uri} with a {@code code}.
 * <p>
 * This is independent of the regular {@link AuthResource#token(String)} flow
 * (which uses {@code client_credentials}). Pick one or the other depending on
 * how the firm is linked to you.
 * <p>
 * The OAuth token endpoint lives at
 * {@code https://epostak.sk/api/oauth/token} — <strong>not</strong> under
 * {@code /api/v1} — so this helper bypasses the configured ePošťák base URL.
 * <p>
 * Named {@code OAuthHelper} (rather than {@code OAuth}) to avoid clashing with
 * {@code javax.security.auth.oauth} / Jakarta types in classpaths that pull
 * those in.
 *
 * <pre>{@code
 * // 1. On every onboarding attempt, generate a fresh PKCE pair.
 * OAuthHelper.PkcePair pair = OAuthHelper.generatePkce();
 * sessions.put(reqSessionId, pair.codeVerifier());
 *
 * // 2. Build the authorize URL and redirect the user.
 * String url = OAuthHelper.buildAuthorizeUrl(
 *     System.getenv("EPOSTAK_OAUTH_CLIENT_ID"),
 *     "https://your-app.com/oauth/epostak/callback",
 *     pair.codeChallenge(),
 *     reqSessionId,
 *     "firm:read firm:manage document:send",
 *     null
 * );
 * res.sendRedirect(url);
 *
 * // 3. On callback, exchange the code for a token pair.
 * TokenResponse tokens = OAuthHelper.exchangeCode(
 *     req.getParameter("code"),
 *     sessions.get(req.getParameter("state")),
 *     System.getenv("EPOSTAK_OAUTH_CLIENT_ID"),
 *     System.getenv("EPOSTAK_OAUTH_CLIENT_SECRET"),
 *     "https://your-app.com/oauth/epostak/callback",
 *     null
 * );
 * }</pre>
 */
public final class OAuthHelper {

    /** Default origin for ePošťák OAuth endpoints. Override for staging. */
    public static final String DEFAULT_ORIGIN = "https://epostak.sk";

    private static final Gson GSON = new Gson();
    private static final Duration TIMEOUT = Duration.ofSeconds(30);
    private static final SecureRandom RANDOM = new SecureRandom();

    private OAuthHelper() {
        // utility class
    }

    /**
     * PKCE code-verifier + S256 code-challenge pair returned by
     * {@link #generatePkce()}.
     *
     * @param codeVerifier  43-char base64url string (≈256 bits of entropy);
     *                      keep server-side, never send to the browser.
     * @param codeChallenge {@code base64url(SHA256(codeVerifier))}; passed in
     *                      the authorize URL.
     */
    public record PkcePair(String codeVerifier, String codeChallenge) {}

    /**
     * Generate a fresh PKCE code-verifier + S256 code-challenge pair.
     *
     * @return verifier and challenge
     */
    public static PkcePair generatePkce() {
        byte[] entropy = new byte[32];
        RANDOM.nextBytes(entropy);
        String codeVerifier = Base64.getUrlEncoder().withoutPadding().encodeToString(entropy);
        try {
            byte[] hash = MessageDigest.getInstance("SHA-256")
                    .digest(codeVerifier.getBytes(StandardCharsets.US_ASCII));
            String codeChallenge = Base64.getUrlEncoder().withoutPadding().encodeToString(hash);
            return new PkcePair(codeVerifier, codeChallenge);
        } catch (NoSuchAlgorithmException e) {
            // SHA-256 is mandated by every JRE.
            throw new IllegalStateException("SHA-256 unavailable", e);
        }
    }

    /**
     * Build a {@code /oauth/authorize} URL the integrator can redirect the
     * user to. Always sets {@code response_type=code} and
     * {@code code_challenge_method=S256}.
     *
     * @param clientId      registered OAuth client id
     * @param redirectUri   exact-match registered redirect URI
     * @param codeChallenge from {@link #generatePkce()}
     * @param state         CSRF/session token; echoed back on the callback
     * @param scope         optional space-separated scope subset, or {@code null}
     * @param origin        override the host (defaults to {@link #DEFAULT_ORIGIN}),
     *                      or {@code null}
     * @return absolute URL string
     */
    public static String buildAuthorizeUrl(
            String clientId,
            String redirectUri,
            String codeChallenge,
            String state,
            String scope,
            String origin
    ) {
        StringBuilder query = new StringBuilder();
        appendParam(query, "client_id", clientId);
        appendParam(query, "redirect_uri", redirectUri);
        appendParam(query, "response_type", "code");
        appendParam(query, "code_challenge", codeChallenge);
        appendParam(query, "code_challenge_method", "S256");
        appendParam(query, "state", state);
        if (scope != null && !scope.isEmpty()) {
            appendParam(query, "scope", scope);
        }
        String base = (origin == null || origin.isEmpty()) ? DEFAULT_ORIGIN : origin;
        if (base.endsWith("/")) base = base.substring(0, base.length() - 1);
        return base + "/oauth/authorize?" + query;
    }

    /**
     * Exchange an authorization {@code code} for an access + refresh token
     * pair on the OAuth token endpoint. Hits {@code ${origin}/api/oauth/token}
     * directly — does not route through the configured ePošťák base URL.
     * <p>
     * The returned access token is a 15-minute JWT; the refresh token is
     * 30-day rotating. Persist both server-side keyed by your firm record.
     *
     * @param code         the authorization code from the callback
     * @param codeVerifier the verifier paired with the {@code code_challenge}
     *                     used when starting the flow
     * @param clientId     integrator OAuth client id
     * @param clientSecret integrator OAuth client secret
     * @param redirectUri  must match the URI used in
     *                     {@link #buildAuthorizeUrl}
     * @param origin       override the host, or {@code null}
     * @return access + refresh token pair
     * @throws EPostakException on a non-2xx response or transport error
     */
    public static TokenResponse exchangeCode(
            String code,
            String codeVerifier,
            String clientId,
            String clientSecret,
            String redirectUri,
            String origin
    ) {
        String base = (origin == null || origin.isEmpty()) ? DEFAULT_ORIGIN : origin;
        if (base.endsWith("/")) base = base.substring(0, base.length() - 1);

        Map<String, String> form = new LinkedHashMap<>();
        form.put("grant_type", "authorization_code");
        form.put("code", code);
        form.put("code_verifier", codeVerifier);
        form.put("client_id", clientId);
        form.put("client_secret", clientSecret);
        form.put("redirect_uri", redirectUri);

        StringBuilder body = new StringBuilder();
        for (Map.Entry<String, String> entry : form.entrySet()) {
            if (body.length() > 0) body.append('&');
            body.append(URLEncoder.encode(entry.getKey(), StandardCharsets.UTF_8));
            body.append('=');
            body.append(URLEncoder.encode(entry.getValue(), StandardCharsets.UTF_8));
        }

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(base + "/api/oauth/token"))
                .timeout(TIMEOUT)
                .header("Content-Type", "application/x-www-form-urlencoded")
                .header("Accept", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(body.toString(), StandardCharsets.UTF_8))
                .build();

        HttpClient client = HttpClient.newBuilder().connectTimeout(TIMEOUT).build();
        HttpResponse<String> response;
        try {
            response = client.send(request, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
        } catch (IOException | InterruptedException e) {
            if (e instanceof InterruptedException) Thread.currentThread().interrupt();
            throw new EPostakException(0, e.getMessage());
        }

        int status = response.statusCode();
        String payload = response.body();
        if (status >= 400) {
            throw new EPostakException(status, payload == null || payload.isEmpty() ? "OAuth token exchange failed" : payload);
        }
        if (payload == null || payload.isEmpty()) {
            return null;
        }
        return GSON.fromJson(payload, TokenResponse.class);
    }

    private static void appendParam(StringBuilder sb, String key, String value) {
        if (sb.length() > 0) sb.append('&');
        sb.append(URLEncoder.encode(key, StandardCharsets.UTF_8));
        sb.append('=');
        sb.append(URLEncoder.encode(value, StandardCharsets.UTF_8));
    }
}
