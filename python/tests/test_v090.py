"""Tests for v0.9.0 additions:
- InboundResource
- OutboundResource
- UblValidationError + UBL_RULES
- webhooks.test() event query param
- WebhookDelivery.idempotency_key field
- client.last_rate_limit
"""

from __future__ import annotations

import datetime
from typing import Any, Callable
from unittest.mock import MagicMock, call, patch

import pytest

from epostak import EPostak, UblValidationError, UBL_RULES
from epostak.errors import build_api_error, EPostakError
from epostak.resources.documents import _parse_rate_limit_headers


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _mock_response(status: int, json_body: Any = None, headers: dict | None = None, text: str = "") -> MagicMock:
    resp = MagicMock()
    resp.status_code = status
    resp.is_success = (200 <= status < 300)
    resp.reason_phrase = "OK" if resp.is_success else "Error"
    resp.headers = headers or {}
    resp.json.return_value = json_body or {}
    resp.text = text
    return resp


def _make_client() -> EPostak:
    """Return an EPostak client with a stubbed token manager."""
    client = EPostak.__new__(EPostak)
    tm = MagicMock()
    tm.get_access_token.return_value = "test-token"
    client._rate_limit_store = []

    import httpx
    http = httpx.Client()

    from epostak.resources.inbound import InboundResource
    from epostak.resources.connector import ConnectorResource
    from epostak.resources.outbound import OutboundResource
    from epostak.resources.webhooks import WebhooksResource

    rl = client._rate_limit_store
    client.connector = ConnectorResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    client.inbound = InboundResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    client.outbound = OutboundResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    client.webhooks = WebhooksResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    return client


def test_events_pull_normalizes_live_events_response():
    from epostak.resources.events import EventsResource

    import httpx

    tm = MagicMock()
    tm.get_access_token.return_value = "test-token"
    events = EventsResource(httpx.Client(), "https://epostak.sk/api/v1", tm, None)

    live_response = {
        "events": [{"event_id": "evt-live", "event": "document.received"}],
        "has_more": False,
    }
    with patch.object(events, "_request", return_value=live_response) as mock_req:
        result = events.pull(limit=10, event_type="document.received")

    mock_req.assert_called_once_with(
        "GET",
        "/events/pull",
        params={"limit": "10", "event_type": "document.received"},
    )
    assert result["items"][0]["event_id"] == "evt-live"


def _make_firm_scoped_connector():
    """Return a ConnectorResource wired to a mocked HTTP client with firm scope."""
    from epostak.resources.connector import ConnectorResource

    http = MagicMock()
    tm = MagicMock()
    tm.get_access_token.return_value = "test-token"
    connector = ConnectorResource(http, "https://epostak.sk/api/v1", tm, "firm-1", max_retries=0)

    response = MagicMock()
    response.status_code = 200
    response.is_success = True
    response.headers = {}
    response.json.return_value = {"ok": True}
    response.text = "<Invoice/>"
    http.request.return_value = response

    return connector, http


def test_box_resource_uses_public_box_paths():
    from epostak.resources.box import BoxResource

    box = BoxResource(MagicMock(), "https://epostak.sk/api/v1", MagicMock(), None, max_retries=0)

    with patch.object(box, "_request", return_value={"items": []}) as mock_req:
        box.list(status="ready", direction="outbound", limit=10, offset=5)
        mock_req.assert_called_once_with(
            "GET",
            "/box/items",
            params={"status": "ready", "direction": "outbound", "limit": "10", "offset": "5"},
        )

    with patch.object(box, "_request", return_value={"ok": True}) as mock_req:
        box.create({
            "payloadXml": "<Invoice/>",
            "scheduledFor": "2026-07-01T00:00:00.000Z",
            "externalId": "erp-doc-1",
            "metadata": {"source": "sdk-test"},
        })
        mock_req.assert_called_once_with(
            "POST",
            "/box/items",
            json={
                "payloadXml": "<Invoice/>",
                "scheduledFor": "2026-07-01T00:00:00.000Z",
                "externalId": "erp-doc-1",
                "metadata": {"source": "sdk-test"},
            },
        )

    with patch.object(box, "_request", return_value={"boxItemId": "box-1"}) as mock_req:
        box.get("box-1")
        mock_req.assert_called_once_with("GET", "/box/items/box-1")

    with patch.object(box, "_request", return_value={"boxItemId": "box-1"}) as mock_req:
        box.schedule("box-1", "2026-07-01T00:00:00.000Z")
        mock_req.assert_called_once_with(
            "POST",
            "/box/items/box-1/schedule",
            json={"scheduledFor": "2026-07-01T00:00:00.000Z"},
        )

    for method_name, expected_path in [
        ("send_now", "/box/items/box-1/send-now"),
        ("retry", "/box/items/box-1/retry"),
        ("cancel", "/box/items/box-1/cancel"),
    ]:
        with patch.object(box, "_request", return_value={"ok": True}) as mock_req:
            getattr(box, method_name)("box-1")
            mock_req.assert_called_once_with("POST", expected_path)


# ---------------------------------------------------------------------------
# UBL_RULES
# ---------------------------------------------------------------------------

def test_ubl_rules_constant():
    assert isinstance(UBL_RULES, tuple)
    assert len(UBL_RULES) == 7
    # The 7 canonical rule codes from lib/ubl/generate.ts (server side).
    # New rules may be added later; the tuple is a known-codes hint, not a
    # closed enum — clients should compare strings, not assume membership.
    assert "BR-06" in UBL_RULES  # Buyer name (the Pavel Horák report case)
    assert "BR-11" in UBL_RULES  # Seller VAT identifier
    assert "PEPPOL-R008" in UBL_RULES


# ---------------------------------------------------------------------------
# UblValidationError
# ---------------------------------------------------------------------------

def test_ubl_validation_error_from_body():
    body = {"error": {"code": "UBL_VALIDATION_ERROR", "message": "BR-06 violated", "rule": "BR-06"}}
    err = UblValidationError(422, body)
    assert err.status == 422
    assert err.rule == "BR-06"
    assert "BR-06" in str(err)


def test_ubl_validation_error_explicit_rule():
    err = UblValidationError(422, {}, rule="BR-09")
    assert err.rule == "BR-09"


def test_ubl_validation_error_no_rule():
    err = UblValidationError(422, {"error": {"code": "UBL_VALIDATION_ERROR", "message": "bad"}})
    assert err.rule is None


def test_ubl_validation_error_request_id_override():
    err = UblValidationError(422, {}, request_id="req-abc")
    assert err.request_id == "req-abc"


def test_build_api_error_raises_ubl_validation_error_on_422():
    body = {"error": {"code": "UBL_VALIDATION_ERROR", "message": "BR-07", "rule": "BR-07"}}
    err = build_api_error(422, body)
    assert isinstance(err, UblValidationError)
    assert err.rule == "BR-07"


def test_build_api_error_does_not_raise_for_other_422():
    body = {"error": {"code": "VALIDATION_FAILED", "message": "bad"}}
    err = build_api_error(422, body)
    assert type(err) is EPostakError


def test_business_error_metadata_and_retry_after_seconds():
    err = build_api_error(
        409,
        {"error": {
            "code": "idempotency_in_flight",
            "message": "Still processing",
            "field": "externalId",
            "nextAction": "retry",
            "retryable": True,
            "requestId": "req-body",
        }},
        {"Retry-After": "7", "X-Request-Id": "req-header"},
    )
    assert err.field == "externalId"
    assert err.next_action == "retry"
    assert err.retryable is True
    assert err.request_id == "req-body"
    assert err.retry_after == 7

    validation = build_api_error(
        422,
        {"error": {"code": "validation_failed", "message": "Fix request", "retryable": False}},
    )
    assert validation.retryable is False
    assert validation.retry_after is None


def test_ubl_validation_error_is_epostak_error():
    err = UblValidationError(422, {})
    assert isinstance(err, EPostakError)


# ---------------------------------------------------------------------------
# ConnectorResource
# ---------------------------------------------------------------------------

@pytest.mark.parametrize(
    "call",
    [
        pytest.param(
            lambda c: c.mapper({"templateKey": "pohoda-csv-v1", "sourceType": "csv", "sourceText": "Doklad"}),
            id="mapper",
        ),
        pytest.param(lambda c: c.zen_input({"customerRef": "erp-customer-1"}), id="zen-input"),
        pytest.param(lambda c: c.autopilot({"customerRef": "erp-customer-1"}), id="autopilot"),
        pytest.param(lambda c: c.get_autopilot_run("auto-1"), id="get-autopilot"),
        pytest.param(lambda c: c.send_autopilot_run("auto-1"), id="send-autopilot"),
        pytest.param(lambda c: c.reconcile(status="exceptions"), id="reconcile"),
        pytest.param(lambda c: c.mailboxes(), id="mailbox"),
        pytest.param(lambda c: c.repair_mailbox({"customerRef": "erp-customer-1"}), id="repair-mailbox"),
        pytest.param(
            lambda c: c.update_mailbox_send_policy("erp-customer-1", {"policy": "daily_batch"}),
            id="send-policy",
        ),
        pytest.param(lambda c: c.sync(customer_ref="erp-customer-1", limit=50), id="sync"),
        pytest.param(lambda c: c.get_document("doc-1"), id="document"),
        pytest.param(lambda c: c.get_document_ubl("doc-1"), id="document-ubl"),
        pytest.param(lambda c: c.get_document_evidence("doc-1"), id="document-evidence"),
        pytest.param(lambda c: c.get_document_evidence_bundle("doc-1"), id="document-evidence-bundle"),
        pytest.param(lambda c: c.run_action("action-1", {"note": "send now"}), id="action"),
    ],
)
def test_connector_v2_omits_global_firm_id_header(call: Callable[[Any], Any]):
    connector, http = _make_firm_scoped_connector()

    call(connector)

    sent_headers = http.request.call_args.kwargs["headers"]
    assert "X-Firm-Id" not in sent_headers


@pytest.mark.parametrize(
    "call",
    [
        pytest.param(lambda c: c.preflight({"invoiceNumber": "FA-1"}), id="preflight"),
        pytest.param(lambda c: c.send({"invoiceNumber": "FA-1"}), id="send"),
        pytest.param(lambda c: c.inbox(limit=20), id="inbox"),
    ],
)
def test_connector_legacy_keeps_global_firm_id_header(call: Callable[[Any], Any]):
    connector, http = _make_firm_scoped_connector()

    call(connector)

    sent_headers = http.request.call_args.kwargs["headers"]
    assert sent_headers["X-Firm-Id"] == "firm-1"


def test_connector_customer_events_omit_firm_while_advanced_legacy_events_keep_it():
    connector, http = _make_firm_scoped_connector()

    connector.customers.for_customer("erp-customer-1").events.list(limit=10)
    customer_headers = http.request.call_args.kwargs["headers"]
    assert "X-Firm-Id" not in customer_headers

    connector.advanced.events(limit=10)
    legacy_headers = http.request.call_args.kwargs["headers"]
    assert legacy_headers["X-Firm-Id"] == "firm-1"


def test_connector_send_calls_correct_endpoint_with_idempotency_key():
    client = _make_client()
    response = {"documentId": "doc-1", "status": "accepted"}

    with patch.object(client.connector, "_request", return_value=response) as mock_req:
        result = client.connector.send(
            {"receiverPeppolId": "0245:1234567890", "document": {"invoiceNumber": "FV-1"}},
            idempotency_key="erp-1",
        )
        mock_req.assert_called_once_with(
            "POST",
            "/connector/send",
            json={"receiverPeppolId": "0245:1234567890", "document": {"invoiceNumber": "FV-1"}},
            extra_headers={"Idempotency-Key": "erp-1"},
        )
        assert result["documentId"] == "doc-1"


def test_connector_inbox_and_events_use_cursor_params():
    client = _make_client()

    with patch.object(client.connector, "_request", return_value={"documents": [], "hasMore": False}) as mock_req:
        client.connector.inbox(cursor="cur-1", limit=25)
        mock_req.assert_called_once_with(
            "GET",
            "/connector/inbox",
            params={"cursor": "cur-1", "limit": "25"},
        )

    with patch.object(client.connector, "_request", return_value={"events": [], "hasMore": False}) as mock_req:
        client.connector.customers.for_customer("erp-customer-1").events.list(limit=10)
        mock_req.assert_called_once_with(
            "GET",
            "/connector/events",
            params={"customerRef": "erp-customer-1", "limit": "10"},
            omit_firm_id=True,
        )


def test_connector_status_get_and_ack_paths():
    client = _make_client()

    with patch.object(client.connector, "_request", return_value={"documentId": "doc-1"}) as mock_req:
        client.connector.status("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/status/doc-1")

    with patch.object(client.connector, "_request", return_value={"documentId": "doc-1"}) as mock_req:
        client.connector.get_inbox_document("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/inbox/doc-1")

    with patch.object(client.connector, "_request", return_value={"acknowledged": True}) as mock_req:
        client.connector.ack("doc-1")
        mock_req.assert_called_once_with(
            "POST",
            "/connector/inbox/doc-1/ack",
            json={},
        )


def test_connector_outbox_paths():
    client = _make_client()

    with patch.object(client.connector, "_request", return_value={"total": 1, "items": []}) as mock_req:
        body = {"items": [{"externalId": "FA-1", "payload": {"invoiceNumber": "FA-1"}}]}
        client.connector.stage_outbox(body)
        mock_req.assert_called_once_with("POST", "/connector/outbox", json=body)

    with patch.object(client.connector, "_request", return_value={"items": []}) as mock_req:
        client.connector.list_outbox(status="blocked", limit=10, offset=20)
        mock_req.assert_called_once_with(
            "GET",
            "/connector/outbox",
            params={"status": "blocked", "limit": "10", "offset": "20"},
        )

    with patch.object(client.connector, "_request", return_value={"outboxId": "outbox-1"}) as mock_req:
        client.connector.get_outbox_item("outbox-1")
        mock_req.assert_called_once_with("GET", "/connector/outbox/outbox-1")

    with patch.object(client.connector, "_request", return_value={"outboxId": "outbox-1"}) as mock_req:
        client.connector.send_outbox_item("outbox-1", force=True)
        mock_req.assert_called_once_with("POST", "/connector/outbox/outbox-1/send", json={"force": True})

    with patch.object(client.connector, "_request", return_value={"results": []}) as mock_req:
        client.connector.send_outbox_batch(ids=["outbox-1"], force=True)
        mock_req.assert_called_once_with("POST", "/connector/outbox/send", json={"ids": ["outbox-1"], "force": True})

    with patch.object(client.connector, "_request", return_value={"outboxId": "outbox-1"}) as mock_req:
        client.connector.cancel_outbox_item("outbox-1")
        mock_req.assert_called_once_with("DELETE", "/connector/outbox/outbox-1")


def test_connector_autopilot_and_reconcile_paths():
    client = _make_client()
    body = {
        "customerRef": "erp-customer-1",
        "mode": "shadow",
        "externalId": "ERP-FA-2026-001",
        "idempotencyKey": "erp-fa-2026-001",
        "payload": {"receiverPeppolId": "0245:1234567890", "invoiceNumber": "FA-2026-001"},
    }

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        client.connector.advanced.autopilot(body)
        mock_req.assert_called_once_with("POST", "/connector/autopilot", json=body, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        client.connector.advanced.get_autopilot_run("auto-1")
        mock_req.assert_called_once_with("GET", "/connector/autopilot/auto-1", omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        client.connector.advanced.send_autopilot_run("auto-1")
        mock_req.assert_called_once_with("POST", "/connector/autopilot/auto-1/send", json={}, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"items": []}) as mock_req:
        client.connector.advanced.reconcile(status="exceptions", since="2026-06-01T00:00:00.000Z")
        mock_req.assert_called_once_with(
            "GET",
            "/connector/reconcile",
            params={"status": "exceptions", "since": "2026-06-01T00:00:00.000Z"},
            omit_firm_id=True,
        )


def test_connector_managed_v2_paths():
    client = _make_client()

    with patch.object(client.connector, "_request", return_value={"ok": True}) as mock_req:
        body = {"templateKey": "pohoda-csv-v1", "sourceType": "csv", "sourceText": "Doklad"}
        client.connector.advanced.mapper(body)
        mock_req.assert_called_once_with("POST", "/connector/mapper", json=body, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        body = {"customerRef": "erp-customer-1", "invoiceNumber": "FA-2026-002", "mode": "stage"}
        client.connector.zen_input(body)
        mock_req.assert_called_once_with("POST", "/connector/zen-input", json=body, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"mailboxes": []}) as mock_req:
        client.connector.mailboxes()
        mock_req.assert_called_once_with("GET", "/connector/mailbox", omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"repaired": True}) as mock_req:
        client.connector.repair_mailbox({"customerRef": "erp-customer-1"})
        mock_req.assert_called_once_with(
            "POST",
            "/connector/mailbox/repair",
            json={"customerRef": "erp-customer-1"},
            omit_firm_id=True,
        )

    with patch.object(client.connector, "_request", return_value={"mailbox": {}}) as mock_req:
        client.connector.update_mailbox_send_policy("erp-customer-1", {"policy": "daily_batch"})
        mock_req.assert_called_once_with(
            "PATCH",
            "/connector/mailbox/erp-customer-1/send-policy",
            json={"policy": "daily_batch"},
            omit_firm_id=True,
        )

    with patch.object(client.connector, "_request", return_value={"items": []}) as mock_req:
        client.connector.sync(customer_ref="erp-customer-1", cursor="cur-1", limit=50)
        mock_req.assert_called_once_with(
            "GET",
            "/connector/sync",
            params={"customerRef": "erp-customer-1", "cursor": "cur-1", "limit": "50"},
            omit_firm_id=True,
        )

    with patch.object(client.connector, "_request", return_value={"documentId": "doc-1"}) as mock_req:
        client.connector.get_document("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1", params={}, omit_firm_id=True)

    raw_response = MagicMock()
    raw_response.text = "<Invoice/>"
    with patch.object(client.connector, "_request", return_value=raw_response) as mock_req:
        assert client.connector.get_document_ubl("doc-1") == "<Invoice/>"
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1/ubl", params={}, raw=True, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"events": []}) as mock_req:
        client.connector.get_document_evidence("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1/evidence", params={}, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"bundle": []}) as mock_req:
        client.connector.get_document_evidence_bundle("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1/evidence-bundle", params={}, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"action": {}}) as mock_req:
        client.connector.run_action("action-1", {"note": "send now"})
        mock_req.assert_called_once_with(
            "POST",
            "/connector/actions/action-1",
            json={"note": "send now"},
            omit_firm_id=True,
        )


def test_peppol_capabilities_uses_participant_envelope():
    from epostak.resources.peppol import PeppolResource

    peppol = PeppolResource(MagicMock(), "https://epostak.sk/api/v1", MagicMock(), None, max_retries=0)
    with patch.object(peppol, "_request", return_value={"found": True, "accepts": True}) as mock_req:
        peppol.capabilities("0245", "2020305606", "urn:invoice")

        mock_req.assert_called_once_with(
            "POST",
            "/peppol/capabilities",
            json={
                "participant": {"scheme": "0245", "identifier": "2020305606"},
                "documentType": "urn:invoice",
            },
        )


def test_major_enterprise_namespace_exposes_full_platform_resources():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    assert client.enterprise.documents is client.documents
    assert client.enterprise.inbox is client.documents.inbox
    assert client.enterprise.pull.inbound is client.inbound
    assert client.enterprise.pull.outbound is client.outbound
    assert client.enterprise.connector is client.connector
    assert client.enterprise.webhooks is client.webhooks
    assert client.connector.advanced.documents is not client.connector.documents
    customer = client.connector.customers.for_customer("erp-customer-1")
    assert customer.mailbox is customer.advanced.mailbox
    with pytest.raises(ValueError, match="customerRef is required"):
        client.connector.customers.for_customer("  ")


def test_connector_global_webhook_facade_never_uses_firm_scope():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )
    webhook = client.connector.webhook

    stored_webhook = {
        "id": "wh-1",
        "url": "https://erp.example/hook",
        "events": ["document.received"],
        "active": True,
        "failedAttempts": 0,
        "createdAt": "2026-07-15T10:00:00Z",
        "updatedAt": "2026-07-15T10:00:00Z",
    }
    with patch.object(client.connector, "_request", return_value={"webhook": stored_webhook}) as mock_req:
        current = webhook.get()
        assert current["webhook"]["id"] == "wh-1"
        mock_req.assert_called_once_with("GET", "/connector/webhook", omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"webhook": stored_webhook, "secret": "a" * 64}) as mock_req:
        configured = webhook.configure(
            "  https://erp.example/hook  ",
            ["document.received", "document.delivered"],
        )
        assert configured["secret"] == "a" * 64
        mock_req.assert_called_once_with(
            "PUT",
            "/connector/webhook",
            json={
                "url": "https://erp.example/hook",
                "events": ["document.received", "document.delivered"],
            },
            omit_firm_id=True,
        )

    with patch.object(client.connector, "_request", return_value={"secret": "whsec_new"}) as mock_req:
        webhook.rotate_secret()
        mock_req.assert_called_once_with(
            "POST",
            "/connector/webhook/rotate-secret",
            omit_firm_id=True,
        )

    pushed_event = {
        "id": "evt-1",
        "type": "document.delivered",
        "customerRef": "erp-customer-1",
        "documentId": "doc-1",
        "state": "delivered",
        "occurredAt": "2026-07-15T10:00:00Z",
        "data": {"customerRef": "erp-customer-1", "direction": "outbound", "type": "invoice", "number": None, "response": None},
        "test": True,
    }
    with patch.object(
        client.connector,
        "_request",
        return_value={"deliveryId": "whd-1", "status": "queued", "event": pushed_event},
    ) as mock_req:
        result = webhook.test("\u00a0erp-customer-1\ufeff")
        assert result["deliveryId"] == "whd-1"
        assert result["event"]["customerRef"] == "erp-customer-1"
        assert result["event"]["data"]["response"] is None
        assert result["event"]["test"] is True
        mock_req.assert_called_once_with(
            "POST",
            "/connector/webhook/test",
            json={"customerRef": "erp-customer-1"},
            omit_firm_id=True,
        )

    with patch.object(
        client.connector,
        "_request",
        return_value={"deliveries": [], "nextCursor": None, "hasMore": False},
    ) as mock_req:
        webhook.deliveries(cursor="next", limit=25, status="failed")
        mock_req.assert_called_once_with(
            "GET",
            "/connector/webhook/deliveries",
            params={"cursor": "next", "limit": "25", "status": "FAILED"},
            omit_firm_id=True,
        )

    with patch.object(client.connector, "_request", return_value=None) as mock_req:
        webhook.delete()
        mock_req.assert_called_once_with("DELETE", "/connector/webhook", omit_firm_id=True)

    with pytest.raises(ValueError, match="webhook URL is required"):
        webhook.configure("  ")
    with pytest.raises(ValueError, match="customerRef is required"):
        webhook.test("  ")


def test_major_connector_customer_documents_send_defaults_to_send_without_firm_id():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    with patch.object(client.connector, "_request", return_value={"id": "doc-1"}) as mock_req:
        client.connector.customers.for_customer("erp-customer-1").documents.send(
            {
                "externalId": "FA-1",
                "type": "invoice",
                "number": "FA-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
            }
        )
        mock_req.assert_called_once_with(
            "POST",
            "/connector/documents",
            json={
                "externalId": "FA-1",
                "type": "invoice",
                "number": "FA-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
                "customerRef": "erp-customer-1",
                "delivery": "send",
            },
            extra_headers={
                "Idempotency-Key": "connector:v1:f7be06badbccd0670a25e6df7fd654fd45ae7291d5f5043257806adc0b107045"
            },
                omit_firm_id=True,
                retry_on_failure=True,
        )


def test_connector_customer_stage_filtered_list_and_invoice_response_wire_contract():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )
    stage_response = {"id": "doc-stage-1", "state": "queued"}
    list_response = {"documents": [], "nextCursor": "cur-2", "hasMore": True}
    respond_response = {
        "id": "doc-in-1",
        "customerRef": "erp-customer-1",
        "response": {
            "status": "accepted",
            "direction": "sent",
            "delivery": "queued",
            "respondedAt": "2026-07-15T12:00:00Z",
        },
        "idempotent": True,
    }

    with patch.object(
        client.connector,
        "_request",
        side_effect=[stage_response, list_response, respond_response],
    ) as mock_req:
        documents = client.connector.customers.for_customer("erp-customer-1").documents
        documents.stage(
            {
                "externalId": "FA-STAGE-1",
                "type": "invoice",
                "number": "FA-STAGE-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
            },
            idempotency_key="connector-stage-key",
        )
        page = documents.list(
            direction="inbound",
            state="received",
            type="invoice",
            created_after="2026-07-01T00:00:00Z",
            cursor="cur-1",
            limit=25,
        )
        result = documents.respond(
            "doc-in-1",
            {"status": "accepted", "note": "Imported into ERP"},
        )

    document = {
        "externalId": "FA-STAGE-1",
        "type": "invoice",
        "number": "FA-STAGE-1",
        "recipient": {"country": "SK", "taxId": "2120123456"},
        "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
        "customerRef": "erp-customer-1",
        "delivery": "stage",
    }
    assert mock_req.call_args_list == [
        call(
            "POST",
            "/connector/documents",
            json=document,
            extra_headers={"Idempotency-Key": "connector-stage-key"},
            omit_firm_id=True,
            retry_on_failure=True,
        ),
        call(
            "GET",
            "/connector/documents",
            params={
                "customerRef": "erp-customer-1",
                "direction": "inbound",
                "state": "received",
                "type": "invoice",
                "createdAfter": "2026-07-01T00:00:00Z",
                "cursor": "cur-1",
                "limit": "25",
            },
            omit_firm_id=True,
        ),
        call(
            "POST",
            "/connector/documents/doc-in-1/respond",
            params={"customerRef": "erp-customer-1"},
            json={"status": "accepted", "note": "Imported into ERP"},
            omit_firm_id=True,
            retry_on_failure=True,
        ),
    ]
    assert page["nextCursor"] == "cur-2"
    assert page["hasMore"] is True
    assert result["response"] == {
        "status": "accepted",
        "direction": "sent",
        "delivery": "queued",
        "respondedAt": "2026-07-15T12:00:00Z",
    }
    assert result["idempotent"] is True


def test_connector_invoice_response_omits_none_note_from_wire_payload():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    with patch.object(client.connector, "_request", return_value={"id": "doc-in-1"}) as mock_req:
        client.connector.customers.for_customer("erp-customer-1").documents.respond(
            "doc-in-1",
            {"status": "accepted", "note": None},
        )

    mock_req.assert_called_once_with(
        "POST",
        "/connector/documents/doc-in-1/respond",
        params={"customerRef": "erp-customer-1"},
        json={"status": "accepted"},
        omit_firm_id=True,
        retry_on_failure=True,
    )


def test_connector_submit_compatibility_alias_keeps_autopilot_stage_semantics_without_mutation():
    client = _make_client()
    body = {"externalId": "legacy-1", "payload": {"invoiceNumber": "FA-1"}}

    with patch.object(client.connector, "_request", return_value={"autopilotId": "run-1"}) as mock_req:
        client.connector.customers.for_customer("erp-customer-1").submit_document(body)

    mock_req.assert_called_once_with(
        "POST",
        "/connector/autopilot",
        json={
            "externalId": "legacy-1",
            "payload": {"invoiceNumber": "FA-1"},
            "customerRef": "erp-customer-1",
            "mode": "stage",
        },
        omit_firm_id=True,
    )
    assert "customerRef" not in body
    assert "mode" not in body


def test_connector_legacy_and_business_event_models_and_scopes_are_distinct():
    from epostak.resources.connector import ConnectorAdvancedResource, ConnectorCustomerEventsResource

    assert ConnectorCustomerEventsResource.list.__annotations__["return"] == "ConnectorBusinessEventsResponse"
    assert ConnectorAdvancedResource.events.__annotations__["return"] == "ConnectorEventsResponse"

    connector, _ = _make_firm_scoped_connector()
    business_fixture = {
        "events": [{
            "id": "evt-1",
            "customerRef": "erp-customer-1",
            "state": "delivered",
            "data": {"customerRef": "erp-customer-1", "direction": "outbound", "type": "invoice", "number": None, "response": None},
        }],
        "nextCursor": None,
        "hasMore": False,
    }
    legacy_fixture = {
        "events": [{"id": "legacy-1", "status": "DELIVERED", "data": {"transport": "as4"}}],
        "nextCursor": None,
        "hasMore": False,
    }

    with patch.object(connector, "_request", return_value=business_fixture) as mock_req:
        business = connector.customers.for_customer("erp-customer-1").events.list()
        assert mock_req.call_args.kwargs["omit_firm_id"] is True
    assert business["events"][0]["state"] == "delivered"
    assert business["events"][0]["customerRef"] == "erp-customer-1"
    assert business["events"][0]["data"]["customerRef"] == "erp-customer-1"
    assert business["events"][0]["data"]["number"] is None
    assert business["events"][0]["data"]["response"] is None

    with patch.object(connector, "_request", return_value=legacy_fixture) as mock_req:
        legacy = connector.advanced.events()
        assert "omit_firm_id" not in mock_req.call_args.kwargs
    assert legacy["events"][0]["status"] == "DELIVERED"


def test_connector_business_model_fixture_and_advanced_document_home():
    client = _make_client()
    body = {
        "externalId": "FA-1",
        "number": "FA-1",
        "buyerReference": "PO-7",
        "prepaidAmount": 50,
        "recipient": {
            "country": "SK",
            "taxId": "2120123456",
            "address": {"street": "Hlavna 1", "city": "Bratislava", "postalCode": "81101"},
        },
        "prepayments": [{"advanceInvoiceRef": "ADV-1", "amountWithVat": 50}],
        "lines": [{
            "description": "Licence",
            "quantity": 1,
            "unitPrice": 100,
            "vatRate": 23,
            "discount": 5,
            "deliveryDate": "2026-07-14",
        }],
        "attachments": [{"fileName": "terms.pdf", "mimeType": "application/pdf", "content": "YQ=="}],
    }
    response = {
        "id": "doc-1",
        "customerRef": "erp-customer-1",
        "direction": "outbound",
        "type": "invoice",
        "state": "queued",
        "amounts": {"withoutTax": 100, "tax": 23, "total": 123, "due": 73},
        "sender": {"name": "Sender", "country": "SK", "resolution": "verified"},
        "recipient": {"name": "Buyer", "country": "SK", "resolution": "verified"},
        "issueDate": "2026-07-14",
        "dueDate": "2026-07-28",
        "processedAt": "2026-07-14T10:00:00Z",
        "processedReference": "ERP-OK",
    }
    with patch.object(client.connector, "_request", return_value=response) as mock_req:
        result = client.connector.customers.for_customer("erp-customer-1").documents.send(body)
    assert mock_req.call_args.kwargs["json"]["recipient"]["address"]["city"] == "Bratislava"
    assert mock_req.call_args.kwargs["json"]["lines"][0]["discount"] == 5
    assert result["amounts"]["due"] == 73
    assert result["sender"]["name"] == "Sender"
    assert result["processedReference"] == "ERP-OK"

    customer = client.connector.customers.for_customer("erp-customer-1")
    raw = MagicMock(text="<Invoice/>")
    with patch.object(client.connector, "_request", return_value=raw) as mock_req:
        assert customer.advanced.documents.ubl("doc-1") == "<Invoice/>"
        mock_req.assert_called_once_with(
            "GET",
            "/connector/documents/doc-1/ubl",
            params={"customerRef": "erp-customer-1"},
            raw=True,
            omit_firm_id=True,
        )
    assert "Compatibility alias" in (customer.documents.ubl.__doc__ or "")


def test_connector_document_idempotency_key_vectors_and_explicit_override():
    from epostak.resources.connector import _connector_document_idempotency_key

    keys = [
        _connector_document_idempotency_key("a:b", "c"),
        _connector_document_idempotency_key("a", "b:c"),
        _connector_document_idempotency_key("c" * 255, "e" * 255),
        _connector_document_idempotency_key("\u00a0\ufeffzákazník😀\ufeff\u00a0", "\ufeffFA-žltý-1\u00a0"),
        _connector_document_idempotency_key("\u0085zákazník😀\u0085", "\u0085FA-žltý-1\u0085"),
    ]
    assert keys == [
        "connector:v1:540e8f1c5ae653a7d7e2fe88f7eb8dcabea924d661b1542ad191bb1848e0c33d",
        "connector:v1:e482a79a788392ccae4952360dd438820641e4c162b4952b42d35e78260d70be",
        "connector:v1:7182fd43682e0689adf34c908bc3ec162aaf1687c167fdbff714ff43daa4b111",
        "connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd",
        "connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080",
    ]
    assert all(len(key) == 77 for key in keys)
    assert keys[0] != keys[1]

    client = _make_client()
    with patch.object(client.connector, "_request", return_value={"id": "doc-1"}) as mock_req:
        client.connector.customers.for_customer("customer").documents.send(
            {
                "externalId": "external",
                "type": "invoice",
                "number": "FA-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 1, "vatRate": 23}],
            },
            idempotency_key="caller-key",
        )
        assert mock_req.call_args.kwargs["extra_headers"] == {"Idempotency-Key": "caller-key"}

    with patch.object(client.connector, "_request", return_value={"id": "doc-1"}) as mock_req:
        client.connector.customers.for_customer("\u00a0\ufeffzákazník😀\ufeff\u00a0").documents.send(
            {
                "externalId": "\ufeffFA-žltý-1\u00a0",
                "number": "FA-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 1, "vatRate": 23}],
            }
        )
        assert mock_req.call_args.kwargs["json"]["customerRef"] == "zákazník😀"
        assert mock_req.call_args.kwargs["json"]["externalId"] == "FA-žltý-1"
        assert mock_req.call_args.kwargs["extra_headers"] == {
            "Idempotency-Key": "connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd"
        }

    with patch.object(client.connector, "_request", return_value={"id": "doc-1"}) as mock_req:
        client.connector.customers.for_customer("\u0085zákazník😀\u0085").documents.send(
            {
                "externalId": "\u0085FA-žltý-1\u0085",
                "number": "FA-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 1, "vatRate": 23}],
            }
        )
        assert mock_req.call_args.kwargs["json"]["customerRef"] == "\u0085zákazník😀\u0085"
        assert mock_req.call_args.kwargs["json"]["externalId"] == "\u0085FA-žltý-1\u0085"
        assert mock_req.call_args.kwargs["extra_headers"] == {
            "Idempotency-Key": "connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080"
        }

    with pytest.raises(ValueError, match="1-255 UTF-8 bytes"):
        client.connector.customers.for_customer("customer").documents.send(
            {
                "externalId": "empty-key",
                "number": "FA-1",
                "recipient": {"country": "SK", "taxId": "2120123456"},
                "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 1, "vatRate": 23}],
            },
            idempotency_key="",
        )


def test_connector_retries_keyed_and_lifecycle_posts_but_not_409():
    client = _make_client()
    client.connector._max_retries = 1
    body = {
        "externalId": "FA-retry",
        "number": "FA-retry",
        "recipient": {"country": "SK", "taxId": "2120123456"},
        "lines": [{"description": "Original", "quantity": 1, "unitPrice": 1, "vatRate": 23}],
    }
    responses = [
        _mock_response(503, {"error": {"code": "temporary", "message": "retry"}}, {"Retry-After": "0"}),
        _mock_response(201, {"id": "doc-1", "state": "queued"}),
    ]

    def request_side_effect(*args: Any, **kwargs: Any) -> MagicMock:
        response = responses.pop(0)
        if response.status_code == 503:
            body["lines"][0]["description"] = "Mutated"
        return response

    with patch.object(client.connector._client, "request", side_effect=request_side_effect) as request_mock, \
         patch.object(client.connector, "_sleep_for_retry"):
        client.connector.customers.for_customer("customer").documents.send(body)
    assert request_mock.call_count == 2
    first_json = request_mock.call_args_list[0].kwargs["json"]
    second_json = request_mock.call_args_list[1].kwargs["json"]
    assert first_json == second_json
    assert first_json["lines"][0]["description"] == "Original"
    assert request_mock.call_args_list[0].kwargs["headers"]["Idempotency-Key"] == request_mock.call_args_list[1].kwargs["headers"]["Idempotency-Key"]

    lifecycle_responses = [
        _mock_response(503, {"error": {"code": "temporary", "message": "retry"}}, {"Retry-After": "0"}),
        _mock_response(200, {"id": "doc-1", "state": "cancelled"}),
    ]
    with patch.object(client.connector._client, "request", side_effect=lifecycle_responses) as lifecycle_mock, \
         patch.object(client.connector, "_sleep_for_retry"):
        client.connector.customers.for_customer("customer").documents.cancel_document("doc-1")
    assert lifecycle_mock.call_count == 2

    conflict = _mock_response(
        409,
        {"error": {"code": "idempotency_in_flight", "message": "busy", "retryable": True}},
        {"Retry-After": "0"},
    )
    with patch.object(client.connector._client, "request", return_value=conflict) as conflict_mock, \
         patch.object(client.connector, "_sleep_for_retry"):
        with pytest.raises(EPostakError) as caught:
            client.connector.customers.for_customer("customer").documents.send({**body, "externalId": "FA-conflict"})
    assert caught.value.status == 409
    assert conflict_mock.call_count == 1


def test_connector_retries_transport_failure_but_sapi_post_surfaces_once():
    import httpx

    connector, connector_http = _make_firm_scoped_connector()
    connector._max_retries = 1
    connector_http.request.side_effect = [
        httpx.ConnectError(
            "socket reset",
            request=httpx.Request("POST", "https://epostak.sk/api/v1/connector/documents"),
        ),
        _mock_response(201, {"id": "doc-transport", "state": "queued"}),
    ]
    body = {
        "externalId": "FA-TRANSPORT-1",
        "type": "invoice",
        "number": "FA-TRANSPORT-1",
        "recipient": {"country": "SK", "taxId": "2120123456"},
        "lines": [{"description": "Licence", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
    }

    with patch.object(connector, "_sleep_for_retry"):
        connector.customers.for_customer("erp-customer-1").documents.stage(
            body,
            idempotency_key="connector-transport-key",
        )

    assert connector_http.request.call_count == 2
    first = connector_http.request.call_args_list[0]
    second = connector_http.request.call_args_list[1]
    assert first == second
    assert first.args == ("POST", "https://epostak.sk/api/v1/connector/documents")
    assert first.kwargs["headers"]["Idempotency-Key"] == "connector-transport-key"
    assert "X-Firm-Id" not in first.kwargs["headers"]
    assert first.kwargs["json"]["customerRef"] == "erp-customer-1"
    assert first.kwargs["json"]["delivery"] == "stage"

    from epostak.resources.sapi import SapiResource

    sapi_http = MagicMock()
    token_manager = MagicMock()
    token_manager.get_access_token.return_value = "test-token"
    sapi = SapiResource(
        sapi_http,
        "https://epostak.sk/api/v1",
        token_manager,
        "firm-1",
        max_retries=1,
    )
    sapi_http.request.side_effect = httpx.ConnectError(
        "socket reset",
        request=httpx.Request("POST", "https://epostak.sk/sapi/v1/document/send"),
    )

    with pytest.raises(EPostakError) as caught:
        sapi.participants.for_participant("0245:1234567890").documents.send(
            {"xml": "<Invoice/>"},
            idempotency_key="sapi-transport-key",
        )
    assert caught.value.status == 0
    assert sapi_http.request.call_count == 1


def test_connector_customer_documents_send_and_cancel_staged_without_body():
    client = _make_client()
    documents = client.connector.customers.for_customer("erp-customer-1").documents
    with pytest.raises(ValueError, match="documentId is required"):
        documents.send_document("  ")

    with patch.object(client.connector, "_request", return_value={"id": "doc-1"}) as mock_req:
        documents.send_document("doc-1")
        mock_req.assert_called_once_with(
            "POST",
            "/connector/documents/doc-1/send",
            params={"customerRef": "erp-customer-1"},
            omit_firm_id=True,
            retry_on_failure=True,
        )

    with patch.object(client.connector, "_request", return_value={"id": "doc-2"}) as mock_req:
        documents.cancel_document("doc-2")
        mock_req.assert_called_once_with(
            "POST",
            "/connector/documents/doc-2/cancel",
            params={"customerRef": "erp-customer-1"},
            omit_firm_id=True,
            retry_on_failure=True,
        )


def test_connector_customer_point_operations_bind_customer_ref():
    client = _make_client()
    customer = client.connector.customers.for_customer("customer A/1")
    raw = MagicMock(text="<Invoice/>")
    responses = [
        {"id": "customer-b-doc"},
        {"id": "customer-b-doc"},
        {"id": "customer-b-doc"},
        {"id": "customer-b-doc"},
        raw,
        {"events": []},
        {"bundle": []},
        {"packet": []},
    ]
    with patch.object(client.connector, "_request", side_effect=responses) as mock_req:
        customer.documents.get("customer-b-doc")
        customer.documents.acknowledge("customer-b-doc", "erp-import")
        customer.documents.send_document("customer-b-doc")
        customer.documents.cancel_document("customer-b-doc")
        customer.advanced.documents.ubl("customer-b-doc")
        customer.advanced.documents.evidence("customer-b-doc")
        customer.advanced.documents.evidence_bundle("customer-b-doc")
        customer.advanced.documents.support_packet("customer-b-doc")

    for call in mock_req.call_args_list:
        assert call.kwargs["params"] == {"customerRef": "customer A/1"}
        assert call.kwargs["omit_firm_id"] is True
    assert mock_req.call_args_list[1].kwargs["json"] == {"reference": "erp-import"}
    assert "json" not in mock_req.call_args_list[2].kwargs
    assert "json" not in mock_req.call_args_list[3].kwargs


def test_major_connector_customer_mapper_injects_customer_ref_without_firm_id():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    with patch.object(client.connector, "_request", return_value={"ok": True}) as mock_req:
        client.connector.customers.for_customer("erp-customer-1").advanced.mapper(
            {
                "templateKey": "pohoda-csv-v1",
                "sourceType": "csv",
                "sourceText": "Doklad,PeppolID\nFA-1,0245:1234567890",
            }
        )
        mock_req.assert_called_once_with(
            "POST",
            "/connector/mapper",
            json={
                "templateKey": "pohoda-csv-v1",
                "sourceType": "csv",
                "sourceText": "Doklad,PeppolID\nFA-1,0245:1234567890",
                "customerRef": "erp-customer-1",
                "execute": "preview",
            },
            omit_firm_id=True,
        )
    with pytest.raises(ValueError, match="only supports preview normalization"):
        client.connector.customers.for_customer("erp-customer-1").advanced.mapper({"execute": "send"})  # type: ignore[typeddict-item]


def test_major_sapi_participant_documents_send_sets_required_headers():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    with patch.object(client.sapi, "_request", return_value={"documentId": "sapi-1"}) as mock_req:
        client.sapi.participants.for_participant("0245:1234567890").documents.send(
            {"xml": "<Invoice/>"},
            idempotency_key="sapi-fa-1",
        )
        mock_req.assert_called_once_with(
            "POST",
            "/sapi/v1/document/send",
            json={"xml": "<Invoice/>"},
            extra_headers={
                "X-Peppol-Participant-Id": "0245:1234567890",
                "Idempotency-Key": "sapi-fa-1",
            },
        )


# ---------------------------------------------------------------------------
# InboundResource
# ---------------------------------------------------------------------------

def test_inbound_list_calls_correct_endpoint():
    client = _make_client()
    list_response = {"documents": [], "next_cursor": None, "has_more": False}

    with patch.object(client.inbound, "_request", return_value=list_response) as mock_req:
        result = client.inbound.list(limit=50, kind="invoice")
        mock_req.assert_called_once_with(
            "GET", "/inbound/documents", params={"limit": "50", "kind": "invoice"}
        )
        assert result["has_more"] is False


def test_inbound_get_calls_correct_endpoint():
    client = _make_client()
    doc = {"id": "doc-1", "kind": "invoice", "status": "RECEIVED"}

    with patch.object(client.inbound, "_request", return_value=doc) as mock_req:
        result = client.inbound.get("doc-1")
        mock_req.assert_called_once_with("GET", "/inbound/documents/doc-1")
        assert result["id"] == "doc-1"


def test_inbound_get_ubl_returns_text():
    client = _make_client()
    xml_text = "<Invoice>test</Invoice>"
    mock_resp = MagicMock()
    mock_resp.text = xml_text

    with patch.object(client.inbound, "_request", return_value=mock_resp):
        result = client.inbound.get_ubl("doc-1")
        assert result == xml_text


def test_inbound_ack_sends_post_with_client_reference():
    client = _make_client()
    ack_response = {"id": "doc-1", "client_acked_at": "2026-05-12T10:00:00Z"}

    with patch.object(client.inbound, "_request", return_value=ack_response) as mock_req:
        result = client.inbound.ack("doc-1", client_reference="PO-001")
        mock_req.assert_called_once_with(
            "POST",
            "/inbound/documents/doc-1/ack",
            json={"client_reference": "PO-001"},
            extra_headers=None,
        )
        assert result["id"] == "doc-1"


def test_inbound_ack_empty_body_when_no_reference():
    client = _make_client()

    with patch.object(client.inbound, "_request", return_value={}) as mock_req:
        client.inbound.ack("doc-2")
        _, call_kwargs = mock_req.call_args
        assert call_kwargs.get("json") == {}


# ---------------------------------------------------------------------------
# OutboundResource
# ---------------------------------------------------------------------------

def test_outbound_list_calls_correct_endpoint():
    client = _make_client()
    response = {"documents": [], "next_cursor": None, "has_more": False}

    with patch.object(client.outbound, "_request", return_value=response) as mock_req:
        client.outbound.list(status="DELIVERED", limit=100)
        mock_req.assert_called_once_with(
            "GET", "/outbound/documents", params={"status": "DELIVERED", "limit": "100"}
        )


def test_outbound_get_calls_correct_endpoint():
    client = _make_client()
    doc = {"id": "out-1", "status": "SENT", "attempt_history": []}

    with patch.object(client.outbound, "_request", return_value=doc) as mock_req:
        result = client.outbound.get("out-1")
        mock_req.assert_called_once_with("GET", "/outbound/documents/out-1")
        assert result["status"] == "SENT"


def test_outbound_get_ubl_returns_text():
    client = _make_client()
    xml_text = "<Invoice>out</Invoice>"
    mock_resp = MagicMock()
    mock_resp.text = xml_text

    with patch.object(client.outbound, "_request", return_value=mock_resp):
        result = client.outbound.get_ubl("out-1")
        assert result == xml_text


def test_outbound_events_with_document_id_filter():
    client = _make_client()
    response = {"events": [], "next_cursor": None, "has_more": False}

    with patch.object(client.outbound, "_request", return_value=response) as mock_req:
        client.outbound.events(document_id="doc-1")
        mock_req.assert_called_once_with(
            "GET", "/outbound/events", params={"document_id": "doc-1"}
        )


# ---------------------------------------------------------------------------
# webhooks.test() — event query param
# ---------------------------------------------------------------------------

def test_webhooks_test_sends_event_as_query_param():
    client = _make_client()

    with patch.object(client.webhooks, "_request", return_value={"success": True}) as mock_req:
        client.webhooks.test("wh-1", event="document.received")
        _, kwargs = mock_req.call_args
        assert kwargs.get("params") == {"event": "document.received"}
        assert kwargs.get("json") == {"event": "document.received"}


def test_webhooks_test_no_event_passes_no_params():
    client = _make_client()

    with patch.object(client.webhooks, "_request", return_value={"success": True}) as mock_req:
        client.webhooks.test("wh-1")
        _, kwargs = mock_req.call_args
        assert kwargs.get("params") is None
        assert kwargs.get("json") == {}


# ---------------------------------------------------------------------------
# WebhookDelivery.idempotency_key field
# ---------------------------------------------------------------------------

def test_webhook_delivery_type_has_idempotency_key():
    from epostak.types import WebhookDelivery
    # Check the field is in the TypedDict annotations
    annotations = WebhookDelivery.__annotations__
    assert "idempotency_key" in annotations


# ---------------------------------------------------------------------------
# client.last_rate_limit
# ---------------------------------------------------------------------------

def test_parse_rate_limit_headers_parses_correctly():
    headers = {
        "x-ratelimit-limit": "200",
        "x-ratelimit-remaining": "150",
        "x-ratelimit-reset": "1747040000",
    }
    result = _parse_rate_limit_headers(headers)
    assert result is not None
    assert result["limit"] == 200
    assert result["remaining"] == 150
    assert isinstance(result["reset_at"], datetime.datetime)
    assert result["reset_at"].tzinfo is datetime.timezone.utc


def test_parse_rate_limit_headers_returns_none_when_absent():
    result = _parse_rate_limit_headers({})
    assert result is None


def test_parse_rate_limit_headers_returns_none_on_partial():
    result = _parse_rate_limit_headers({"x-ratelimit-limit": "200"})
    assert result is None


def test_last_rate_limit_is_none_before_request():
    client = _make_client()
    assert client.last_rate_limit is None


def test_last_rate_limit_populated_after_request():
    client = _make_client()
    raw_headers = {
        "x-ratelimit-limit": "200",
        "x-ratelimit-remaining": "99",
        "x-ratelimit-reset": "1747040000",
        "content-type": "application/json",
    }

    list_response = {"documents": [], "next_cursor": None, "has_more": False}
    fake_http_resp = MagicMock()
    fake_http_resp.is_success = True
    fake_http_resp.status_code = 200
    # Use a MagicMock for headers so .get() can be mocked
    mock_headers = MagicMock()
    mock_headers.get = lambda k, d=None: raw_headers.get(k, d)
    fake_http_resp.headers = mock_headers
    fake_http_resp.json.return_value = list_response

    with patch.object(client.inbound._client, "request", return_value=fake_http_resp):
        client.inbound.list()

    rl = client.last_rate_limit
    assert rl is not None
    assert rl["limit"] == 200
    assert rl["remaining"] == 99
