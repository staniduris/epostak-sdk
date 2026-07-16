"""Managed, customer-scoped Connector ERP workflow."""

from __future__ import annotations

import hashlib
from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import quote

from epostak.resources.documents import _BaseResource, _build_query, _idempotency_headers

_CONNECTOR_TRIM_CHARS = (
    "\u0009\u000a\u000b\u000c\u000d\u0020\u00a0\u1680"
    "\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a"
    "\u2028\u2029\u202f\u205f\u3000\ufeff"
)
_CONNECTOR_INVOICE_RESPONSE_STATUSES = {
    "received",
    "in_process",
    "under_query",
    "conditionally_accepted",
    "rejected",
    "accepted",
    "paid",
}


def _connector_trim_string(value: str) -> str:
    """Match the backend's ECMAScript TrimString code-point contract."""
    return value.strip(_CONNECTOR_TRIM_CHARS)

if TYPE_CHECKING:
    from epostak.types import (
        ConnectorAckResponse,
        ConnectorActionRequest,
        ConnectorActionResponse,
        ConnectorAutopilotRequest,
        ConnectorAutopilotRunResponse,
        ConnectorBusinessAcknowledgeResponse,
        ConnectorBusinessDocument,
        ConnectorBusinessDocumentType,
        ConnectorBusinessDocumentListResponse,
        ConnectorBusinessDocumentRequest,
        ConnectorBusinessEventsResponse,
        ConnectorBusinessState,
        ConnectorEventsResponse,
        ConnectorInboxDocument,
        ConnectorInboxListResponse,
        ConnectorInvoiceResponseRequest,
        ConnectorInvoiceResponseResult,
        ConnectorMailboxListResponse,
        ConnectorMailboxRepairRequest,
        ConnectorMailboxUpdateResponse,
        ConnectorMapperRequest,
        ConnectorMapperPreviewRequest,
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
        ConnectorSubmitDocumentRequest,
        ConnectorSyncResponse,
        ConnectorWebhookConfiguration,
        ConnectorWebhookDeliveriesResponse,
        ConnectorWebhookTestResponse,
        ConnectorWebhookDeliveryDetail,
        ConnectorWebhookReplayResult,
        ConnectorWebhookTestSuiteAccepted,
        ConnectorWebhookTestSuiteStatus,
        ConnectorZenInputRequest,
    )


def _connector_document_idempotency_key(customer_ref: str, external_id: str) -> str:
    """Hash a length-prefixed UTF-8 tuple into a stable bounded key."""
    customer = _connector_trim_string(customer_ref).encode("utf-8")
    external = _connector_trim_string(external_id).encode("utf-8")
    payload = (
        len(customer).to_bytes(4, "big")
        + customer
        + len(external).to_bytes(4, "big")
        + external
    )
    return f"connector:v1:{hashlib.sha256(payload).hexdigest()}"


def _validate_connector_idempotency_key(value: str) -> str:
    byte_length = len(value.encode("utf-8"))
    if not _connector_trim_string(value) or byte_length > 255:
        raise ValueError("Connector idempotency key must be 1-255 UTF-8 bytes")
    return value


class ConnectorResource(_BaseResource):
    """Connector workflow endpoints for ERP teams.

    The primary API is ``customers.for_customer(ref).documents`` and
    ``.events``. Direct preflight/send/outbox/inbox/Autopilot/sync methods are
    supported compatibility aliases for the same methods under ``advanced``.
    """

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        super().__init__(*args, **kwargs)
        self.documents = ConnectorDocumentsResource(self)
        self.customers = ConnectorCustomersResource(self)
        self.webhook = ConnectorWebhookResource(self)
        self.advanced = ConnectorAdvancedResource(self)

    def submit_document(
        self,
        body: ConnectorSubmitDocumentRequest,
    ) -> ConnectorAutopilotRunResponse:
        """Autopilot submit compatibility alias; defaults to ``mode='stage'``."""
        payload: Dict[str, Any] = dict(body)
        payload.setdefault("mode", "stage")
        return self.autopilot(payload)  # type: ignore[arg-type]

    def submit_customer_document(
        self,
        customer_ref: str,
        body: ConnectorBusinessDocumentRequest,
        *,
        delivery: str = "send",
        idempotency_key: Optional[str] = None,
    ) -> ConnectorBusinessDocument:
        """Send or stage strict business JSON for an approved customer."""
        if not _connector_trim_string(customer_ref):
            raise ValueError("Connector customerRef is required")
        external_id = _connector_trim_string(str(body.get("externalId") or ""))
        if not external_id:
            raise ValueError("Connector externalId is required")
        if not str(body.get("number") or "").strip():
            raise ValueError("Connector number is required")
        recipient = body.get("recipient")
        if not isinstance(recipient, dict) or not str(recipient.get("country") or "").strip():
            raise ValueError("Connector recipient.country is required")
        if not any(str(recipient.get(key) or "").strip() for key in ("companyId", "taxId", "vatId", "networkId")):
            raise ValueError("Connector recipient requires companyId, taxId, vatId, or networkId")
        lines = body.get("lines")
        if not isinstance(lines, list) or not lines:
            raise ValueError("Connector lines must contain at least one item")
        payload = dict(body)
        normalized_customer_ref = _connector_trim_string(customer_ref)
        payload["customerRef"] = normalized_customer_ref
        payload["externalId"] = external_id
        payload["delivery"] = delivery
        key = (
            _validate_connector_idempotency_key(idempotency_key)
            if idempotency_key is not None
            else _connector_document_idempotency_key(normalized_customer_ref, external_id)
        )
        return self._request(
            "POST",
            "/connector/documents",
            json=payload,
            extra_headers=_idempotency_headers(key),
            omit_firm_id=True,
            retry_on_failure=True,
        )

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
        """Compatibility firm-scoped technical event feed."""
        params = _build_query({"cursor": cursor, "limit": limit})
        return self._request("GET", "/connector/events", params=params)

    def list_customer_events(
        self,
        customer_ref: str,
        *,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorBusinessEventsResponse:
        """List canonical customer-scoped business lifecycle events."""
        if not customer_ref.strip():
            raise ValueError("Connector customerRef is required")
        params = _build_query({"customerRef": customer_ref, "cursor": cursor, "limit": limit})
        return self._request("GET", "/connector/events", params=params, omit_firm_id=True)

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

    def get_document(self, document_id: str, customer_ref: Optional[str] = None) -> Any:
        """Retrieve a Connector document lifecycle snapshot."""
        return self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}",
            params=_build_query({"customerRef": customer_ref}),
            omit_firm_id=True,
        )

    def list_customer_documents(
        self,
        customer_ref: str,
        *,
        direction: Optional[str] = None,
        state: Optional[ConnectorBusinessState] = None,
        type: Optional[ConnectorBusinessDocumentType] = None,
        created_after: Optional[str] = None,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorBusinessDocumentListResponse:
        params = _build_query(
            {
                "customerRef": customer_ref,
                "direction": direction,
                "state": state,
                "type": type,
                "createdAfter": created_after,
                "cursor": cursor,
                "limit": limit,
            }
        )
        return self._request("GET", "/connector/documents", params=params, omit_firm_id=True)

    def acknowledge_document(
        self,
        document_id: str,
        reference: str,
        customer_ref: Optional[str] = None,
    ) -> ConnectorBusinessAcknowledgeResponse:
        if not reference.strip():
            raise ValueError("Connector reference is required")
        return self._request(
            "POST",
            f"/connector/documents/{quote(document_id, safe='')}/acknowledge",
            json={"reference": reference},
            params=_build_query({"customerRef": customer_ref}),
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def respond_document(
        self,
        document_id: str,
        customer_ref: str,
        body: ConnectorInvoiceResponseRequest,
    ) -> ConnectorInvoiceResponseResult:
        normalized_document_id = document_id.strip()
        normalized_customer_ref = _connector_trim_string(customer_ref)
        status = body.get("status")
        note = body.get("note")
        if not normalized_document_id:
            raise ValueError("Connector documentId is required")
        if not normalized_customer_ref:
            raise ValueError("Connector customerRef is required")
        if status not in _CONNECTOR_INVOICE_RESPONSE_STATUSES:
            raise ValueError("Invalid Connector response status")
        if note is not None and not isinstance(note, str):
            raise ValueError("Connector response note must be a string")
        payload: Dict[str, Any] = {"status": status}
        if note is not None:
            payload["note"] = note
        return self._request(
            "POST",
            f"/connector/documents/{quote(normalized_document_id, safe='')}/respond",
            params={"customerRef": normalized_customer_ref},
            json=payload,
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def send_customer_document(self, document_id: str, customer_ref: Optional[str] = None) -> ConnectorBusinessDocument:
        """Send a previously staged customer document."""
        if not document_id.strip():
            raise ValueError("Connector documentId is required")
        return self._request(
            "POST",
            f"/connector/documents/{quote(document_id, safe='')}/send",
            params=_build_query({"customerRef": customer_ref}),
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def cancel_customer_document(self, document_id: str, customer_ref: Optional[str] = None) -> ConnectorBusinessDocument:
        """Cancel a staged customer document before delivery starts."""
        if not document_id.strip():
            raise ValueError("Connector documentId is required")
        return self._request(
            "POST",
            f"/connector/documents/{quote(document_id, safe='')}/cancel",
            params=_build_query({"customerRef": customer_ref}),
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def get_document_ubl(self, document_id: str, customer_ref: Optional[str] = None) -> str:
        """Download a Connector document UBL XML body."""
        response = self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/ubl",
            params=_build_query({"customerRef": customer_ref}),
            raw=True,
            omit_firm_id=True,
        )
        return response.text

    def get_document_evidence(self, document_id: str, customer_ref: Optional[str] = None) -> Dict[str, Any]:
        """Retrieve Connector document delivery evidence."""
        return self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/evidence",
            params=_build_query({"customerRef": customer_ref}),
            omit_firm_id=True,
        )

    def get_document_evidence_bundle(self, document_id: str, customer_ref: Optional[str] = None) -> Dict[str, Any]:
        """Retrieve the Connector evidence bundle manifest."""
        return self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/evidence-bundle",
            params=_build_query({"customerRef": customer_ref}),
            omit_firm_id=True,
        )

    def get_document_support_packet(self, document_id: str, customer_ref: Optional[str] = None) -> Dict[str, Any]:
        """Retrieve the Connector support packet manifest."""
        return self._request(
            "GET",
            f"/connector/documents/{quote(document_id, safe='')}/support-packet",
            params=_build_query({"customerRef": customer_ref}),
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


class ConnectorAdvancedOutboxResource:
    """Protocol-oriented staging queue kept outside the primary document API."""

    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent

    def stage(self, body: ConnectorOutboxStageRequest) -> ConnectorOutboxStageResponse:
        return self._parent.stage_outbox(body)

    def list(
        self,
        *,
        status: Optional[ConnectorOutboxStatus] = None,
        limit: Optional[int] = None,
        offset: Optional[int] = None,
    ) -> ConnectorOutboxListResponse:
        return self._parent.list_outbox(status=status, limit=limit, offset=offset)

    def get(self, outbox_id: str) -> ConnectorOutboxItem:
        return self._parent.get_outbox_item(outbox_id)

    def send(self, outbox_id: str, *, force: Optional[bool] = None) -> ConnectorOutboxItem:
        return self._parent.send_outbox_item(outbox_id, force=force)

    def send_batch(
        self,
        *,
        ids: Optional[list[str]] = None,
        limit: Optional[int] = None,
        force: Optional[bool] = None,
    ) -> ConnectorOutboxBatchSendResponse:
        return self._parent.send_outbox_batch(ids=ids, limit=limit, force=force)

    def cancel(self, outbox_id: str) -> ConnectorOutboxItem:
        return self._parent.cancel_outbox_item(outbox_id)


class ConnectorWebhookResource:
    """One global Connector webhook configuration per integrator."""

    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent

    def get(self) -> ConnectorWebhookConfiguration:
        """Get the integrator's Connector webhook configuration."""
        return self._parent._request(
            "GET",
            "/connector/webhook",
            omit_firm_id=True,
        )

    def configure(
        self,
        url: str,
        events: Optional[list[str]] = None,
    ) -> ConnectorWebhookConfiguration:
        """Create or replace the integrator's Connector webhook."""
        normalized_url = url.strip()
        if not normalized_url:
            raise ValueError("Connector webhook URL is required")
        body: Dict[str, Any] = {"url": normalized_url}
        if events is not None:
            body["events"] = list(events)
        return self._parent._request(
            "PUT",
            "/connector/webhook",
            json=body,
            omit_firm_id=True,
        )

    def delete(self) -> Any:
        """Delete the integrator's Connector webhook configuration."""
        return self._parent._request(
            "DELETE",
            "/connector/webhook",
            omit_firm_id=True,
        )

    def rotate_secret(self) -> Dict[str, Any]:
        """Rotate and return the Connector webhook signing secret."""
        return self._parent._request(
            "POST",
            "/connector/webhook/rotate-secret",
            omit_firm_id=True,
        )

    def test(self, customer_ref: str) -> ConnectorWebhookTestResponse:
        """Send a canonical test event for one approved customer."""
        normalized_customer_ref = _connector_trim_string(customer_ref)
        if not normalized_customer_ref:
            raise ValueError("Connector customerRef is required")
        return self._parent._request(
            "POST",
            "/connector/webhook/test",
            json={"customerRef": normalized_customer_ref},
            omit_firm_id=True,
        )

    def deliveries(
        self,
        *,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
        status: Optional[str] = None,
        customer_ref: Optional[str] = None,
        event_type: Optional[str] = None,
        test: Optional[bool] = None,
        from_time: Optional[str] = None,
        to_time: Optional[str] = None,
    ) -> ConnectorWebhookDeliveriesResponse:
        """List Connector webhook delivery attempts."""
        params = _build_query({
            "cursor": cursor,
            "limit": limit,
            "status": status.upper() if status else None,
            "customerRef": customer_ref,
            "type": event_type,
            "test": str(test).lower() if test is not None else None,
            "from": from_time,
            "to": to_time,
        })
        return self._parent._request(
            "GET",
            "/connector/webhook/deliveries",
            params=params,
            omit_firm_id=True,
        )

    def list_deliveries(self, **filters: Any) -> ConnectorWebhookDeliveriesResponse:
        return self.deliveries(**filters)

    def get_delivery(self, delivery_id: str) -> ConnectorWebhookDeliveryDetail:
        return self._parent._request("GET", f"/connector/webhook/deliveries/{quote(delivery_id, safe='')}", omit_firm_id=True)

    def replay_delivery(self, delivery_id: str, idempotency_key: str, *, confirm_successful_replay: bool = False) -> ConnectorWebhookReplayResult:
        key = idempotency_key.strip()
        if not key:
            raise ValueError("Connector replay idempotency_key is required")
        return self._parent._request(
            "POST",
            f"/connector/webhook/deliveries/{quote(delivery_id, safe='')}/replay",
            json={"confirmSuccessfulReplay": confirm_successful_replay},
            extra_headers={"Idempotency-Key": key},
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def run_test_suite(self, customer_ref: str, idempotency_key: str, *, event: Optional[str] = None, scenarios: Optional[list[str]] = None) -> ConnectorWebhookTestSuiteAccepted:
        customer = _connector_trim_string(customer_ref)
        key = idempotency_key.strip()
        if not customer or not key:
            raise ValueError("Connector customer_ref and idempotency_key are required")
        body: Dict[str, Any] = {"customerRef": customer}
        if event is not None:
            body["event"] = event
        if scenarios is not None:
            body["scenarios"] = list(scenarios)
        return self._parent._request("POST", "/connector/webhook/test-suite", json=body, extra_headers={"Idempotency-Key": key}, omit_firm_id=True, retry_on_failure=True)

    def get_test_suite(self, test_run_id: str) -> ConnectorWebhookTestSuiteStatus:
        return self._parent._request("GET", f"/connector/webhook/test-suite/{quote(test_run_id, safe='')}", omit_firm_id=True)


class ConnectorAdvancedResource:
    """Advanced Connector workflows.

    New ERP integrations should start with
    ``connector.customers.for_customer(ref).documents`` and ``.events``.
    This namespace intentionally contains protocol-level and legacy controls.
    """

    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent
        self.outbox = ConnectorAdvancedOutboxResource(parent)
        self.documents = ConnectorAdvancedDocumentsResource(parent)

    def preflight(self, body: ConnectorPreflightRequest) -> ConnectorPreflightResponse:
        return self._parent.preflight(body)

    def send(
        self,
        body: ConnectorSendRequest,
        *,
        idempotency_key: Optional[str] = None,
    ) -> ConnectorSendResponse:
        return self._parent.send(body, idempotency_key=idempotency_key)

    def status(self, document_id: str) -> ConnectorStatusResponse:
        return self._parent.status(document_id)

    def inbox(
        self,
        *,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorInboxListResponse:
        return self._parent.inbox(cursor=cursor, limit=limit)

    def get_inbox_document(self, document_id: str) -> ConnectorInboxDocument:
        return self._parent.get_inbox_document(document_id)

    def ack(self, document_id: str) -> ConnectorAckResponse:
        return self._parent.ack(document_id)

    def events(
        self,
        *,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorEventsResponse:
        return self._parent.events(cursor=cursor, limit=limit)

    def autopilot(self, body: ConnectorAutopilotRequest) -> ConnectorAutopilotRunResponse:
        return self._parent.autopilot(body)

    def mapper(self, body: ConnectorMapperRequest) -> Dict[str, Any]:
        return self._parent.mapper(body)

    def zen_input(self, body: ConnectorZenInputRequest) -> ConnectorAutopilotRunResponse:
        return self._parent.zen_input(body)

    def get_autopilot_run(self, autopilot_id: str) -> ConnectorAutopilotRunResponse:
        return self._parent.get_autopilot_run(autopilot_id)

    def send_autopilot_run(self, autopilot_id: str) -> ConnectorAutopilotRunResponse:
        return self._parent.send_autopilot_run(autopilot_id)

    def reconcile(
        self,
        *,
        status: Optional[ConnectorReconcileStatus] = None,
        since: Optional[str] = None,
    ) -> ConnectorReconcileResponse:
        return self._parent.reconcile(status=status, since=since)

    def mailboxes(self) -> ConnectorMailboxListResponse:
        return self._parent.mailboxes()

    def repair_mailbox(
        self,
        body: Optional[ConnectorMailboxRepairRequest] = None,
    ) -> Dict[str, Any]:
        return self._parent.repair_mailbox(body)

    def update_mailbox_send_policy(
        self,
        customer_ref: str,
        body: ConnectorSendPolicyOptions,
    ) -> ConnectorMailboxUpdateResponse:
        return self._parent.update_mailbox_send_policy(customer_ref, body)

    def sync(
        self,
        *,
        customer_ref: Optional[str] = None,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorSyncResponse:
        return self._parent.sync(customer_ref=customer_ref, cursor=cursor, limit=limit)

    def run_action(
        self,
        action_id: str,
        body: Optional[ConnectorActionRequest] = None,
    ) -> ConnectorActionResponse:
        return self._parent.run_action(action_id, body)


def _with_customer_ref(customer_ref: str, body: Dict[str, Any]) -> Dict[str, Any]:
    customer_ref = _connector_trim_string(customer_ref)
    current = body.get("customerRef")
    if current is not None and _connector_trim_string(str(current)) != customer_ref:
        raise ValueError("Connector customerRef conflicts with scoped customer")
    scoped = dict(body)
    scoped["customerRef"] = customer_ref
    return scoped


class ConnectorDocumentsResource:
    """Direct document facade kept for source compatibility."""

    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent

    def get(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document(document_id)

    def respond(
        self,
        document_id: str,
        customer_ref: str,
        body: ConnectorInvoiceResponseRequest,
    ) -> ConnectorInvoiceResponseResult:
        return self._parent.respond_document(document_id, customer_ref, body)

    def ubl(self, document_id: str) -> str:
        """Compatibility alias; use ``connector.advanced.documents.ubl``."""
        return self._parent.get_document_ubl(document_id)

    def evidence(self, document_id: str) -> Dict[str, Any]:
        """Compatibility alias; use ``connector.advanced.documents.evidence``."""
        return self._parent.get_document_evidence(document_id)

    def evidence_bundle(self, document_id: str) -> Dict[str, Any]:
        """Compatibility alias; use ``connector.advanced.documents.evidence_bundle``."""
        return self._parent.get_document_evidence_bundle(document_id)

    def support_packet(self, document_id: str) -> Dict[str, Any]:
        """Compatibility alias; use ``connector.advanced.documents.support_packet``."""
        return self._parent.get_document_support_packet(document_id)


class ConnectorAdvancedDocumentsResource:
    """Advanced UBL and evidence artifacts outside the golden business flow."""

    def __init__(self, parent: ConnectorResource, customer_ref: Optional[str] = None) -> None:
        self._parent = parent
        self._customer_ref = customer_ref

    def ubl(self, document_id: str) -> str:
        return self._parent.get_document_ubl(document_id, self._customer_ref)

    def evidence(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document_evidence(document_id, self._customer_ref)

    def evidence_bundle(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document_evidence_bundle(document_id, self._customer_ref)

    def support_packet(self, document_id: str) -> Dict[str, Any]:
        return self._parent.get_document_support_packet(document_id, self._customer_ref)


class ConnectorCustomerDocumentsResource(ConnectorDocumentsResource):
    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        super().__init__(parent)
        self._customer_ref = customer_ref

    def send(
        self,
        body: ConnectorBusinessDocumentRequest,
        *,
        idempotency_key: Optional[str] = None,
    ) -> ConnectorBusinessDocument:
        return self._parent.submit_customer_document(
            self._customer_ref,
            body,
            delivery="send",
            idempotency_key=idempotency_key,
        )

    def stage(
        self,
        body: ConnectorBusinessDocumentRequest,
        *,
        idempotency_key: Optional[str] = None,
    ) -> ConnectorBusinessDocument:
        return self._parent.submit_customer_document(
            self._customer_ref,
            body,
            delivery="stage",
            idempotency_key=idempotency_key,
        )

    def list(
        self,
        *,
        direction: Optional[str] = None,
        state: Optional[ConnectorBusinessState] = None,
        type: Optional[ConnectorBusinessDocumentType] = None,
        created_after: Optional[str] = None,
        cursor: Optional[str] = None,
        limit: Optional[int] = None,
    ) -> ConnectorBusinessDocumentListResponse:
        return self._parent.list_customer_documents(
            self._customer_ref,
            direction=direction,
            state=state,
            type=type,
            created_after=created_after,
            cursor=cursor,
            limit=limit,
        )

    def get(self, document_id: str) -> ConnectorBusinessDocument:
        return self._parent.get_document(document_id, self._customer_ref)

    def respond(
        self,
        document_id: str,
        body: ConnectorInvoiceResponseRequest,
    ) -> ConnectorInvoiceResponseResult:
        return self._parent.respond_document(document_id, self._customer_ref, body)

    def acknowledge(self, document_id: str, reference: str) -> ConnectorBusinessAcknowledgeResponse:
        return self._parent.acknowledge_document(document_id, reference, self._customer_ref)

    def send_document(self, document_id: str) -> ConnectorBusinessDocument:
        return self._parent.send_customer_document(document_id, self._customer_ref)

    def cancel_document(self, document_id: str) -> ConnectorBusinessDocument:
        return self._parent.cancel_customer_document(document_id, self._customer_ref)

    def ubl(self, document_id: str) -> str:
        """Compatibility alias; use ``customer.advanced.documents.ubl``."""
        return self._parent.get_document_ubl(document_id, self._customer_ref)

    def evidence(self, document_id: str) -> Dict[str, Any]:
        """Compatibility alias; use ``customer.advanced.documents.evidence``."""
        return self._parent.get_document_evidence(document_id, self._customer_ref)

    def evidence_bundle(self, document_id: str) -> Dict[str, Any]:
        """Compatibility alias; use ``customer.advanced.documents.evidence_bundle``."""
        return self._parent.get_document_evidence_bundle(document_id, self._customer_ref)

    def support_packet(self, document_id: str) -> Dict[str, Any]:
        """Compatibility alias; use ``customer.advanced.documents.support_packet``."""
        return self._parent.get_document_support_packet(document_id, self._customer_ref)


class ConnectorCustomerEventsResource:
    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        self._parent = parent
        self._customer_ref = customer_ref

    def list(self, *, cursor: Optional[str] = None, limit: Optional[int] = None) -> ConnectorBusinessEventsResponse:
        return self._parent.list_customer_events(self._customer_ref, cursor=cursor, limit=limit)


class ConnectorCustomerMailboxResource:
    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        self._parent = parent
        self._customer_ref = customer_ref

    def repair(self) -> Dict[str, Any]:
        return self._parent.repair_mailbox({"customerRef": self._customer_ref})

    def update_send_policy(self, body: ConnectorSendPolicyOptions) -> ConnectorMailboxUpdateResponse:
        return self._parent.update_mailbox_send_policy(self._customer_ref, body)


class ConnectorCustomerAdvancedResource:
    """Advanced helpers for one manually approved Connector customer.

    Only ``documents`` and preview-only ``mapper`` are available with managed
    Connector credentials; the remaining members are legacy compatibility.
    """

    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        self._parent = parent
        self._customer_ref = customer_ref
        self.documents = ConnectorAdvancedDocumentsResource(parent, customer_ref)
        # Compatibility-only: unavailable with managed Connector credentials.
        self.mailbox = ConnectorCustomerMailboxResource(parent, customer_ref)

    def autopilot(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        """Compatibility-only: unavailable with managed Connector credentials."""
        return self._parent.advanced.autopilot(_with_customer_ref(self._customer_ref, body))  # type: ignore[arg-type]

    def mapper(self, body: ConnectorMapperPreviewRequest) -> Dict[str, Any]:
        """Preview and normalize source data without staging or sending."""
        if body.get("execute") not in (None, "preview"):
            raise ValueError("Connector Mapper only supports preview normalization")
        preview = dict(body)
        preview["execute"] = "preview"
        return self._parent.advanced.mapper(_with_customer_ref(self._customer_ref, preview))  # type: ignore[arg-type]

    def zen_input(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        """Compatibility-only: unavailable with managed Connector credentials."""
        return self._parent.advanced.zen_input(_with_customer_ref(self._customer_ref, body))  # type: ignore[arg-type]

    def sync(self, **params: Any) -> ConnectorSyncResponse:
        """Compatibility-only: unavailable with managed Connector credentials."""
        return self._parent.advanced.sync(customer_ref=self._customer_ref, **params)


class ConnectorCustomerResource:
    def __init__(self, parent: ConnectorResource, customer_ref: str) -> None:
        self._parent = parent
        self._customer_ref = customer_ref
        self.documents = ConnectorCustomerDocumentsResource(parent, customer_ref)
        self.events = ConnectorCustomerEventsResource(parent, customer_ref)
        self.advanced = ConnectorCustomerAdvancedResource(parent, customer_ref)
        # Supported compatibility alias. Use ``customer.advanced.mailbox``.
        self.mailbox = self.advanced.mailbox

    def submit_document(
        self,
        body: ConnectorSubmitDocumentRequest,
    ) -> ConnectorAutopilotRunResponse:
        """Autopilot submit compatibility alias; defaults to ``mode='stage'``."""
        payload = _with_customer_ref(self._customer_ref, dict(body))
        payload.setdefault("mode", "stage")
        return self._parent.autopilot(payload)  # type: ignore[arg-type]

    def autopilot(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        """Compatibility alias; use ``customer.advanced.autopilot``."""
        return self.advanced.autopilot(body)

    def mapper(self, body: Dict[str, Any]) -> Dict[str, Any]:
        """Compatibility alias; use ``customer.advanced.mapper``."""
        return self.advanced.mapper(body)

    def zen_input(self, body: Dict[str, Any]) -> ConnectorAutopilotRunResponse:
        """Compatibility alias; use ``customer.advanced.zen_input``."""
        return self.advanced.zen_input(body)

    def sync(self, **params: Any) -> ConnectorSyncResponse:
        """Compatibility alias; use ``customer.advanced.sync``."""
        return self.advanced.sync(**params)


class ConnectorCustomersResource:
    def __init__(self, parent: ConnectorResource) -> None:
        self._parent = parent

    def for_customer(self, customer_ref: str) -> ConnectorCustomerResource:
        if not _connector_trim_string(customer_ref):
            raise ValueError("Connector customerRef is required")
        return ConnectorCustomerResource(self._parent, _connector_trim_string(customer_ref))
