package sk.epostak.sdk.models;

/**
 * Response from sending a test event to a webhook endpoint via
 * {@code POST /webhooks/{id}/test}.
 *
 * @param success      whether the test delivery was successful (HTTP 2xx from the endpoint)
 * @param mode                 {@code direct} for immediate tests, {@code queued} for worker-backed tests
 * @param statusCode           HTTP status code returned by the webhook URL, or {@code null} if the request failed
 * @param responseTime         round-trip response time in milliseconds (server-observed)
 * @param webhookId            the webhook UUID that was tested
 * @param event                the event type used for the test
 * @param requested            number of requested test deliveries
 * @param sent                 number of immediate deliveries sent
 * @param succeeded            number of immediate deliveries that returned 2xx
 * @param failed               number of immediate deliveries that failed
 * @param queued               number of delivery rows queued for worker processing
 * @param testRunId            correlation id for queued tests
 * @param deliveryIdsTruncated true when only a subset of delivery IDs was returned
 * @param deliveriesUrl        convenience URL for polling queued test deliveries
 * @param error                error message if the test delivery failed, or {@code null} on success
 */
public record WebhookTestResponse(
        boolean success,
        String mode,
        Integer statusCode,
        long responseTime,
        String webhookId,
        String event,
        Integer requested,
        Integer sent,
        Integer succeeded,
        Integer failed,
        Integer queued,
        String testRunId,
        Boolean deliveryIdsTruncated,
        String deliveriesUrl,
        String error
) {}
