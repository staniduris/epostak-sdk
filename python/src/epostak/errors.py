"""Error types for the ePostak SDK.

All API errors are raised as :class:`EPostakError`. Network-level failures
(timeouts, DNS resolution, etc.) are also wrapped in ``EPostakError`` with
``status=0``.

Specialised subclasses (currently :class:`DuplicateInvoiceNumberError`)
are constructed automatically by :func:`build_api_error` based on the API
``error.code`` field, so callers can ``except`` on the specific subclass
without inspecting the code string themselves.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any, List, Mapping, Optional


_INSUFFICIENT_SCOPE_RE = re.compile(r'error\s*=\s*"?insufficient_scope', re.IGNORECASE)
_SCOPE_VALUE_RE = re.compile(r'scope\s*=\s*"([^"]+)"', re.IGNORECASE)


def _parse_required_scope(headers: Optional[Mapping[str, str]]) -> Optional[str]:
    """Extract ``scope="..."`` from a ``WWW-Authenticate: Bearer
    error="insufficient_scope" scope="..."`` header. Returns ``None`` when
    the header is absent or the rejection was for a different reason
    (e.g. ``error="invalid_token"``).
    """
    if not headers:
        return None
    raw: Optional[str] = None
    # httpx Headers are case-insensitive; plain dicts may not be.
    try:
        raw = headers.get("www-authenticate")  # type: ignore[union-attr]
    except Exception:
        raw = None
    if raw is None:
        # Try case variants
        for key in ("WWW-Authenticate", "Www-Authenticate"):
            try:
                raw = headers.get(key)  # type: ignore[union-attr]
            except Exception:
                raw = None
            if raw is not None:
                break
    if not raw:
        return None
    if not _INSUFFICIENT_SCOPE_RE.search(raw):
        return None
    m = _SCOPE_VALUE_RE.search(raw)
    return m.group(1) if m else None


class EPostakError(Exception):
    """Error raised when an API request fails.

    The SDK normalises both the legacy ``{"error": {"code", "message",
    "details"}}`` envelope and the RFC 7807 ``application/problem+json``
    envelope into the same shape.

    Attributes:
        status: HTTP status code (``0`` for network errors).
        code: Machine-readable error code from the API, e.g.
            ``"VALIDATION_FAILED"``, ``"idempotency_conflict"``,
            ``"insufficient_scope"``. May be ``None``.
        message: Human-readable error description.
        details: Additional error details (validation messages, etc.).
        request_id: Server-assigned request identifier — populated whenever
            the server returns ``X-Request-Id`` (or echoes ``requestId`` in
            the body).
        type: RFC 7807 ``type`` — URI reference identifying the problem type.
        title: RFC 7807 ``title`` — short, human-readable summary.
        detail: RFC 7807 ``detail`` — explanation of this specific occurrence.
        instance: RFC 7807 ``instance`` — URI reference to this occurrence.
        required_scope: OAuth scope required when the server rejects with
            ``403 insufficient_scope``. Parsed from
            ``WWW-Authenticate: Bearer error="insufficient_scope" scope="..."``.
            ``None`` when the header is absent.

    Example::

        from epostak import EPostak, EPostakError

        try:
            client.documents.send({...})
        except EPostakError as err:
            print(err.status, err.code, err.message)
            if err.code == "VALIDATION_FAILED":
                print(err.details)
            if err.required_scope:
                print(f"Mint a token with scope: {err.required_scope}")
    """

    status: int
    code: Optional[str]
    message: str
    details: Any
    request_id: Optional[str]
    type: Optional[str]
    title: Optional[str]
    detail: Optional[str]
    instance: Optional[str]
    required_scope: Optional[str]

    def __init__(
        self,
        status: int,
        body: Optional[dict[str, Any]] = None,
        headers: Optional[Mapping[str, str]] = None,
    ) -> None:
        body = body or {}

        # Detect RFC 7807 envelope: { type, title, status, detail, instance, ... }
        is_problem = (
            isinstance(body, dict)
            and ("title" in body or "detail" in body)
            and "error" not in body
        )

        msg = "API request failed"
        code: Optional[str] = None
        details: Any = None

        if is_problem:
            title = body.get("title")
            detail = body.get("detail")
            if isinstance(title, str) and title:
                msg = title
            elif isinstance(detail, str) and detail:
                msg = detail
            body_code = body.get("code")
            if isinstance(body_code, str):
                code = body_code
            if "errors" in body:
                details = body.get("errors")
        else:
            error_obj = body.get("error") if isinstance(body, dict) else None
            if isinstance(error_obj, str):
                msg = error_obj
            elif isinstance(error_obj, dict):
                err_msg = error_obj.get("message")
                msg = str(err_msg) if err_msg is not None else msg
                if "code" in error_obj:
                    code = str(error_obj["code"])
                details = error_obj.get("details")
            else:
                body_msg = body.get("message") if isinstance(body, dict) else None
                if isinstance(body_msg, str):
                    msg = body_msg

        super().__init__(msg)
        self.name = "EPostakError"
        self.status = status
        self.code = code
        self.details = details
        self.message = msg

        # RFC 7807 fields — copied through verbatim when present.
        self.type = body.get("type") if isinstance(body.get("type"), str) else None
        self.title = body.get("title") if isinstance(body.get("title"), str) else None
        self.detail = body.get("detail") if isinstance(body.get("detail"), str) else None
        self.instance = (
            body.get("instance") if isinstance(body.get("instance"), str) else None
        )

        # Server-assigned request ID — body wins, then header.
        request_id: Optional[str] = None
        body_request_id = body.get("requestId") if isinstance(body, dict) else None
        if isinstance(body_request_id, str):
            request_id = body_request_id
        else:
            err = body.get("error") if isinstance(body, dict) else None
            if isinstance(err, dict):
                nested = err.get("requestId")
                if isinstance(nested, str):
                    request_id = nested
        if not request_id and headers is not None:
            try:
                hdr = headers.get("x-request-id") or headers.get("X-Request-Id")  # type: ignore[union-attr]
            except Exception:
                hdr = None
            if isinstance(hdr, str) and hdr:
                request_id = hdr
        self.request_id = request_id

        # Parse WWW-Authenticate for OAuth `insufficient_scope` rejections.
        scope = _parse_required_scope(headers)
        if not scope and isinstance(body, dict):
            body_scope = body.get("required_scope")
            if not isinstance(body_scope, str):
                err = body.get("error")
                if isinstance(err, dict):
                    body_scope = err.get("required_scope")
            if isinstance(body_scope, str) and body_scope:
                scope = body_scope
        self.required_scope = scope

    def __repr__(self) -> str:
        parts = [f"status={self.status}"]
        if self.code:
            parts.append(f"code={self.code!r}")
        parts.append(f"message={self.message!r}")
        return f"{type(self).__name__}({', '.join(parts)})"


@dataclass(frozen=True)
class DuplicateInvoiceRecipient:
    """Identification of the recipient on the existing duplicate invoice."""

    peppol_id: Optional[str]
    ico: Optional[str]
    name: Optional[str]


@dataclass(frozen=True)
class DuplicateInvoiceExistingDocument:
    """The pre-existing outbound invoice that triggered the conflict.

    ``sent_at`` is an ISO 8601 string — ``peppolSentAt`` if the original
    document was already delivered via Peppol, otherwise ``createdAt``.
    """

    id: str
    invoice_number: str
    status: str
    sent_at: str
    recipient: Optional[DuplicateInvoiceRecipient]


class DuplicateInvoiceNumberError(EPostakError):
    """Raised when ``POST /api/v1/documents/send`` (or the dashboard
    create endpoint) rejects an outbound invoice whose ``invoice_number``
    already exists for the firm.

    The conflict key is ``("firmId", "invoiceNumber")`` — recipient is
    intentionally NOT part of it; outbound numbering belongs to the
    sender.

    Example::

        from epostak import EPostak, DuplicateInvoiceNumberError

        try:
            client.documents.send({"invoiceNumber": "2026001", ...})
        except DuplicateInvoiceNumberError as err:
            existing = err.existing_document
            if existing is not None:
                print(f"already sent on {existing.sent_at}, doc id {existing.id}")
    """

    conflict_key: List[str]
    existing_document: Optional[DuplicateInvoiceExistingDocument]

    def __init__(
        self,
        status: int,
        body: Optional[dict[str, Any]] = None,
        headers: Optional[Mapping[str, str]] = None,
    ) -> None:
        super().__init__(status, body, headers)
        body = body or {}
        error_obj = body.get("error") if isinstance(body, dict) else None
        error_map = error_obj if isinstance(error_obj, dict) else {}

        ck = error_map.get("conflictKey")
        self.conflict_key = (
            [str(x) for x in ck]
            if isinstance(ck, list)
            else ["firmId", "invoiceNumber"]
        )

        ed = error_map.get("existingDocument")
        if isinstance(ed, dict):
            recipient_raw = ed.get("recipient")
            recipient: Optional[DuplicateInvoiceRecipient] = None
            if isinstance(recipient_raw, dict):
                recipient = DuplicateInvoiceRecipient(
                    peppol_id=_opt_str(recipient_raw.get("peppolId")),
                    ico=_opt_str(recipient_raw.get("ico")),
                    name=_opt_str(recipient_raw.get("name")),
                )
            self.existing_document = DuplicateInvoiceExistingDocument(
                id=str(ed.get("id", "")),
                invoice_number=str(ed.get("invoiceNumber", "")),
                status=str(ed.get("status", "")),
                sent_at=str(ed.get("sentAt", "")),
                recipient=recipient,
            )
        else:
            self.existing_document = None


def _opt_str(value: Any) -> Optional[str]:
    return None if value is None else str(value)


# ---------------------------------------------------------------------------
# UBL validation errors (v0.9.0)
# ---------------------------------------------------------------------------


UBL_RULES = (
    "BR-02",  # issueDate (BT-2) required
    "BR-05",  # Seller name (BT-27) required
    "BR-06",  # Buyer name (BT-44) required
    "BR-11",  # Seller VAT identifier required for VAT-rated invoices
    "BR-16",  # Invoice must have at least one line
    "BT-1",   # invoiceNumber required
    "PEPPOL-R008",  # EndpointID empty — firm needs DIČ/IČO/peppolId
)
"""The 7 known UBL validation rule codes that may appear in
``UblValidationError.rule`` when the server rejects with
``code == 'UBL_VALIDATION_ERROR'``. Mirrors the enum in the
OpenAPI ``UblValidationError`` schema and the source thrown
sites in ``lib/ubl/generate.ts``. New rules may be added in
future versions — clients should check the string, not assume
the tuple is exhaustive.
"""


class UblValidationError(EPostakError):
    """Raised when the server returns HTTP 422 with
    ``error.code == 'UBL_VALIDATION_ERROR'``.

    The ``rule`` attribute holds the failing Peppol BIS 3.0 rule ID (e.g.
    ``"BR-06"``).  One of the constants in :data:`UBL_RULES`.

    Attributes:
        rule: The Peppol rule ID that caused the rejection.
        request_id: Server-assigned request identifier (from base class).

    Example::

        from epostak import EPostak
        from epostak.errors import UblValidationError

        try:
            client.documents.send({"xml": bad_xml, ...})
        except UblValidationError as err:
            print(f"Peppol validation failed: {err.rule} — {err.message}")
    """

    rule: Optional[str]

    def __init__(
        self,
        status: int,
        body: Optional[dict[str, Any]] = None,
        headers: Optional[Mapping[str, str]] = None,
        *,
        rule: Optional[str] = None,
        request_id: Optional[str] = None,
    ) -> None:
        super().__init__(status, body, headers)
        # If rule was passed explicitly (e.g. from tests), use it;
        # otherwise try to extract from the body.
        if rule is not None:
            self.rule = rule
        else:
            body = body or {}
            error_obj = body.get("error") if isinstance(body, dict) else None
            rule_from_body: Optional[str] = None
            if isinstance(error_obj, dict):
                rule_from_body = _opt_str(error_obj.get("rule"))
            if rule_from_body is None and isinstance(body, dict):
                rule_from_body = _opt_str(body.get("rule"))
            self.rule = rule_from_body
        # Allow override of request_id from caller
        if request_id is not None:
            self.request_id = request_id


def build_api_error(
    status: int,
    body: Optional[dict[str, Any]] = None,
    headers: Optional[Mapping[str, str]] = None,
) -> EPostakError:
    """Build the right :class:`EPostakError` subclass from a parsed body.

    Falls back to :class:`EPostakError` when no specialised mapping applies.
    """
    body = body or {}
    error_obj = body.get("error") if isinstance(body, dict) else None
    code: Optional[str] = None
    if isinstance(error_obj, dict) and "code" in error_obj:
        code = str(error_obj["code"])
    if code == "DUPLICATE_INVOICE_NUMBER":
        return DuplicateInvoiceNumberError(status, body, headers)
    if status == 422 and code == "UBL_VALIDATION_ERROR":
        return UblValidationError(status, body, headers)
    return EPostakError(status, body, headers)
