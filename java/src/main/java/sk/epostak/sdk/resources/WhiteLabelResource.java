package sk.epostak.sdk.resources;

import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.*;

import java.util.LinkedHashMap;
import java.util.Map;
import java.nio.charset.StandardCharsets;

/** Integrator-scoped White Label participant registration and migration. */
public final class WhiteLabelResource {
    private final HttpClient http;

    public WhiteLabelResource(HttpClient http) {
        this.http = http;
    }

    public WhiteLabelParticipantList listParticipants(Integer limit, String cursor) {
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("limit", limit);
        params.put("cursor", cursor);
        return http.getNoFirm(
                "/white-label/participants" + HttpClient.buildQuery(params),
                WhiteLabelParticipantList.class
        );
    }

    public WhiteLabelParticipantList listParticipants() {
        return listParticipants(null, null);
    }

    public WhiteLabelParticipantOperation registerParticipant(
            WhiteLabelParticipantRegistrationRequest request,
            String idempotencyKey
    ) {
        String key = idempotencyKey(idempotencyKey);
        return http.postIdempotentNoFirm(
                "/white-label/participants/registrations",
                request,
                WhiteLabelParticipantOperation.class,
                key
        );
    }

    public WhiteLabelParticipantOperation migrateParticipant(
            WhiteLabelParticipantMigrationRequest request,
            String idempotencyKey
    ) {
        String key = idempotencyKey(idempotencyKey);
        return http.postIdempotentNoFirm(
                "/white-label/participants/migrations",
                request,
                WhiteLabelParticipantOperation.class,
                key
        );
    }

    public WhiteLabelParticipant getParticipant(String participantId) {
        return http.getNoFirm(
                "/white-label/participants/" + HttpClient.encode(participantId),
                WhiteLabelParticipant.class
        );
    }

    public WhiteLabelMigrationCodeResponse requestMigrationCode(
            String participantId,
            String idempotencyKey
    ) {
        String key = idempotencyKey(idempotencyKey);
        return http.postIdempotentNoFirm(
                "/white-label/participants/" + HttpClient.encode(participantId) + "/migration-code",
                null,
                WhiteLabelMigrationCodeResponse.class,
                key
        );
    }

    public WhiteLabelParticipantOperation getOperation(String operationId) {
        return http.getNoFirm(
                "/white-label/operations/" + HttpClient.encode(operationId),
                WhiteLabelParticipantOperation.class
        );
    }

    private static String idempotencyKey(String value) {
        if (value == null || value.isBlank() || value.getBytes(StandardCharsets.UTF_8).length > 255) {
            throw new IllegalArgumentException("White Label idempotency key must be 1-255 UTF-8 bytes");
        }
        return value;
    }
}
