"""Main client for the ePostak Enterprise API.

This module exports :class:`EPostak`, the top-level entry point for all SDK
operations.  Instantiate it with an API key and access resource namespaces
(``documents``, ``firms``, ``peppol``, ``webhooks``, ``reporting``,
``extract``, ``account``) as attributes.
"""

from __future__ import annotations

from typing import Optional

import httpx

from epostak.resources.account import AccountResource
from epostak.resources.documents import DocumentsResource
from epostak.resources.extract import ExtractResource
from epostak.resources.firms import FirmsResource
from epostak.resources.peppol import PeppolResource
from epostak.resources.reporting import ReportingResource
from epostak.resources.webhooks import WebhooksResource

DEFAULT_BASE_URL = "https://epostak.sk/api/enterprise"


class EPostak:
    """ePostak Enterprise API client.

    Args:
        api_key: Your Enterprise API key. Use ``sk_live_*`` for direct access
            or ``sk_int_*`` for integrator (multi-tenant) access.
        base_url: Base URL for the API. Defaults to ``https://epostak.sk/api/enterprise``.
        firm_id: Firm UUID to act on behalf of. Required when using integrator
            keys (``sk_int_*``). Each API call will include ``X-Firm-Id`` header.

    Example::

        from epostak import EPostak

        client = EPostak(api_key="sk_live_xxxxx")
        result = client.documents.send({
            "receiverPeppolId": "0245:1234567890",
            "items": [{"description": "Consulting", "quantity": 10, "unitPrice": 50, "vatRate": 23}],
        })
    """

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

    def __init__(
        self,
        api_key: str,
        *,
        base_url: str = DEFAULT_BASE_URL,
        firm_id: Optional[str] = None,
    ) -> None:
        if not api_key or not isinstance(api_key, str):
            raise ValueError("EPostak: api_key is required")

        self._api_key = api_key
        self._base_url = base_url.rstrip("/")
        self._firm_id = firm_id
        self._client = httpx.Client()

        self.documents = DocumentsResource(self._client, self._base_url, self._api_key, self._firm_id)
        self.firms = FirmsResource(self._client, self._base_url, self._api_key, self._firm_id)
        self.peppol = PeppolResource(self._client, self._base_url, self._api_key, self._firm_id)
        self.webhooks = WebhooksResource(self._client, self._base_url, self._api_key, self._firm_id)
        self.reporting = ReportingResource(self._client, self._base_url, self._api_key, self._firm_id)
        self.extract = ExtractResource(self._client, self._base_url, self._api_key, self._firm_id)
        self.account = AccountResource(self._client, self._base_url, self._api_key, self._firm_id)

    def with_firm(self, firm_id: str) -> EPostak:
        """Create a new client instance scoped to a specific firm.

        Useful when an integrator key needs to switch between clients.

        Args:
            firm_id: The firm UUID to scope subsequent requests to.

        Returns:
            A new ``EPostak`` instance with the ``X-Firm-Id`` header set.

        Example::

            base = EPostak(api_key="sk_int_xxxxx")
            client_a = base.with_firm("firm-uuid-a")
            client_b = base.with_firm("firm-uuid-b")
            client_a.documents.send({...})
        """
        return EPostak(
            api_key=self._api_key,
            base_url=self._base_url,
            firm_id=firm_id,
        )

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
