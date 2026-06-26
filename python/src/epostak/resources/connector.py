"""Connector resource -- polling-first ERP workflow over Enterprise API."""

from __future__ import annotations

from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import quote

from epostak.resources.documents import _BaseResource, _build_query, _idempotency_headers

if TYPE_CHECKING:
    from epostak.types import (
        ConnectorAckResponse,
        ConnectorActionRequest,
        ConnectorActionResponse,
        ConnectorAutopilotRequest,
        ConnectorAutopilotRunResponse,
        ConnectorEventsResponse,
        ConnectorInboxDocument,
        ConnectorInboxListResponse,
        ConnectorMailboxListResponse,
        ConnectorMailboxRepairRequest,
        ConnectorMailboxUpdateResponse,
        ConnectorMapperRequest,
        ConnectorOutboxBatchSendResponse,
        ConnectorOutboxItem,
        ConnectorOutboxListResponse,
        ConnectorOutboxStageRequest,
        ConnectorOutboxStageResponse,
        ConnectorOutboxStatus,
        ConnectorPreflightRequest,
        ConnectorPreflightResponse,
        ConnectorReconcileResponse,
        ConnectorReconcileStatus,
        ConnectorSendRequest,
        ConnectorSendResponse,
        ConnectorSendPolicyOptions,
        ConnectorStatusResponse,
        ConnectorSyncResponse,
        ConnectorZenInputRequest,
    )


class ConnectorResource(_BaseResource):
    """Connector workflow endpoints for ERP teams.

    Legacy Connector calls use firm scoping. Connector V2 calls resolve the
    managed firm from ``customerRef`` and omit ``X-Firm-Id``.
    """

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        super().__init__(*args, **kwargs)
        self.customers = ConnectorCustomersResource(self)

    def submit_document(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        """Submit an ERP document through the durable Connector lifecycle."""
        payload = dict(body)
        payload.setdefault("mode", "stage")
        return self.autopilot(payload)  # type: ignore[arg-type]

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

    def autopilot(self, body: ConnectorAutopilotRequest) -> ConnectorAutopilotRunResponse:
        """Start a managed Connector Autopilot lifecycle run."""
        return self._request("POST", "/connector/autopilot", json=body, omit_firm_id=True)

    def mapper(self, body: ConnectorMapperRequest) -> Dict[str, Any]:
        """Map a saved Connector Mapper template input into preview, stage, or send."""
        return self._request("POST", "/connector/mapper", json=body, omit_firm_id=True)

    def zen_input(self, body: ConnectorZenInputRequest) -> ConnectorAutopilotRunResponse:
        """Normalize a loose ERP/customer payload into a Connector lifecycle run."""
        return self._request("POST", "/connector/zen-input", json=body, omit_firm_id=True)

    def get_autopilot_run(self, autopilot_id: str) -> ConnectorAutopilotRunResponse:
        """Retrieve an Autopilot run by ID."""
        return self._request("GET", f"/connector/autopilot/{quote(autopilot_id, safe='')}", omit_firm_id=True)

    def send_autopilot_run(self, autopilot_id: str) -> ConnectorAutopilotRunResponse:
        """Send a shadow-validated or staged Autopilot run."""
        return self._request(
            "POST",
            f"/connector/autopilot/{quote(autopilot_id, safe='')}/send",
            json={},
            omit_firm_id=True,
        )

    def reconcile(
        self,
        *,
        status: Optional[ConnectorReconcileStatus] = None,
        since: Optional[str] = None,
    ) -> ConnectorReconcileResponse:
        """List Connector reconciliation items for ERP state sync."""
        params = _build_query({"status": status, "since": since})
        return self._request("GET", "/connector/reconcile", params=params, omit_firm_id=True)

    def mailboxes(self) -> ConnectorMailboxListResponse:
        """List Connector-managed customer mailboxes."""
        return self._request("GET", "/connector/mailbox", omit_firm_id=True)

    def repair_mailbox(
        self,
        body: Optional[ConnectorMailboxRepairRequest] = None,
    ) -> Dict[str, Any]:
        """Repair Connector mailbox state for one customer or all customers."""
        return self._request("POST", "/connector/mailbox/repair", json=body or {}, omit_firm_id=True)

    def update_mailbox_send_policy(
        self,
        customer_ref: str,
        body: ConnectorSendPolicyOptions,
    ) -> ConnectorMailboxUpdateResponse:
        """Update the managed send policy for a Connector mailbox."""
        return self._request(
            "PATCH",
            f"/connector/mailbox/{quote(customer_ref, safe='')}/send-policy",
            json=body,
            omit_firm_id=True,
        )

    def sync(
        self,
        *,
        customer_ref: Optional[str] = None,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorSyncResponse:
        """List Connector sync items for ERP reconciliation cursors."""
        params = _build_query({"customerRef": customer_ref, "cursor": cursor, "limit": limit})
        return self._request("GET", "/connector/sync", params=params, omit_firm_id=True)

    def get_document(self, document_id: str) -> Dict[str, Any]:
        """Retrieve a Connector document lifecycle snapshot."""
        return self._request("GET", f"/connector/documents/{quote(document_id, safe='')}", omit_firm_id=True)

    def get_document_ubl(self, document_id: str) -> str:
        """Download a Connector document UBL XML body."""
        response = self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/ubl",
            raw=True,
            omit_firm_id=True,
        )
        return response.text

    def get_document_evidence(self, document_id: str) -> Dict[str, Any]:
        """Retrieve Connector document delivery evidence."""
        return self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/evidence",
            omit_firm_id=True,
        )

    def get_document_evidence_bundle(self, document_id: str) -> Dict[str, Any]:
        """Retrieve the Connector evidence bundle manifest."""
        return self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/evidence-bundle",
            omit_firm_id=True,
        )

    def run_action(
        self,
        action_id: str,
        body: Optional[ConnectorActionRequest] = None,
    ) -> ConnectorActionResponse:
        """Execute a pending Connector action."""
        return self._request(
            "POST",
            f"/connector/actions/{quote(action_id, safe='')}",
            json=body or {},
            omit_firm_id=True,
        )


def _with_customer_ref(customer_ref: str, body: Dict[str, Any]) -> Dict[str, Any]:
    if body.get("customerRef") not in (None, customer_ref):
        raise ValueError("Connector customerRef conflicts with scoped customer")
    scoped = dict(body)
    scoped["customerRef"] = customer_ref
    return scoped


class ConnectorCustomerDocumentsResource:
    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent

    def get(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document(document_id)

    def ubl(self, document_id: str) -> str:
        return self._parent.get_document_ubl(document_id)

    def evidence(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document_evidence(document_id)

    def evidence_bundle(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document_evidence_bundle(document_id)


class ConnectorCustomerMailboxResource:
    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        self._parent = parent
        self._customer_ref = customer_ref

    def repair(self) -> Dict[str, Any]:
        return self._parent.repair_mailbox({"customerRef": self._customer_ref})

    def update_send_policy(self, body: ConnectorSendPolicyOptions) -> ConnectorMailboxUpdateResponse:
        return self._parent.update_mailbox_send_policy(self._customer_ref, body)


class ConnectorCustomerResource:
    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        self._parent = parent
        self._customer_ref = customer_ref
        self.documents = ConnectorCustomerDocumentsResource(parent)
        self.mailbox = ConnectorCustomerMailboxResource(parent, customer_ref)

    def submit_document(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        payload = _with_customer_ref(self._customer_ref, body)
        payload.setdefault("mode", "stage")
        return self._parent.autopilot(payload)  # type: ignore[arg-type]

    def autopilot(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        return self._parent.autopilot(_with_customer_ref(self._customer_ref, body))  # type: ignore[arg-type]

    def mapper(self, body: Dict[str, Any]) -> Dict[str, Any]:
        return self._parent.mapper(_with_customer_ref(self._customer_ref, body))  # type: ignore[arg-type]

    def zen_input(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        return self._parent.zen_input(_with_customer_ref(self._customer_ref, body))  # type: ignore[arg-type]

    def sync(self, **params: Any) -> ConnectorSyncResponse:
        return self._parent.sync(customer_ref=self._customer_ref, **params)


class ConnectorCustomersResource:
    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent

    def for_customer(self, customer_ref: str) -> ConnectorCustomerResource:
        return ConnectorCustomerResource(self._parent, customer_ref)
