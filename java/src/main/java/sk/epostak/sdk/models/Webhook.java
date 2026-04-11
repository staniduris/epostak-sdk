package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;
import java.util.List;

/**
 * A webhook subscription for push-based event delivery.
 *
 * @param id        the webhook UUID
 * @param url       the endpoint URL receiving webhook payloads
 * @param events    list of subscribed event types, e.g. {@code ["document.received", "document.delivered"]}
 * @param isActive  {@code true} if the webhook is actively delivering events
 * @param createdAt ISO 8601 timestamp of webhook creation
 */
public record Webhook(
        String id,
        String url,
        List<String> events,
        @SerializedName("is_active") boolean isActive,
        @SerializedName("created_at") String createdAt
) {}
