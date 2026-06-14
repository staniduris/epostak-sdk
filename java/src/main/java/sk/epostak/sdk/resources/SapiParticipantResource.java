package sk.epostak.sdk.resources;

public final class SapiParticipantResource {
    private final SapiParticipantDocumentsResource documents;

    SapiParticipantResource(SapiResource sapi, String participantId) {
        this.documents = new SapiParticipantDocumentsResource(sapi, participantId);
    }

    public SapiParticipantDocumentsResource documents() {
        return documents;
    }
}
