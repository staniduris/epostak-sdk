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
            - ``"unsupported_algorithm"``
            - ``"malformed_header"``
            - ``"signature_mismatch"``
            - ``"timestamp_outside_tolerance"``
        timestamp: Parsed timestamp from the ``X-Webhook-Timestamp`` header,
            in seconds since the epoch. ``None`` when the timestamp could
            not be parsed.
    """

    valid: bool
    reason: Optional[str] = None
    timestamp: Optional[int] = None


def verify_webhook_signature(
    payload: Union[bytes, str],
    signature: str,
    timestamp: str,
    secret: str,
    tolerance_seconds: int = 300,
) -> VerifyWebhookSignatureResult:
    """Verify an ePošťák webhook payload using HMAC-SHA256 (timing-safe).

    The server sends two separate headers::

        X-Webhook-Signature: sha256=<hex>
        X-Webhook-Timestamp: <unix_seconds>

    The signed string is ``f"{timestamp}.{raw_body}"``, hex-encoded
    HMAC-SHA256, computed on the bytes exactly as received off the wire —
    do **not** re-serialize the parsed JSON, the round-trip will reorder
    keys and mutate whitespace.

    Args:
        payload: Raw request body (bytes preferred; ``str`` is also
            accepted and encoded as UTF-8).
        signature: Value of the ``X-Webhook-Signature`` header.
            Must be in ``sha256=<hex>`` format.
        timestamp: Value of the ``X-Webhook-Timestamp`` header (unix
            seconds as a string).
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
                signature=request.headers.get("X-Webhook-Signature", ""),
                timestamp=request.headers.get("X-Webhook-Timestamp", ""),
                secret=WEBHOOK_SECRET,
            )
            if not result.valid:
                return f"bad signature: {result.reason}", 400
            event = request.get_json()
            # process event...
            return "", 204
    """
    if not signature:
        return VerifyWebhookSignatureResult(valid=False, reason="missing_header")

    # Parse "sha256=<hex>" — reject any other algorithm.
    eq = signature.find("=")
    if eq < 0:
        return VerifyWebhookSignatureResult(valid=False, reason="malformed_header")
    algorithm = signature[:eq]
    hex_sig = signature[eq + 1:]

    if algorithm != "sha256":
        return VerifyWebhookSignatureResult(valid=False, reason="unsupported_algorithm")

    if not hex_sig:
        return VerifyWebhookSignatureResult(valid=False, reason="malformed_header")

    # Parse timestamp.
    if not timestamp:
        return VerifyWebhookSignatureResult(valid=False, reason="missing_header")
    try:
        ts = int(timestamp)
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
    signed = f"{timestamp}.".encode("utf-8") + bytes(payload_bytes)
    expected = hmac.new(secret.encode("utf-8"), msg=signed, digestmod=hashlib.sha256).hexdigest()

    try:
        if hmac.compare_digest(hex_sig, expected):
            return VerifyWebhookSignatureResult(valid=True, timestamp=ts)
    except Exception:
        pass

    return VerifyWebhookSignatureResult(
        valid=False,
        reason="signature_mismatch",
        timestamp=ts,
    )
