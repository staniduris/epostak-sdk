package sk.epostak.sdk.models;

import java.util.List;

/** Cursor-paginated White Label participant list. */
public record WhiteLabelParticipantList(
        List<WhiteLabelParticipant> participants,
        String nextCursor
) {}
