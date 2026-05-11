package sk.epostak.sdk.models;

import java.util.List;

/**
 * A webhook subscription for push-based event delivery.
 *
 * @param id             the webhook UUID
 * @param url            the HTTPS endpoint URL receiving webhook payloads,
 *                       or {@code null} for a pull-only subscription
 * @param events         list of subscribed event types, e.g. {@code ["document.received", "document.delivered"]}
 * @param isActive       {@code true} if the webhook is actively delivering events
 * @param failedAttempts count of consecutive failed delivery attempts (resets on success)
 * @param createdAt      ISO 8601 timestamp of webhook creation
 */
public record Webhook(
        String id,
        String url,    // nullable — null for pull-only subscriptions
        List<String> events,
        boolean isActive,
        Integer failedAttempts,
        String createdAt
) {}
