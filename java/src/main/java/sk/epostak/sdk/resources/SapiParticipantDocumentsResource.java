package sk.epostak.sdk.resources;

import java.util.Map;

public final class SapiParticipantDocumentsResource {
    private final SapiResource sapi;
    private final String participantId;

    SapiParticipantDocumentsResource(SapiResource sapi, String participantId) {
        this.sapi = sapi;
        this.participantId = participantId;
    }

    public Map<String, Object> send(Map<String, Object> body, String idempotencyKey) {
        return sapi.send(body, participantId, idempotencyKey);
    }

    public Map<String, Object> receive(Integer limit, String status, String pageToken) {
        return sapi.receive(participantId, limit, status, pageToken);
    }

    public Map<String, Object> get(String documentId) {
        return sapi.get(documentId, participantId);
    }

    public Map<String, Object> acknowledge(String documentId) {
        return sapi.acknowledge(documentId, participantId);
    }
}
