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
from unittest.mock import MagicMock, patch

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
    resp.headers.get = lambda k, default=None: (headers or {}).get(k, default)
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
        pytest.param(lambda c: c.events(limit=20), id="events"),
    ],
)
def test_connector_legacy_keeps_global_firm_id_header(call: Callable[[Any], Any]):
    connector, http = _make_firm_scoped_connector()

    call(connector)

    sent_headers = http.request.call_args.kwargs["headers"]
    assert sent_headers["X-Firm-Id"] == "firm-1"


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
        client.connector.events(limit=10)
        mock_req.assert_called_once_with(
            "GET",
            "/connector/events",
            params={"limit": "10"},
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
        client.connector.autopilot(body)
        mock_req.assert_called_once_with("POST", "/connector/autopilot", json=body, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        client.connector.get_autopilot_run("auto-1")
        mock_req.assert_called_once_with("GET", "/connector/autopilot/auto-1", omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        client.connector.send_autopilot_run("auto-1")
        mock_req.assert_called_once_with("POST", "/connector/autopilot/auto-1/send", json={}, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"items": []}) as mock_req:
        client.connector.reconcile(status="exceptions", since="2026-06-01T00:00:00.000Z")
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
        client.connector.mapper(body)
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
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1", omit_firm_id=True)

    raw_response = MagicMock()
    raw_response.text = "<Invoice/>"
    with patch.object(client.connector, "_request", return_value=raw_response) as mock_req:
        assert client.connector.get_document_ubl("doc-1") == "<Invoice/>"
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1/ubl", raw=True, omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"events": []}) as mock_req:
        client.connector.get_document_evidence("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1/evidence", omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"bundle": []}) as mock_req:
        client.connector.get_document_evidence_bundle("doc-1")
        mock_req.assert_called_once_with("GET", "/connector/documents/doc-1/evidence-bundle", omit_firm_id=True)

    with patch.object(client.connector, "_request", return_value={"action": {}}) as mock_req:
        client.connector.run_action("action-1", {"note": "send now"})
        mock_req.assert_called_once_with(
            "POST",
            "/connector/actions/action-1",
            json={"note": "send now"},
            omit_firm_id=True,
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


def test_major_connector_customer_submit_document_defaults_to_stage_without_firm_id():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    with patch.object(client.connector, "_request", return_value={"autopilotId": "auto-1"}) as mock_req:
        client.enterprise.connector.customers.for_customer("erp-customer-1").submit_document(
            {
                "externalId": "FA-1",
                "idempotencyKey": "erp-fa-1",
                "payload": {"invoiceNumber": "FA-1"},
            }
        )
        mock_req.assert_called_once_with(
            "POST",
            "/connector/autopilot",
            json={
                "externalId": "FA-1",
                "idempotencyKey": "erp-fa-1",
                "payload": {"invoiceNumber": "FA-1"},
                "customerRef": "erp-customer-1",
                "mode": "stage",
            },
            omit_firm_id=True,
        )


def test_major_connector_customer_mapper_injects_customer_ref_without_firm_id():
    client = EPostak(
        client_id="sk_int_test",
        client_secret="sk_int_test",
        base_url="https://dev.epostak.sk/api/v1",
        firm_id="firm-1",
    )

    with patch.object(client.connector, "_request", return_value={"ok": True}) as mock_req:
        client.enterprise.connector.customers.for_customer("erp-customer-1").mapper(
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
            },
            omit_firm_id=True,
        )


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
