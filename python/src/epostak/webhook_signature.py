"""Webhook signature verification helper.

Top-level :func:`verify_webhook_signature` validates HMAC-SHA256 webhook
deliveries from ePošťák. Exported from the package root so it can be used
without a configured client::

    from epostak import verify_webhook_signature
"""

from __future__ import annotations

import hashlib
import hmac
import time
from dataclasses import dataclass
from typing import Optional, Union


@dataclass
class VerifyWebhookSignatureResult:
    """Result of :func:`verify_webhook_signature`.

    ``valid is True`` means the body, signature, and secret line up AND
    the timestamp is within tolerance. On ``False`` the ``reason`` explains
    why so callers can log it; the SDK never raises on bad signatures.

    Attributes:
        valid: Whether the signature is valid AND the timestamp is within
            tolerance.
        reason: Why the signature was rejected — only populated when
            ``valid is False``. One of:

            - ``"missing_header"``
            - ``"malformed_header"``
            - ``"no_v1_signature"``
            - ``"signature_mismatch"``
            - ``"timestamp_outside_tolerance"``
        timestamp: Parsed timestamp from the header, in seconds since the
            epoch. ``None`` when the header could not be parsed.
    """

    valid: bool
    reason: Optional[str] = None
    timestamp: Optional[int] = None


def verify_webhook_signature(
    payload: Union[bytes, str],
    signature_header: str,
    secret: str,
    tolerance_seconds: int = 300,
) -> VerifyWebhookSignatureResult:
    """Verify an ePošťák webhook payload using HMAC-SHA256 (timing-safe).

    Header format: ``t=<unix_seconds>,v1=<hex_signature>``. Multiple
    ``v1=`` signatures may appear (during secret rotation); any of them
    matching is sufficient.

    The signed string is ``f"{t}.{raw_body}"``, hex-encoded HMAC-SHA256,
    computed on the bytes exactly as received off the wire — do **not**
    re-serialize the parsed JSON, the round-trip will reorder keys and
    mutate whitespace.

    Args:
        payload: Raw request body (bytes preferred; ``str`` is also
            accepted and encoded as UTF-8).
        signature_header: Value of the ``X-Epostak-Signature`` header.
        secret: The webhook signing secret captured at webhook-creation
            time.
        tolerance_seconds: Maximum age of the signature in seconds.
            Defaults to 300 (5 minutes), matching the server's replay
            window. Pass ``0`` to disable the timestamp check (not
            recommended in production).

    Returns:
        A :class:`VerifyWebhookSignatureResult`.

    Example::

        from flask import Flask, request
        from epostak import verify_webhook_signature

        app = Flask(__name__)

        @app.post("/webhooks/epostak")
        def hook():
            result = verify_webhook_signature(
                payload=request.get_data(),
                signature_header=request.headers.get("X-Epostak-Signature", ""),
                secret=WEBHOOK_SECRET,
            )
            if not result.valid:
                return f"bad signature: {result.reason}", 400
            event = request.get_json()
            # process event...
            return "", 204
    """
    if not signature_header:
        return VerifyWebhookSignatureResult(valid=False, reason="missing_header")

    timestamp_str: Optional[str] = None
    v1_signatures: list[str] = []
    for raw in signature_header.split(","):
        part = raw.strip()
        eq = part.find("=")
        if eq < 0:
            continue
        k = part[:eq]
        v = part[eq + 1 :]
        if k == "t":
            timestamp_str = v
        elif k == "v1":
            v1_signatures.append(v)

    if timestamp_str is None:
        return VerifyWebhookSignatureResult(valid=False, reason="malformed_header")
    if not v1_signatures:
        return VerifyWebhookSignatureResult(valid=False, reason="no_v1_signature")

    try:
        ts = int(timestamp_str)
    except (ValueError, TypeError):
        return VerifyWebhookSignatureResult(valid=False, reason="malformed_header")

    if tolerance_seconds > 0:
        now = int(time.time())
        if abs(now - ts) > tolerance_seconds:
            return VerifyWebhookSignatureResult(
                valid=False,
                reason="timestamp_outside_tolerance",
                timestamp=ts,
            )

    payload_bytes = payload if isinstance(payload, (bytes, bytearray)) else payload.encode("utf-8")
    signed = f"{timestamp_str}.".encode("utf-8") + bytes(payload_bytes)
    expected = hmac.new(secret.encode("utf-8"), signed, hashlib.sha256).hexdigest()

    for candidate in v1_signatures:
        # Use compare_digest on the hex strings for timing-safe compare;
        # length mismatch is also handled by compare_digest.
        try:
            if hmac.compare_digest(candidate, expected):
                return VerifyWebhookSignatureResult(valid=True, timestamp=ts)
        except Exception:
            continue

    return VerifyWebhookSignatureResult(
        valid=False,
        reason="signature_mismatch",
        timestamp=ts,
    )
