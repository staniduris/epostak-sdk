"""Auth resource -- OAuth ``client_credentials`` flow + key management.

Provides :class:`AuthResource` for minting, renewing, and revoking JWT
access/refresh token pairs, introspecting the calling API key, rotating its
secret, and managing the per-key IP allowlist.

Available as ``client.auth`` on the :class:`~epostak.client.EPostak` client.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional, TYPE_CHECKING

import httpx

from epostak.resources.documents import _BaseResource

if TYPE_CHECKING:
    from epostak.types import (
        AuthStatusResponse,
        IpAllowlistResponse,
        RevokeResponse,
        RotateSecretResponse,
        TokenResponse,
    )


class IpAllowlistResource(_BaseResource):
    """Sub-resource for the per-key IP allowlist (Wave 3.1).

    An empty list means **no** IP restriction — any caller IP is accepted.
    When the list is non-empty, requests authenticated with this key are
    rejected (HTTP 403) unless the source IP matches at least one entry.
    Each entry is either a bare IPv4/IPv6 address or a CIDR block
    (``addr/prefix``). Maximum 50 entries.
    """

    def get(self) -> IpAllowlistResponse:
        """Read the current IP allowlist for the calling API key.

        Example::

            res = client.auth.ip_allowlist.get()
            print(res["ip_allowlist"])  # list of CIDR strings
        """
        return self._request("GET", "/auth/ip-allowlist")

    def update(self, cidrs: List[str]) -> IpAllowlistResponse:
        """Replace the IP allowlist for the calling API key.

        Args:
            cidrs: New allowlist. Each entry must be either a bare IP or
                a valid CIDR. Pass ``[]`` to clear the restriction.

        Returns:
            Dict with the updated ``ip_allowlist`` list.

        Example::

            client.auth.ip_allowlist.update([
                "192.168.1.0/24",
                "203.0.113.42",
            ])
        """
        return self._request(
            "PUT",
            "/auth/ip-allowlist",
            json={"ip_allowlist": list(cidrs)},
        )


class AuthResource(_BaseResource):
    """OAuth ``client_credentials`` flow + key management.

    The token mint endpoint accepts the API key as the ``client_secret`` of
    an OAuth ``client_credentials`` exchange. ePošťák returns a short-lived
    JWT access token (``expires_in: 900`` seconds) and a 30-day rotating
    refresh token. Use :meth:`token` once at startup, cache the access
    token, and call :meth:`renew` before it expires.

    Example::

        client = EPostak(api_key="sk_live_xxxxx")
        tokens = client.auth.token(api_key="sk_live_xxxxx")
        print(tokens["access_token"], tokens["expires_in"])

        # Later, before expiry:
        renewed = client.auth.renew(refresh_token=tokens["refresh_token"])

        # On logout / key rotation:
        client.auth.revoke(token=tokens["refresh_token"], token_type_hint="refresh_token")
    """

    ip_allowlist: IpAllowlistResource
    """Sub-resource for the per-key IP allowlist."""

    def __init__(
        self,
        client: httpx.Client,
        base_url: str,
        api_key: str,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
    ) -> None:
        super().__init__(client, base_url, api_key, firm_id, max_retries=max_retries)
        self.ip_allowlist = IpAllowlistResource(
            client, base_url, api_key, firm_id, max_retries=max_retries
        )

    def token(
        self,
        api_key: str,
        firm_id: Optional[str] = None,
        scope: Optional[str] = None,
    ) -> TokenResponse:
        """Mint an OAuth access token via the ``client_credentials`` grant.

        The API key is sent as both the ``Authorization: Bearer`` header and
        the ``client_secret`` body field — the server accepts either, but
        doubling up keeps the SDK compatible across spec revisions. For
        integrator keys (``sk_int_*``) you must also pass ``firm_id``, which
        is forwarded as ``X-Firm-Id`` so the issued JWT is bound to the
        right tenant.

        Args:
            api_key: The plaintext API key being exchanged.
            firm_id: Required for integrator keys (``sk_int_*``).
            scope: Optional space-separated scope subset (defaults to the
                key's own scopes).

        Returns:
            Dict with ``access_token``, ``refresh_token``, ``token_type``,
            ``expires_in`` (seconds), and ``scope``.

        Example::

            tokens = client.auth.token(api_key="sk_live_xxxxx")
            print(tokens["access_token"])
        """
        body: Dict[str, Any] = {
            "grant_type": "client_credentials",
            "client_id": api_key,
            "client_secret": api_key,
        }
        if scope:
            body["scope"] = scope

        # Build a one-off request so we use the supplied api_key (not self._api_key).
        from epostak.errors import EPostakError

        url = f"{self._base_url}/auth/token"
        headers: Dict[str, str] = {"Authorization": f"Bearer {api_key}"}
        if firm_id:
            headers["X-Firm-Id"] = firm_id

        response = self._client.request(
            "POST",
            url,
            headers=headers,
            json=body,
            timeout=30.0,
        )
        if not response.is_success:
            try:
                err_body = response.json()
            except Exception:
                err_body = {"error": response.reason_phrase or "API request failed"}
            raise EPostakError(response.status_code, err_body, response.headers)
        return response.json()

    def renew(self, refresh_token: str) -> TokenResponse:
        """Exchange a refresh token for a new access + refresh pair.

        The old refresh token is invalidated server-side, so always replace
        your stored refresh token with the value returned by this call.

        Args:
            refresh_token: The current refresh token.

        Returns:
            New ``TokenResponse`` (rotated refresh token).

        Example::

            renewed = client.auth.renew(refresh_token=stored)
            secrets.put("epostak_refresh", renewed["refresh_token"])
        """
        return self._request(
            "POST",
            "/auth/renew",
            json={
                "grant_type": "refresh_token",
                "refresh_token": refresh_token,
            },
        )

    def revoke(
        self,
        token: str,
        token_type_hint: Optional[str] = None,
    ) -> RevokeResponse:
        """Revoke an access or refresh token. Idempotent.

        Returns HTTP 200 even if the token is unknown or already revoked,
        so this is safe to call unconditionally on logout.

        Args:
            token: The token string to revoke.
            token_type_hint: ``"access_token"`` or ``"refresh_token"`` —
                pass when known to skip the auto-detect path.

        Example::

            client.auth.revoke(token=stored, token_type_hint="refresh_token")
        """
        body: Dict[str, Any] = {"token": token}
        if token_type_hint:
            body["token_type_hint"] = token_type_hint
        return self._request("POST", "/auth/revoke", json=body)

    def status(self) -> AuthStatusResponse:
        """Introspect the calling API key without revealing the plaintext.

        Returns the key metadata, the firm it is bound to, the current
        plan, and — for integrator keys — the integrator summary.

        Example::

            info = client.auth.status()
            print(info["key"]["prefix"], info["plan"]["name"])
        """
        return self._request("GET", "/auth/status")

    def rotate_secret(self) -> RotateSecretResponse:
        """Rotate the calling ``sk_live_*`` API key.

        The previous key is deactivated immediately and the new plaintext
        key is returned ONCE — store it in your secret manager before
        continuing. Integrator keys (``sk_int_*``) are rejected with HTTP
        403; rotate those through the integrator dashboard instead.

        Returns:
            Dict with ``key`` (new plaintext, shown ONCE), ``prefix``,
            and ``message``.

        Example::

            rotated = client.auth.rotate_secret()
            secrets.put("epostak_api_key", rotated["key"])
        """
        return self._request("POST", "/auth/rotate-secret")
