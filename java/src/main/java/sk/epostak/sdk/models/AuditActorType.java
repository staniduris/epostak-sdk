package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Actor type recorded against an audit row. Wire format is camelCase, kept
 * stable so audit JSON forwarded to SIEMs remains identical to the API.
 */
public enum AuditActorType {
    /** Logged-in dashboard user. */
    @SerializedName("user") USER,
    /** Direct {@code sk_live_*} API key. */
    @SerializedName("apiKey") API_KEY,
    /** Integrator {@code sk_int_*} API key. */
    @SerializedName("integratorKey") INTEGRATOR_KEY,
    /** System-generated event (cron jobs, scheduled tasks). */
    @SerializedName("system") SYSTEM;

    /**
     * Wire-format string for a query parameter
     * (e.g. {@code "apiKey"} for {@link #API_KEY}).
     *
     * @return the camelCase wire value
     */
    public String wireValue() {
        switch (this) {
            case USER: return "user";
            case API_KEY: return "apiKey";
            case INTEGRATOR_KEY: return "integratorKey";
            case SYSTEM: return "system";
            default: throw new AssertionError(this);
        }
    }
}
