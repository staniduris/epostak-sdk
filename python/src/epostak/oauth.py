"""OAuth ``authorization_code`` + PKCE helpers.

Stateless helpers for the **integrator-initiated** OAuth ``authorization_code``
+ PKCE flow. Use these from your own backend when you want to onboard an
end-user firm into ePošťák from inside your own application — the user clicks
a "Connect ePošťák" button in your UI, lands on the ePošťák ``/oauth/authorize``
consent page, and ePošťák redirects back to your ``redirect_uri`` with a
``code``.

This is independent of the regular :meth:`epostak.resources.auth.AuthResource.token`
flow (which uses ``client_credentials``). Pick one or the other depending on
how the firm is linked to you.

The OAuth token endpoint lives at ``https://epostak.sk/api/oauth/token`` —
**not** under ``/api/v1`` — so this module bypasses the configured
``EPostak(base_url=...)``.

Example::

    from epostak import OAuth

    # 1. On every onboarding attempt, generate a fresh PKCE pair.
    pair = OAuth.generate_pkce()
    sessions[req.session_id] = pair["code_verifier"]

    # 2. Build the authorize URL and redirect the user.
    url = OAuth.build_authorize_url(
        client_id=os.environ["EPOSTAK_OAUTH_CLIENT_ID"],
        redirect_uri="https://your-app.com/oauth/epostak/callback",
        code_challenge=pair["code_challenge"],
        state=req.session_id,
        scope="firm:read firm:manage document:send",
    )
    return redirect(url)

    # 3. On callback, exchange the code for a token pair.
    tokens = OAuth.exchange_code(
        code=req.args["code"],
        code_verifier=sessions[req.args["state"]],
        client_id=os.environ["EPOSTAK_OAUTH_CLIENT_ID"],
        client_secret=os.environ["EPOSTAK_OAUTH_CLIENT_SECRET"],
        redirect_uri="https://your-app.com/oauth/epostak/callback",
    )
"""

from __future__ import annotations

import base64
import hashlib
import secrets
from typing import TYPE_CHECKING, Optional
from urllib.parse import urlencode

import httpx

from epostak.errors import EPostakError, build_api_error

if TYPE_CHECKING:
    from epostak.types import TokenResponse


class OAuth:
    """Stateless helpers for the OAuth ``authorization_code`` + PKCE flow.

    All methods are :func:`staticmethod`\\ s — there is no per-instance state.
    """

    DEFAULT_ORIGIN = "https://epostak.sk"
    """Default origin for ePošťák OAuth endpoints. Override for staging."""

    @staticmethod
    def generate_pkce() -> dict[str, str]:
        """Generate a fresh PKCE code-verifier + S256 code-challenge pair.

        The ``code_verifier`` is 43 base64url characters (≈256 bits of
        entropy). Store it server-side keyed by ``state`` — you must NOT
        round-trip it through the user's browser, that defeats PKCE.

        Returns:
            Dict with two keys: ``code_verifier`` (random) and
            ``code_challenge`` (``base64url(SHA256(code_verifier))``).
        """
        code_verifier = secrets.token_urlsafe(32)
        digest = hashlib.sha256(code_verifier.encode("ascii")).digest()
        code_challenge = base64.urlsafe_b64encode(digest).rstrip(b"=").decode("ascii")
        return {"code_verifier": code_verifier, "code_challenge": code_challenge}

    @staticmethod
    def build_authorize_url(
        client_id: str,
        redirect_uri: str,
        code_challenge: str,
        state: str,
        scope: Optional[str] = None,
        origin: Optional[str] = None,
    ) -> str:
        """Build a ``/oauth/authorize`` URL the integrator can redirect to.

        Always sets ``response_type=code`` and ``code_challenge_method=S256``.

        Args:
            client_id: The integrator's registered OAuth client id.
            redirect_uri: Exact-match registered redirect URI.
            code_challenge: From :meth:`generate_pkce`.
            state: CSRF/session token; echoed back on the callback.
            scope: Optional space-separated subset of registered scopes.
                Omit to receive the full registered scope list on the
                consent screen.
            origin: Override the host (defaults to :attr:`DEFAULT_ORIGIN`).

        Returns:
            Absolute URL string.
        """
        params: list[tuple[str, str]] = [
            ("client_id", client_id),
            ("redirect_uri", redirect_uri),
            ("response_type", "code"),
            ("code_challenge", code_challenge),
            ("code_challenge_method", "S256"),
            ("state", state),
        ]
        if scope:
            params.append(("scope", scope))
        base = (origin or OAuth.DEFAULT_ORIGIN).rstrip("/")
        return f"{base}/oauth/authorize?{urlencode(params)}"

    @staticmethod
    def exchange_code(
        code: str,
        code_verifier: str,
        client_id: str,
        client_secret: str,
        redirect_uri: str,
        origin: Optional[str] = None,
    ) -> TokenResponse:
        """Exchange an authorization ``code`` for a :data:`TokenResponse`.

        Hits ``${origin}/api/oauth/token`` directly — does not route through
        :class:`~epostak.client.EPostak` ``base_url`` since OAuth lives outside
        ``/api/v1``.

        The returned access token is a 15-minute JWT; the refresh token is
        30-day rotating. Persist both server-side keyed by your firm record.

        Args:
            code: The authorization code from the ``redirect_uri`` callback.
            code_verifier: The verifier paired with the ``code_challenge``
                used when starting the flow.
            client_id: Integrator OAuth client id.
            client_secret: Integrator OAuth client secret.
            redirect_uri: Must match the URI used in
                :meth:`build_authorize_url`.
            origin: Override the host.

        Raises:
            EPostakError: When the server returns a non-2xx response.
        """
        base = (origin or OAuth.DEFAULT_ORIGIN).rstrip("/")
        url = f"{base}/api/oauth/token"
        body = {
            "grant_type": "authorization_code",
            "code": code,
            "code_verifier": code_verifier,
            "client_id": client_id,
            "client_secret": client_secret,
            "redirect_uri": redirect_uri,
        }
        try:
            response = httpx.post(
                url,
                content=urlencode(body),
                headers={
                    "Content-Type": "application/x-www-form-urlencoded",
                    "Accept": "application/json",
                },
                timeout=30.0,
            )
        except httpx.HTTPError as exc:
            raise EPostakError(0, {"error": str(exc)}) from exc

        text = response.text or ""
        try:
            parsed = response.json() if text else {}
        except Exception:
            parsed = {"error": {"message": text}}

        if not response.is_success:
            raise build_api_error(response.status_code, parsed, response.headers)
        return parsed
