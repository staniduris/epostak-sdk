"""Error types for the ePostak SDK.

All API errors are raised as :class:`EPostakError`.  Network-level failures
(timeouts, DNS resolution, etc.) are also wrapped in ``EPostakError`` with
``status=0``.
"""

from __future__ import annotations

import re
from typing import Any, Mapping, Optional


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
        return f"EPostakError({', '.join(parts)})"
