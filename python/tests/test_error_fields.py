"""Tests for the additive error fields (api_code, message_sk, fields, rule,
fix_hint, fix_hint_sk, docs) read from ``body.error``."""

from __future__ import annotations

from epostak.errors import EPostakError, build_api_error

BODY = {
    "error": {
        "code": "DOCUMENT_INVALID",
        "message": "unitPrice must be a number",
        "retryable": False,
        "nextAction": "Correct the indicated document field and retry.",
        "apiCode": "VALIDATION_ERROR",
        "message_sk": "Jednotková cena musí byť číslo",
        "fields": [
            {
                "path": "items[0].unitPrice",
                "message": "unitPrice must be a number",
                "message_sk": "Jednotková cena musí byť číslo",
            }
        ],
        "rule": "PEPPOL-EN16931-R003",
        "fix_hint": "Send unitPrice as a number.",
        "fix_hint_sk": "Pošlite unitPrice ako číslo.",
        "docs": "https://epostak.sk/api/docs",
        "requestId": "request-ubl-validation",
    }
}


def test_new_error_fields_are_read() -> None:
    err = build_api_error(422, BODY)
    assert isinstance(err, EPostakError)
    assert err.api_code == "VALIDATION_ERROR"
    assert err.message_sk == "Jednotková cena musí byť číslo"
    assert err.fields == BODY["error"]["fields"]
    assert err.rule == "PEPPOL-EN16931-R003"
    assert err.fix_hint == "Send unitPrice as a number."
    assert err.fix_hint_sk == "Pošlite unitPrice ako číslo."
    assert err.docs == "https://epostak.sk/api/docs"
    # Existing fields are unchanged.
    assert err.code == "DOCUMENT_INVALID"
    assert err.message == "unitPrice must be a number"
    assert err.next_action == "Correct the indicated document field and retry."
    assert err.retryable is False
    assert err.request_id == "request-ubl-validation"


def test_missing_or_wrong_typed_fields_are_none() -> None:
    plain = EPostakError(500, {"error": {"code": "X", "message": "boom"}})
    wrong = EPostakError(
        422,
        {"error": {"apiCode": 1, "message_sk": {}, "fields": "x", "rule": 2,
                   "fix_hint": [], "fix_hint_sk": True, "docs": 3}},
    )
    no_body = EPostakError(0)
    for err in (plain, wrong, no_body):
        assert err.api_code is None
        assert err.message_sk is None
        assert err.fields is None
        assert err.rule is None
        assert err.fix_hint is None
        assert err.fix_hint_sk is None
        assert err.docs is None
