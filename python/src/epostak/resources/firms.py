"""Firms resource -- manage client firms (integrator keys).

Provides :class:`FirmsResource` for listing, inspecting, and assigning firms
when using an integrator API key (``sk_int_*``).  Single-firm keys
(``sk_live_*``) only have access to :meth:`~FirmsResource.list` and
:meth:`~FirmsResource.get`.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    from epostak.types import (
        AssignFirmResponse,
        BatchAssignFirmsResponse,
        FirmDetail,
        FirmSummary,
        InboxListResponse,
        PeppolIdentifierResponse,
    )

from epostak.resources.documents import _BaseResource, _build_query


class FirmsResource(_BaseResource):
    """Manage client firms (integrator keys)."""

    def list(self) -> List[FirmSummary]:
        """List all accessible firms.

        Returns:
            List of firm summary dicts with ``id``, ``name``, ``ico``, ``peppolId``,
            and ``peppolStatus``.

        Example::

            firms = client.firms.list()
            for firm in firms:
                print(firm["name"], firm["peppolStatus"])
        """
        res = self._request("GET", "/firms")
        return res.get("firms", res) if isinstance(res, dict) else res

    def get(self, id: str) -> FirmDetail:
        """Get firm detail by ID.

        Args:
            id: Firm UUID.

        Returns:
            Full firm detail including address, Peppol identifiers, and tax IDs.

        Example::

            firm = client.firms.get("firm-uuid")
            print(firm["name"], firm["peppolIdentifiers"])
        """
        return self._request("GET", f"/firms/{quote(id, safe='')}")

    def documents(
        self,
        id: str,
        offset: int = 0,
        limit: int = 20,
        direction: Optional[str] = None,
    ) -> InboxListResponse:
        """List documents for a firm.

        Args:
            id: Firm UUID.
            offset: Pagination offset (default 0).
            limit: Page size (default 20).
            direction: ``"inbound"`` or ``"outbound"``.

        Returns:
            Paginated response with ``documents``, ``total``, ``limit``, and ``offset``.

        Example::

            docs = client.firms.documents("firm-uuid", limit=50, direction="inbound")
            print(docs["total"], "total documents")
        """
        params = _build_query({"offset": offset, "limit": limit, "direction": direction})
        return self._request("GET", f"/firms/{quote(id, safe='')}/documents", params=params)

    def register_peppol_id(self, id: str, scheme: str, identifier: str) -> PeppolIdentifierResponse:
        """Register a Peppol ID for a firm.

        Args:
            id: Firm UUID.
            scheme: Peppol scheme, e.g. ``"0245"`` (SK DIČ) or ``"9950"`` (SK IČ DPH).
            identifier: Peppol identifier value, e.g. ``"12345678"``.

        Returns:
            Dict with ``peppolId``, ``scheme``, ``identifier``, and ``registeredAt``.

        Example::

            result = client.firms.register_peppol_id("firm-uuid", "0245", "12345678")
            print(result["peppolId"])  # "0245:12345678"
        """
        return self._request(
            "POST",
            f"/firms/{quote(id, safe='')}/peppol-identifiers",
            json={"scheme": scheme, "identifier": identifier},
        )

    def assign(self, ico: str) -> AssignFirmResponse:
        """Assign an existing firm to this integrator by ICO.

        Args:
            ico: Slovak ICO (8 digits).

        Returns:
            Dict with ``firm`` (assigned firm details) and ``status`` (e.g. ``"active"``).

        Example::

            result = client.firms.assign("12345678")
            print(result["firm"]["id"], result["status"])
        """
        return self._request("POST", "/firms/assign", json={"ico": ico})

    def assign_batch(self, icos: List[str]) -> BatchAssignFirmsResponse:
        """Batch assign firms to this integrator (max 50).

        Args:
            icos: List of Slovak ICOs (8-digit strings).

        Returns:
            Dict with ``results`` -- one entry per ICO with ``status``, ``firm``, and
            optional ``error``/``message`` on failure.

        Example::

            result = client.firms.assign_batch(["12345678", "87654321"])
            for r in result["results"]:
                print(r["ico"], r["status"])
        """
        return self._request("POST", "/firms/assign/batch", json={"icos": icos})
