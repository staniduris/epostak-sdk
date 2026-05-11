"""Outbound resource -- Pull API for sent documents.

Provides :class:`OutboundResource` accessible via ``client.outbound``.
Requires ``api-eligible`` plan, scope ``documents:read``.
"""

from __future__ import annotations

from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    from epostak.types import (
        PullOutboundDocument,
        PullOutboundListResponse,
        PullOutboundEventsResponse,
    )

from epostak.resources.documents import _BaseResource, _build_query


class OutboundResource(_BaseResource):
    """Pull API: sent (outbound) documents and their delivery events."""

    def list(
        self,
        *,
        since: Optional[str] = None,
        limit: Optional[int] = None,
        kind: Optional[str] = None,
        status: Optional[str] = None,
        business_status: Optional[str] = None,
        recipient: Optional[str] = None,
    ) -> PullOutboundListResponse:
        """Fetch sent documents (cursor-paginated).

        The result is a union of ``Invoice`` (billing) and ``PeppolDocument``
        (generic) tables, merged in-app and sorted by creation date.

        Args:
            since: ISO 8601 cursor — only return documents created after this.
            limit: Max documents to return, 1–500 (default 100).
            kind: Filter by document kind, e.g. ``"invoice"``, ``"credit_note"``.
            status: Filter by transport status, e.g. ``"SENT"``, ``"DELIVERED"``.
            business_status: Filter by business status.
            recipient: Filter by recipient Peppol ID, e.g. ``"0245:1234567890"``.

        Returns:
            Dict with ``documents``, ``next_cursor`` (str or None), and
            ``has_more`` (bool).

        Example::

            page = client.outbound.list(status="DELIVERED", limit=50)
            for doc in page["documents"]:
                print(doc["id"], doc["status"])
        """
        params = _build_query(
            {
                "since": since,
                "limit": limit,
                "kind": kind,
                "status": status,
                "business_status": business_status,
                "recipient": recipient,
            }
        )
        return self._request("GET", "/outbound/documents", params=params)

    def get(self, id: str) -> PullOutboundDocument:
        """Get a single sent document by ID.

        The detail view includes ``attempt_history`` (delivery attempts), which
        is absent in the list view.

        Args:
            id: Document UUID.

        Returns:
            Full outbound document object.

        Example::

            doc = client.outbound.get("doc-uuid")
            print(doc["status"], doc.get("attempt_history"))
        """
        return self._request("GET", f"/outbound/documents/{quote(id, safe='')}")

    def get_ubl(self, id: str) -> str:
        """Get the raw UBL XML for a sent document.

        Probes ``Invoice.ublXmlPath`` and ``PeppolDocument.rawXmlPath``,
        returning the first match. Raises ``EPostakError`` with status 404
        when neither is available.

        Args:
            id: Document UUID.

        Returns:
            UBL 2.1 XML string.

        Example::

            xml = client.outbound.get_ubl("doc-uuid")
        """
        resp = self._request("GET", f"/outbound/documents/{quote(id, safe='')}/ubl", raw=True)
        if isinstance(resp, str):
            return resp
        try:
            return resp.text
        except AttributeError:
            return str(resp)

    def events(
        self,
        *,
        since: Optional[str] = None,
        limit: Optional[int] = None,
        document_id: Optional[str] = None,
    ) -> PullOutboundEventsResponse:
        """Stream document delivery events (cursor-paginated).

        Currently covers invoice-backed documents only (noted in route code).

        Args:
            since: ISO 8601 cursor — only return events after this timestamp.
            limit: Max events to return, 1–500 (default 100).
            document_id: Filter to a specific document UUID.

        Returns:
            Dict with ``events``, ``next_cursor`` (str or None), and
            ``has_more`` (bool).

        Example::

            stream = client.outbound.events(document_id="doc-uuid")
            for ev in stream["events"]:
                print(ev["type"], ev["occurred_at"])
        """
        params = _build_query({"since": since, "limit": limit, "document_id": document_id})
        return self._request("GET", "/outbound/events", params=params)
