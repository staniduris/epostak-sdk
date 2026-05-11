"""Inbound resource -- Pull API for received documents.

Provides :class:`InboundResource` accessible via ``client.inbound``.
Requires ``api-eligible`` plan (``requireApiEligiblePlan``), scope ``documents:read``
for list/get/get_ubl and ``documents:write`` for ack.
"""

from __future__ import annotations

from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    from epostak.types import (
        PullInboundDocument,
        PullInboundListResponse,
        PullInboundAckResponse,
    )

from epostak.resources.documents import _BaseResource, _build_query, _idempotency_headers


class InboundResource(_BaseResource):
    """Pull API: received (inbound) documents."""

    def list(
        self,
        *,
        since: Optional[str] = None,
        limit: Optional[int] = None,
        kind: Optional[str] = None,
        sender: Optional[str] = None,
    ) -> PullInboundListResponse:
        """Fetch received documents (cursor-paginated).

        Args:
            since: ISO 8601 cursor — only return documents received after this timestamp.
            limit: Max documents to return, 1–500 (default 100).
            kind: Filter by document kind, e.g. ``"invoice"``, ``"credit_note"``,
                ``"self_billing"``.
            sender: Filter by sender Peppol ID, e.g. ``"0245:1234567890"``.

        Returns:
            Dict with ``documents``, ``next_cursor`` (str or None), and
            ``has_more`` (bool).

        Example::

            page = client.inbound.list(limit=50)
            for doc in page["documents"]:
                print(doc["id"], doc["sender_peppol_id"])
            if page["has_more"]:
                next_page = client.inbound.list(since=page["next_cursor"])
        """
        params = _build_query({"since": since, "limit": limit, "kind": kind, "sender": sender})
        return self._request("GET", "/inbound/documents", params=params)

    def get(self, id: str) -> PullInboundDocument:
        """Get a single received document by ID.

        Args:
            id: Document UUID.

        Returns:
            Full inbound document object.

        Example::

            doc = client.inbound.get("doc-uuid")
            print(doc["status"], doc["sender_peppol_id"])
        """
        return self._request("GET", f"/inbound/documents/{quote(id, safe='')}")

    def get_ubl(self, id: str) -> str:
        """Get the raw UBL XML for a received document.

        Returns ``application/xml`` from the server. Raises ``EPostakError``
        with status 404 when the document has no stored raw XML (legacy rows
        created before UBL storage was enabled).

        Args:
            id: Document UUID.

        Returns:
            UBL 2.1 XML string.

        Example::

            xml = client.inbound.get_ubl("doc-uuid")
            with open("invoice.xml", "w", encoding="utf-8") as f:
                f.write(xml)
        """
        resp = self._request("GET", f"/inbound/documents/{quote(id, safe='')}/ubl", raw=True)
        if isinstance(resp, str):
            return resp
        # httpx Response object from raw=True
        try:
            return resp.text
        except AttributeError:
            return str(resp)

    def ack(
        self,
        id: str,
        *,
        client_reference: Optional[str] = None,
        idempotency_key: Optional[str] = None,
    ) -> PullInboundAckResponse:
        """Acknowledge a received document (idempotent, latest-ack-wins).

        Marks the document as client-acknowledged. Subsequent acks overwrite
        ``clientAckedAt`` (latest-ack-wins). Scope required: ``documents:write``.

        Args:
            id: Document UUID to acknowledge.
            client_reference: Optional client-side reference string (max 256 chars).
                Stored on the document and returned in the response.
            idempotency_key: Optional idempotency key to prevent duplicate acks.

        Returns:
            Full document object post-ack.

        Example::

            doc = client.inbound.ack("doc-uuid", client_reference="PO-2026-001")
            print(doc["client_acked_at"])
        """
        body: Dict[str, Any] = {}
        if client_reference is not None:
            body["client_reference"] = client_reference
        return self._request(
            "POST",
            f"/inbound/documents/{quote(id, safe='')}/ack",
            json=body,
            extra_headers=_idempotency_headers(idempotency_key),
        )
