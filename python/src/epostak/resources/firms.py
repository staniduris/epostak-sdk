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
            scheme: Peppol scheme. For Slovak participants use ``"0245"`` (DIČ) — per Slovak PASR the ``"9950"`` VAT-number form is not supported.
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
        """Link this integrator to a Firm that already completed FS SR signup.

        **Lookup-only** -- this endpoint cannot create new Firms. The target
        Firm must have completed FS SR PFS signup and granted consent to this
        integrator before the link succeeds.

        Args:
            ico: Slovak ICO (8 digits).

        Returns:
            Dict with ``firm`` (linked firm details) and ``status`` (e.g. ``"active"``).

        Raises:
            EPostakError: 404 with ``code="FIRM_NOT_REGISTERED"`` -- no Firm with
                that ICO exists yet. Direct the firm to complete FS SR PFS signup
                before retrying.
            EPostakError: 403 with ``code="CONSENT_REQUIRED"`` -- Firm exists
                but has not granted consent for this integrator.
            EPostakError: 409 with ``code="ALREADY_LINKED"`` -- the integrator
                already has an active link to this Firm.

        Example::

            try:
                result = client.firms.assign("12345678")
                print(result["firm"]["id"], result["status"])
            except EPostakError as err:
                if err.code == "FIRM_NOT_REGISTERED":
                    # ask the firm to complete FS SR PFS signup first
                    ...
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
