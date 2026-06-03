"""Connector resource -- polling-first ERP workflow over Enterprise API."""

from __future__ import annotations

from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import quote

from epostak.resources.documents import _BaseResource, _build_query, _idempotency_headers

if TYPE_CHECKING:
    from epostak.types import (
        ConnectorAckResponse,
        ConnectorEventsResponse,
        ConnectorInboxDocument,
        ConnectorInboxListResponse,
        ConnectorPreflightRequest,
        ConnectorPreflightResponse,
        ConnectorSendRequest,
        ConnectorSendResponse,
        ConnectorStatusResponse,
    )


class ConnectorResource(_BaseResource):
    """Connector workflow endpoints for ERP teams.

    Connector uses the same Enterprise API credentials, firm scoping, and
    ``documentId`` as the full Enterprise API. For integrator keys, scope the
    client with ``client.with_firm(firm_id)`` so requests include
    ``X-Firm-Id``.
    """

    def preflight(self, body: ConnectorPreflightRequest) -> ConnectorPreflightResponse:
        """Validate receiver reachability and payload readiness before send."""
        return self._request("POST", "/connector/preflight", json=body)

    def send(
        self,
        body: ConnectorSendRequest,
        *,
        idempotency_key: Optional[str] = None,
    ) -> ConnectorSendResponse:
        """Send an ERP document payload through Connector."""
        return self._request(
            "POST",
            "/connector/send",
            json=body,
            extra_headers=_idempotency_headers(idempotency_key),
        )

    def status(self, document_id: str) -> ConnectorStatusResponse:
        """Get Connector status for a document ID."""
        return self._request("GET", f"/connector/status/{quote(document_id, safe='')}")

    def inbox(
        self,
        *,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorInboxListResponse:
        """List Connector inbox documents with cursor pagination."""
        params = _build_query({"cursor": cursor, "limit": limit})
        return self._request("GET", "/connector/inbox", params=params)

    def get_inbox_document(self, document_id: str) -> ConnectorInboxDocument:
        """Retrieve a single Connector inbox document."""
        return self._request("GET", f"/connector/inbox/{quote(document_id, safe='')}")

    def ack(self, document_id: str) -> ConnectorAckResponse:
        """Acknowledge a Connector inbox document as processed."""
        return self._request(
            "POST",
            f"/connector/inbox/{quote(document_id, safe='')}/ack",
            json={},
        )

    def events(
        self,
        *,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorEventsResponse:
        """List Connector polling events with cursor pagination."""
        params = _build_query({"cursor": cursor, "limit": limit})
        return self._request("GET", "/connector/events", params=params)
