package sk.epostak.sdk.models;

/**
 * Response from sending a test event to a webhook endpoint via
 * {@code POST /webhooks/{id}/test}.
 *
 * @param success      whether the test delivery was successful (HTTP 2xx from the endpoint)
 * @param statusCode   HTTP status code returned by the webhook URL, or {@code null} if the request failed
 * @param responseTime round-trip response time in milliseconds (server-observed)
 * @param webhookId    the webhook UUID that was tested
 * @param event        the event type used for the test
 * @param error        error message if the test delivery failed, or {@code null} on success
 */
public record WebhookTestResponse(
        boolean success,
        Integer statusCode,
        long responseTime,
        String webhookId,
        String event,
        String error
) {}
