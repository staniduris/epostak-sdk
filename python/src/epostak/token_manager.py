"""Thread-safe OAuth token manager for the ePostak SDK.

Automatically mints a JWT via ``POST /sapi/v1/auth/token`` on first use,
caches it, and refreshes 60 seconds before expiry via ``/sapi/v1/auth/renew``.
"""

from __future__ import annotations

import re
import threading
import time
from typing import Optional

import httpx

_SAPI_TOKEN_PATH = "/sapi/v1/auth/token"
_SAPI_RENEW_PATH = "/sapi/v1/auth/renew"
_REFRESH_BUFFER_S = 60


class TokenManager:
    """Manages OAuth ``client_credentials`` JWT lifecycle."""

    def __init__(
        self,
        client_id: str,
        client_secret: str,
        base_url: str,
        firm_id: Optional[str] = None,
    ) -> None:
        self._client_id = client_id
        self._client_secret = client_secret
        self._base_url = base_url
        # firm_id accepted for backward compatibility but not used for token minting

        self._access_token: Optional[str] = None
        self._refresh_token: Optional[str] = None
        self._expires_at: float = 0.0
        self._lock = threading.Lock()

    def _sapi_base_url(self) -> str:
        return re.sub(r"/api/v1/?$", "", self._base_url)

    def get_access_token(self) -> str:
        """Return a valid JWT, minting or refreshing as needed. Thread-safe."""
        if self._access_token and time.time() < self._expires_at - _REFRESH_BUFFER_S:
            return self._access_token

        with self._lock:
            # Double-check after acquiring lock
            if self._access_token and time.time() < self._expires_at - _REFRESH_BUFFER_S:
                return self._access_token
            self._refresh_or_mint()
            return self._access_token  # type: ignore[return-value]

    def _refresh_or_mint(self) -> None:
        if self._refresh_token and self._access_token:
            try:
                self._do_renew()
                return
            except Exception:
                pass  # Fall through to full mint
        self._do_mint()

    def _do_mint(self) -> None:
        url = f"{self._sapi_base_url()}{_SAPI_TOKEN_PATH}"
        headers: dict[str, str] = {"Content-Type": "application/json"}

        body: dict[str, str] = {
            "grant_type": "client_credentials",
            "client_id": self._client_id,
            "client_secret": self._client_secret,
        }

        response = httpx.post(url, json=body, headers=headers, timeout=30.0)
        if not response.is_success:
            text = response.text or response.reason_phrase or "token mint failed"
            raise RuntimeError(f"Token mint failed ({response.status_code}): {text}")

        self._apply(response.json())

    def _do_renew(self) -> None:
        url = f"{self._sapi_base_url()}{_SAPI_RENEW_PATH}"
        response = httpx.post(
            url,
            json={
                "grant_type": "refresh_token",
                "refresh_token": self._refresh_token,
            },
            headers={
                "Content-Type": "application/json",
                "Authorization": f"Bearer {self._access_token}",
            },
            timeout=30.0,
        )
        if not response.is_success:
            raise RuntimeError(f"Token renew failed ({response.status_code})")

        self._apply(response.json())

    def _apply(self, data: dict) -> None:
        self._access_token = data["access_token"]
        self._refresh_token = data.get("refresh_token")
        self._expires_at = time.time() + data["expires_in"]
