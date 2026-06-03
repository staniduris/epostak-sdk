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
        ConnectorOutboxBatchSendResponse,
        ConnectorOutboxItem,
        ConnectorOutboxListResponse,
        ConnectorOutboxStageRequest,
        ConnectorOutboxStageResponse,
        ConnectorOutboxStatus,
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

    def stage_outbox(self, body: ConnectorOutboxStageRequest) -> ConnectorOutboxStageResponse:
        """Stage one or more ERP invoices without immediate Peppol delivery."""
        return self._request("POST", "/connector/outbox", json=body)

    def list_outbox(
        self,
        *,
        status: Optional[ConnectorOutboxStatus] = None,
        limit: Optional[int] = None,
        offset: Optional[int] = None,
    ) -> ConnectorOutboxListResponse:
        """List staged Connector outbox items."""
        params = _build_query({"status": status, "limit": limit, "offset": offset})
        return self._request("GET", "/connector/outbox", params=params)

    def get_outbox_item(self, outbox_id: str) -> ConnectorOutboxItem:
        """Retrieve a single Connector outbox item."""
        return self._request("GET", f"/connector/outbox/{quote(outbox_id, safe='')}")

    def send_outbox_item(
        self,
        outbox_id: str,
        *,
        force: Optional[bool] = None,
    ) -> ConnectorOutboxItem:
        """Send one staged outbox item through the Connector workflow."""
        body: Dict[str, Any] = {}
        if force is not None:
            body["force"] = force
        return self._request("POST", f"/connector/outbox/{quote(outbox_id, safe='')}/send", json=body)

    def send_outbox_batch(
        self,
        *,
        ids: Optional[list[str]] = None,
        limit: Optional[int] = None,
        force: Optional[bool] = None,
    ) -> ConnectorOutboxBatchSendResponse:
        """Send ready, failed, or due scheduled outbox items in a batch."""
        body: Dict[str, Any] = {}
        if ids is not None:
            body["ids"] = ids
        if limit is not None:
            body["limit"] = limit
        if force is not None:
            body["force"] = force
        return self._request("POST", "/connector/outbox/send", json=body)

    def cancel_outbox_item(self, outbox_id: str) -> ConnectorOutboxItem:
        """Cancel a staged outbox item before it is sent."""
        return self._request("DELETE", f"/connector/outbox/{quote(outbox_id, safe='')}")
