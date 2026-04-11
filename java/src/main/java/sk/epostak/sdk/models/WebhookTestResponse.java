package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

/**
 * Response from sending a test event to a webhook endpoint.
 *
 * @param success      whether the test delivery was successful
 * @param statusCode   HTTP status code returned by the webhook URL, or {@code null} if the request failed
 * @param responseTime round-trip response time in milliseconds
 * @param webhookId    the webhook UUID that was tested
 * @param event        the event type used for the test
 * @param error        error message if the test delivery failed, or {@code null}
 */
public record WebhookTestResponse(
        boolean success,
        @SerializedName("statusCode") Integer statusCode,
        @SerializedName("responseTime") double responseTime,
        @SerializedName("webhookId") String webhookId,
        String event,
        String error
) {}
