package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * Response from {@code POST /document/receive-callback}. Contains the
 * created webhook subscription including the one-time HMAC-SHA256 signing
 * secret that should be stored for verifying incoming payloads.
 *
 * @param id        the webhook subscription UUID
 * @param url       the registered endpoint URL
 * @param events    subscribed event types
 * @param secret    HMAC-SHA256 signing secret — returned ONCE at creation time
 * @param isActive  {@code true} when the subscription is active
 * @param createdAt ISO 8601 timestamp of creation
 */
public record ReceiveCallbackResponse(
        String id,
        String url,
        List<String> events,
        String secret,
        @SerializedName("is_active") boolean isActive,
        @SerializedName("created_at") String createdAt
) {}
