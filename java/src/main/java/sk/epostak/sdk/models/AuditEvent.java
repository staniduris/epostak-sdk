package sk.epostak.sdk.models;

import com.google.gson.annotations.SerializedName;

import java.util.Map;

/**
 * A single row in the per-firm audit feed. Field naming on the wire is
 * snake_case to match the SIEM-forwardable JSON shape; the Java accessors are
 * camelCase as usual.
 *
 * @param id          audit row UUID
 * @param occurredAt  ISO 8601 timestamp when the event occurred
 * @param actorType   type of actor that triggered the event
 * @param actorId     UUID of the actor (key id, user id, or {@code null} for {@code system})
 * @param event       event name, e.g. {@code "jwt.issued"}, {@code "api_key.rotated"}
 * @param targetType  type of the target object, or {@code null}
 * @param targetId    UUID of the target object, or {@code null}
 * @param ip          source IP, or {@code null} when not captured
 * @param userAgent   caller User-Agent, or {@code null}
 * @param metadata    free-form metadata map, or {@code null}
 */
public record AuditEvent(
        String id,
        @SerializedName("occurred_at") String occurredAt,
        @SerializedName("actor_type") AuditActorType actorType,
        @SerializedName("actor_id") String actorId,
        String event,
        @SerializedName("target_type") String targetType,
        @SerializedName("target_id") String targetId,
        String ip,
        @SerializedName("user_agent") String userAgent,
        Map<String, Object> metadata
) {}
