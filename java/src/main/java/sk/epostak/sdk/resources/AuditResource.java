package sk.epostak.sdk.resources;

import com.google.gson.reflect.TypeToken;
import sk.epostak.sdk.HttpClient;
import sk.epostak.sdk.models.AuditEvent;
import sk.epostak.sdk.models.AuditListParams;
import sk.epostak.sdk.models.CursorPage;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Per-firm security/auth audit feed (Wave 3.4).
 * <p>
 * Tenant-isolated: every row is filtered by the firm the calling key is
 * bound to. Integrators with multiple managed firms see only the firm
 * specified by {@code X-Firm-Id} (set automatically on the client when you
 * pass {@code firmId} to the builder or use {@code client.withFirm(...)}).
 * <p>
 * Cursor pagination over {@code (occurred_at DESC, id DESC)} — pass the
 * {@link CursorPage#nextCursor()} from one page back into the next call to
 * walk the feed deterministically, even across rows with identical
 * timestamps.
 *
 * <pre>{@code
 * String cursor = null;
 * do {
 *     CursorPage<AuditEvent> page = client.audit().list(
 *         AuditListParams.builder()
 *             .event("jwt.issued")
 *             .since("2026-04-01T00:00:00Z")
 *             .cursor(cursor)
 *             .limit(50)
 *             .build());
 *     for (AuditEvent ev : page.items()) {
 *         System.out.println(ev.occurredAt() + " " + ev.event() + " " + ev.actorId());
 *     }
 *     cursor = page.nextCursor();
 * } while (cursor != null);
 * }</pre>
 */
public final class AuditResource {

    private static final TypeToken<CursorPage<AuditEvent>> PAGE_TYPE =
            new TypeToken<CursorPage<AuditEvent>>() {};

    private final HttpClient http;

    /**
     * Creates a new audit resource.
     *
     * @param http the HTTP client used for API communication
     */
    public AuditResource(HttpClient http) {
        this.http = http;
    }

    /**
     * List audit events for the current firm. Cursor-paginated.
     *
     * @param params optional filters and pagination; pass {@link AuditListParams#empty()} to read the latest page
     * @return one page of audit rows plus the cursor for the next page (if any)
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<AuditEvent> list(AuditListParams params) {
        Map<String, Object> qp = new LinkedHashMap<>();
        if (params != null) {
            qp.put("event", params.event());
            qp.put("actor_type", params.actorType() != null ? params.actorType().wireValue() : null);
            qp.put("since", params.since());
            qp.put("until", params.until());
            qp.put("cursor", params.cursor());
            qp.put("limit", params.limit());
        }
        String path = "/audit" + HttpClient.buildQuery(qp);
        return http.getTyped(path, PAGE_TYPE);
    }

    /**
     * List audit events with no filters and the server's default page size.
     *
     * @return one page of audit rows newest-first
     * @throws sk.epostak.sdk.EPostakException if the request fails
     */
    public CursorPage<AuditEvent> list() {
        return list(AuditListParams.empty());
    }
}
