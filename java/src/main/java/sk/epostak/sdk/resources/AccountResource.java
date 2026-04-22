package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.Account;
import sk.epostak.sdk.models.AuthStatusResponse;
import sk.epostak.sdk.models.RotateSecretResponse;

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
     * Describe the authenticated API key: its permissions, the firm it resolves
     * to, the current plan (with expiry and active flag), the applicable rate
     * limit, and — for integrator subkeys — the parent integrator.
     * <p>
     * Useful as a lightweight health check to verify credentials work without
     * consuming a document quota.
     *
     * <pre>{@code
     * AuthStatusResponse status = client.account().status();
     * System.out.println("Key: " + status.key().prefix());
     * System.out.println("Plan: " + status.plan().name() + " (active=" + status.plan().active() + ")");
     * System.out.println("Rate limit: " + status.rateLimit().perMinute() + "/" + status.rateLimit().window());
     * }</pre>
     *
     * @return the auth status snapshot
     * @throws sk.epostak.sdk.EPostakException if authentication fails or the request fails
     */
    public AuthStatusResponse status() {
        return http.get("/auth/status", AuthStatusResponse.class);
    }

    /**
     * Rotate the current API key. The new key value is returned only once —
     * store it immediately. The previous key is deactivated server-side; any
     * in-flight requests signed with it will be rejected with HTTP 401.
     * <p>
     * Not available for integrator subkeys ({@code sk_int_*}); the server
     * responds with HTTP 403 in that case.
     *
     * <pre>{@code
     * RotateSecretResponse rotated = client.account().rotateSecret();
     * saveSecurely(rotated.key()); // only chance to see it
     * }</pre>
     *
     * @return the rotation result, including the new key value
     * @throws sk.epostak.sdk.EPostakException if the key cannot be rotated (403 for integrator keys) or the request fails
     */
    public RotateSecretResponse rotateSecret() {
        return http.post("/auth/rotate-secret", null, RotateSecretResponse.class);
    }
}
