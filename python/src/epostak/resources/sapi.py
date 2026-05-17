"""SAPI-SK 1.0 interoperable document resource."""

from __future__ import annotations

import re
from typing import Any, Dict, Optional
from urllib.parse import quote, urlencode

from epostak.resources.documents import _BaseResource, _build_query


class SapiResource(_BaseResource):
    """SAPI-SK 1.0 document send/receive endpoints."""

    @property
    def _sapi_base_url(self) -> str:
        return re.sub(r"/api/v1/?$", "", self._base_url)

    def _sapi_request(
        self,
        method: str,
        path: str,
        *,
        participant_id: str,
        json: Any = None,
        extra_headers: Optional[Dict[str, str]] = None,
    ) -> Any:
        original = self._base_url
        headers = {"X-Peppol-Participant-Id": participant_id}
        if extra_headers:
            headers.update(extra_headers)
        try:
            self._base_url = self._sapi_base_url
            return self._request(method, path, json=json, extra_headers=headers)
        finally:
            self._base_url = original

    def send(
        self,
        body: Dict[str, Any],
        *,
        participant_id: str,
        idempotency_key: str,
    ) -> Dict[str, Any]:
        """Send one SAPI XML document."""
        return self._sapi_request(
            "POST",
            "/sapi/v1/document/send",
            participant_id=participant_id,
            json=body,
            extra_headers={"Idempotency-Key": idempotency_key},
        )

    def receive(
        self,
        *,
        participant_id: str,
        limit: Optional[int] = None,
        status: Optional[str] = None,
        page_token: Optional[str] = None,
    ) -> Dict[str, Any]:
        """List received SAPI documents for a participant."""
        query = _build_query({"limit": limit, "status": status, "pageToken": page_token})
        suffix = f"?{urlencode(query)}" if query else ""
        return self._sapi_request(
            "GET",
            f"/sapi/v1/document/receive{suffix}",
            participant_id=participant_id,
        )

    def get(self, document_id: str, *, participant_id: str) -> Dict[str, Any]:
        """Get a received SAPI document with its XML payload."""
        return self._sapi_request(
            "GET",
            f"/sapi/v1/document/receive/{quote(document_id, safe='')}",
            participant_id=participant_id,
        )

    def acknowledge(self, document_id: str, *, participant_id: str) -> Dict[str, Any]:
        """Mark a received SAPI document as processed."""
        return self._sapi_request(
            "POST",
            f"/sapi/v1/document/receive/{quote(document_id, safe='')}/acknowledge",
            participant_id=participant_id,
        )
