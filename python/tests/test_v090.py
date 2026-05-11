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
from typing import Any
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
    from epostak.resources.outbound import OutboundResource
    from epostak.resources.webhooks import WebhooksResource

    rl = client._rate_limit_store
    client.inbound = InboundResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    client.outbound = OutboundResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    client.webhooks = WebhooksResource(http, "https://epostak.sk/api/v1", tm, None, _rate_limit_store=rl)
    return client


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
