"""Audit resource -- per-firm security/auth audit feed (Wave 3.4).

Tenant-isolated: every row is filtered by the firm the calling key is
bound to. Integrators with multiple managed firms see only the firm
specified by ``X-Firm-Id`` (set automatically when you pass ``firm_id``
to :class:`~epostak.client.EPostak` or use
:meth:`~epostak.client.EPostak.with_firm`).

Cursor pagination over ``(occurred_at DESC, id DESC)`` — pass the
``next_cursor`` from one page back into the next call to walk the feed
deterministically, even across rows with identical timestamps.
"""

from __future__ import annotations

from typing import Optional, TYPE_CHECKING

if TYPE_CHECKING:
    from epostak.types import AuditEvent, CursorPage

from epostak.resources.documents import _BaseResource, _build_query


class AuditResource(_BaseResource):
    """Per-firm audit feed (cursor-paginated)."""

    def list(
        self,
        event: Optional[str] = None,
        actor_type: Optional[str] = None,
        since: Optional[str] = None,
        until: Optional[str] = None,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> "CursorPage[AuditEvent]":
        """List audit events for the current firm. Cursor-paginated.

        Args:
            event: Exact-match filter (e.g. ``"jwt.issued"``,
                ``"key.rotated"``).
            actor_type: One of ``"user"``, ``"apiKey"``,
                ``"integratorKey"``, ``"system"``.
            since: ISO-8601 timestamp lower bound (inclusive).
            until: ISO-8601 timestamp upper bound (exclusive).
            cursor: Opaque cursor returned by a previous page's
                ``next_cursor``.
            limit: 1–100, default 20.

        Returns:
            Dict with ``items`` (list of ``AuditEvent``) and
            ``next_cursor`` (str or None when the feed is exhausted).

        Example::

            cursor = None
            while True:
                page = client.audit.list(
                    event="jwt.issued",
                    since="2026-04-01T00:00:00Z",
                    cursor=cursor,
                    limit=50,
                )
                for ev in page["items"]:
                    print(ev["occurred_at"], ev["event"], ev["actor_id"])
                cursor = page.get("next_cursor")
                if not cursor:
                    break
        """
        params = _build_query(
            {
                "event": event,
                "actor_type": actor_type,
                "since": since,
                "until": until,
                "cursor": cursor,
                "limit": limit,
            }
        )
        return self._request("GET", "/audit", params=params)
