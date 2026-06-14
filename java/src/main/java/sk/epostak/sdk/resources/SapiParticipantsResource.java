package sk.epostak.sdk.resources;

public final class SapiParticipantsResource {
    private final SapiResource sapi;

    SapiParticipantsResource(SapiResource sapi) {
        this.sapi = sapi;
    }

    public SapiParticipantResource forParticipant(String participantId) {
        return new SapiParticipantResource(sapi, participantId);
    }
}
