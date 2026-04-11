"""Error types for the ePostak SDK.

All API errors are raised as :class:`EPostakError`.  Network-level failures
(timeouts, DNS resolution, etc.) are also wrapped in ``EPostakError`` with
``status=0``.
"""

from __future__ import annotations

from typing import Any, Optional


class EPostakError(Exception):
    """Error raised when an API request fails.

    Attributes:
        status: HTTP status code (``0`` for network errors).
        code: Machine-readable error code from the API, e.g. ``"VALIDATION_FAILED"``.
            May be ``None`` if the API did not return a structured error.
        message: Human-readable error description.
        details: Additional error details (validation messages, etc.).
            May be ``None`` if not provided.

    Example::

        from epostak import EPostak, EPostakError

        try:
            client.documents.send({...})
        except EPostakError as err:
            print(err.status, err.code, err.message)
            if err.details:
                print(err.details)
    """

    status: int
    code: Optional[str]
    message: str
    details: Any

    def __init__(
        self,
        status: int,
        body: Optional[dict[str, Any]] = None,
    ) -> None:
        body = body or {}
        error_obj = body.get("error")

        if isinstance(error_obj, str):
            msg = error_obj
            self.code = None
            self.details = None
        elif isinstance(error_obj, dict):
            msg = str(error_obj.get("message", "API request failed"))
            self.code = str(error_obj["code"]) if "code" in error_obj else None
            self.details = error_obj.get("details")
        else:
            msg = "API request failed"
            self.code = None
            self.details = None

        super().__init__(msg)
        self.status = status
        self.message = msg

    def __repr__(self) -> str:
        parts = [f"status={self.status}"]
        if self.code:
            parts.append(f"code={self.code!r}")
        parts.append(f"message={self.message!r}")
        return f"EPostakError({', '.join(parts)})"
