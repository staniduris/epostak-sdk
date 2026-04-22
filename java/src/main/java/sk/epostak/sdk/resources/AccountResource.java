package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.Account;
import sk.epostak.sdk.models.AuthStatusResponse;
import sk.epostak.sdk.models.RotateSecretResponse;

import java.util.Map;

/**
 * Account and firm information.
 * <p>
 * Provides access to the current account's firm details, subscription plan,
 * and document usage counters.
 * <p>
 * Access via {@code client.account()}.
 *
 * <pre>{@code
 * Account acct = client.account().get();
 * System.out.println("Firm: " + acct.firm().name());
 * System.out.println("Plan: " + acct.plan().name());
 * System.out.println("Outbound usage: " + acct.usage().outbound());
 * }</pre>
 */
public final class AccountResource {

    private final HttpClient http;

    /**
     * Creates a new account resource.
     *
     * @param http the HTTP client used for API communication
     */
    public AccountResource(HttpClient http) {
        this.http = http;
    }

    /**
     * Get the current account information, including firm details, subscription
     * plan, and document usage counters.
     *
     * @return the account details
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public Account get() {
        return http.get("/account", Account.class);
    }

    /**
     * Describe the authenticated API key, the firm it resolves to, the current
     * subscription plan, applicable rate limit, and optional integrator info.
     * <p>
     * Useful as a lightweight health check to verify credentials work without
     * consuming a document quota.
     *
     * <pre>{@code
     * AuthStatusResponse status = client.account().status();
     * System.out.println("Acting as: " + status.firm().name());
     * System.out.println("Plan: " + status.plan());
     * System.out.println("Remaining: " + status.rateLimit().remaining());
     * }</pre>
     *
     * @return the auth status snapshot
     * @throws sk.epostak.sdk.EPostakException if authentication fails or the request fails
     */
    public AuthStatusResponse status() {
        return http.post("/auth/status", Map.of(), AuthStatusResponse.class);
    }

    /**
     * Rotate the current API key's secret. The new key is returned only once —
     * store it immediately. The previous secret is invalidated server-side.
     * <p>
     * Not available for integrator subkeys ({@code sk_int_*}); the server
     * responds with HTTP 409 in that case.
     *
     * <pre>{@code
     * RotateSecretResponse rotated = client.account().rotateSecret();
     * saveSecurely(rotated.key()); // only chance to see it
     * }</pre>
     *
     * @return the rotation result, including the new key value
     * @throws sk.epostak.sdk.EPostakException if the key cannot be rotated (e.g. 409 for integrator keys) or the request fails
     */
    public RotateSecretResponse rotateSecret() {
        return http.post("/auth/rotate-secret", Map.of(), RotateSecretResponse.class);
    }
}
