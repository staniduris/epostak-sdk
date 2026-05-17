package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.Account;

import java.util.Map;

/**
 * Account and firm information.
 * <p>
 * Provides access to the current account's firm details, subscription plan,
 * and document usage counters.
 * <p>
 * For key introspection, OAuth token minting, and key rotation see
 * {@code client.auth()}.
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

    @SuppressWarnings("unchecked")
    public Map<String, Object> licenseInfo() {
        return http.get("/licenses/info", Map.class);
    }
}
