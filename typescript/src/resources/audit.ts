import { BaseResource, buildQuery } from "../utils/request.js";
import type { AuditEvent, AuditListParams, CursorPage } from "../types.js";

/**
 * Resource for the per-firm security/auth audit feed (Wave 3.4).
 *
 * Tenant-isolated: every row is filtered by the firm the calling key is
 * bound to. Integrators with multiple managed firms see only the firm
 * specified by `X-Firm-Id` (set automatically on the client when you pass
 * `firmId` to `new EPostak({...})` or use `client.withFirm(...)`).
 *
 * Cursor pagination over `(occurred_at DESC, id DESC)` — pass the
 * `next_cursor` from one page back into the next call to walk the feed
 * deterministically, even across rows with identical timestamps.
 *
 * @example
 * ```typescript
 * let cursor: string | null = null;
 * do {
 *   const page = await client.audit.list({
 *     event: "jwt.issued",
 *     since: "2026-04-01T00:00:00Z",
 *     cursor,
 *     limit: 50,
 *   });
 *   for (const ev of page.items) {
 *     console.log(ev.occurred_at, ev.event, ev.actor_id);
 *   }
 *   cursor = page.next_cursor;
 * } while (cursor);
 * ```
 */
export class AuditResource extends BaseResource {
  /**
   * List audit events for the current firm. Cursor-paginated.
   *
   * @param params - Optional filters: `event` (exact match),
   *   `actorType` (one of `user | apiKey | integratorKey | system`),
   *   ISO-timestamp `since`/`until`, `cursor` (opaque, from a previous page),
   *   and `limit` (1–100, default 20).
   */
  list(params?: AuditListParams): Promise<CursorPage<AuditEvent>> {
    return this.request(
      "GET",
      `/audit${buildQuery({
        event: params?.event,
        actor_type: params?.actorType,
        since: params?.since,
        until: params?.until,
        cursor: params?.cursor,
        limit: params?.limit,
      })}`,
    );
  }
}
