"""Main client for the ePostak API.

This module exports :class:`EPostak`, the top-level entry point for all SDK
operations.  Instantiate it with an API key and access resource namespaces
(``auth``, ``audit``, ``documents``, ``firms``, ``peppol``, ``webhooks``,
``reporting``, ``extract``, ``account``) as attributes.
"""

from __future__ import annotations

from typing import Optional

import httpx

from epostak.resources.account import AccountResource
from epostak.resources.audit import AuditResource
from epostak.resources.auth import AuthResource
from epostak.resources.documents import DocumentsResource
from epostak.resources.extract import ExtractResource
from epostak.resources.firms import FirmsResource
from epostak.resources.integrator import IntegratorResource
from epostak.resources.peppol import PeppolResource
from epostak.resources.reporting import ReportingResource
from epostak.resources.webhooks import WebhooksResource
from epostak.token_manager import TokenManager

DEFAULT_BASE_URL = "https://epostak.sk/api/v1"
DEFAULT_PUBLIC_BASE_URL = "https://epostak.sk/api"


def _derive_public_base_url(base_url: str) -> str:
    """Derive the public (non-versioned) API base URL from a versioned one."""
    stripped = base_url.rstrip("/")
    for suffix in ("/v1", "/enterprise"):
        if stripped.endswith(suffix):
            return stripped[: -len(suffix)]
    return stripped


def validate(xml: str, base_url: Optional[str] = None) -> "dict":
    """Validate a UBL XML document against the Peppol BIS 3.0 3-layer rules.

    This endpoint is **public** -- no API key is required.  Rate-limited to
    20 requests per minute per IP. Max 10 MB per XML payload.

    Args:
        xml: UBL 2.1 XML invoice or credit note as a string.
        base_url: Optional override for the public API base URL.  Useful when
            pointing the SDK at a staging or self-hosted deployment.  The
            default is ``https://epostak.sk/api``.

    Returns:
        Full 3-layer Peppol BIS 3.0 validation report as returned by the API.

    Example::

        from epostak import validate

        with open("invoice.xml", "r", encoding="utf-8") as f:
            report = validate(f.read())
        print(report["valid"], len(report.get("errors", [])))
    """
    url = f"{(base_url or DEFAULT_PUBLIC_BASE_URL).rstrip('/')}/validate"
    with httpx.Client() as client:
        response = client.post(
            url,
            content=xml.encode("utf-8") if isinstance(xml, str) else xml,
            headers={"Content-Type": "application/xml"},
            timeout=30.0,
        )
    if not response.is_success:
        from epostak.errors import build_api_error

        try:
            body = response.json()
        except Exception:
            body = {"error": response.reason_phrase or "validate request failed"}
        raise build_api_error(response.status_code, body, response.headers)
    return response.json()


class EPostak:
    """ePošťák API client.

    Authenticates via OAuth ``client_credentials`` -- the SDK auto-mints a
    JWT on first request and refreshes it transparently.

    Args:
        client_id: Your API key id (``sk_live_*`` or ``sk_int_*``).
        client_secret: Your API key secret.
        base_url: Base URL for the API. Defaults to
            ``https://epostak.sk/api/v1``.
        firm_id: Firm UUID to act on behalf of. Required when using
            integrator keys (``sk_int_*``). Each API call will include
            an ``X-Firm-Id`` header.
        max_retries: Maximum number of retry attempts for failed requests
            (default 3). Retries use exponential backoff with jitter and
            apply to GET/DELETE requests that receive HTTP 429 or 5xx
            responses. Set to 0 to disable retries.

    Example::

        from epostak import EPostak

        client = EPostak(
            client_id="sk_live_xxxxx",
            client_secret="sk_live_xxxxx",
        )
        result = client.documents.send({
            "receiverPeppolId": "0245:1234567890",
            "items": [{"description": "Consulting", "quantity": 10, "unitPrice": 50, "vatRate": 23}],
        })
    """

    auth: AuthResource
    """OAuth token mint/renew/revoke + key introspection, rotation, IP allowlist."""

    audit: AuditResource
    """Per-firm audit feed (cursor-paginated)."""

    documents: DocumentsResource
    """Send and receive documents via Peppol."""

    firms: FirmsResource
    """Manage client firms (integrator keys)."""

    peppol: PeppolResource
    """SMP lookup and Peppol directory search."""

    webhooks: WebhooksResource
    """Manage webhook subscriptions and pull queue."""

    reporting: ReportingResource
    """Document statistics and reports."""

    extract: ExtractResource
    """AI-powered OCR extraction from PDFs and images."""

    account: AccountResource
    """Account and firm information."""

    integrator: IntegratorResource
    """Integrator-aggregate endpoints (sk_int_* keys)."""

    def __init__(
        self,
        client_id: str,
        client_secret: str,
        *,
        base_url: str = DEFAULT_BASE_URL,
        firm_id: Optional[str] = None,
        max_retries: int = 3,
        _token_manager: Optional[TokenManager] = None,
    ) -> None:
        if not client_id or not isinstance(client_id, str):
            raise ValueError("EPostak: client_id is required")
        if not client_secret or not isinstance(client_secret, str):
            raise ValueError("EPostak: client_secret is required")

        self._client_id = client_id
        self._client_secret = client_secret
        self._base_url = base_url.rstrip("/")
        self._firm_id = firm_id
        self._max_retries = max_retries
        self._client = httpx.Client()

        self._token_manager = _token_manager or TokenManager(
            client_id=client_id,
            client_secret=client_secret,
            base_url=self._base_url,
            firm_id=firm_id,
        )

        retry_kw = {"max_retries": max_retries}
        tm = self._token_manager
        self.auth = AuthResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.audit = AuditResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.documents = DocumentsResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.firms = FirmsResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.peppol = PeppolResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.webhooks = WebhooksResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.reporting = ReportingResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.extract = ExtractResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.account = AccountResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)
        self.integrator = IntegratorResource(self._client, self._base_url, tm, self._firm_id, **retry_kw)

    def with_firm(self, firm_id: str) -> EPostak:
        """Create a new client instance scoped to a specific firm.

        Shares the same :class:`~epostak.token_manager.TokenManager` so the
        JWT is reused across firm-scoped clients.

        Args:
            firm_id: The firm UUID to scope subsequent requests to.

        Returns:
            A new ``EPostak`` instance with the ``X-Firm-Id`` header set.

        Example::

            base = EPostak(client_id="sk_int_xxxxx", client_secret="sk_int_xxxxx")
            client_a = base.with_firm("firm-uuid-a")
            client_b = base.with_firm("firm-uuid-b")
            client_a.documents.send({...})
        """
        return EPostak(
            client_id=self._client_id,
            client_secret=self._client_secret,
            base_url=self._base_url,
            firm_id=firm_id,
            max_retries=self._max_retries,
            _token_manager=self._token_manager,
        )

    def validate(self, xml: str) -> "dict":
        """Validate a UBL XML document via the public ``/api/validate`` endpoint.

        This is the PUBLIC validation endpoint and does **not** require an
        API key; the SDK intentionally bypasses the ``Authorization`` header
        for this call.  Rate-limited to 20 requests per minute per IP.

        Args:
            xml: UBL 2.1 XML invoice or credit note as a string.

        Returns:
            Full 3-layer Peppol BIS 3.0 validation report.

        Example::

            client = EPostak(api_key="sk_live_xxx")
            report = client.validate(open("invoice.xml").read())
            if not report["valid"]:
                for err in report["errors"]:
                    print(err)
        """
        return validate(xml, base_url=_derive_public_base_url(self._base_url))

    def close(self) -> None:
        """Close the underlying HTTP client and release resources.

        Called automatically when using the client as a context manager.
        """
        self._client.close()

    def __enter__(self) -> EPostak:
        """Enter context manager -- returns ``self``."""
        return self

    def __exit__(self, *args: object) -> None:
        """Exit context manager -- calls :meth:`close`."""
        self.close()
