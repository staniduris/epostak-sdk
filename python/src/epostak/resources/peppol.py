"""Peppol resource -- SMP lookup, directory search, and company lookup.

Provides :class:`PeppolResource` for querying the Peppol SMP network and
:class:`PeppolDirectoryResource` (accessible via ``client.peppol.directory``)
for searching the Peppol Business Card directory.  Also includes a Slovak
company lookup by ICO.
"""

from __future__ import annotations

from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    from epostak.types import (
        CompanyLookup,
        DirectorySearchResult,
        PeppolParticipant,
    )

from epostak.resources.documents import _BaseResource, _build_query

import httpx


class PeppolDirectoryResource(_BaseResource):
    """Peppol Business Card directory search."""

    def search(
        self,
        q: Optional[str] = None,
        country: Optional[str] = None,
        page: Optional[int] = None,
        page_size: Optional[int] = None,
    ) -> DirectorySearchResult:
        """Search the Peppol Business Card directory.

        Args:
            q: Free-text search query string (company name, ID, etc.).
            country: ISO 3166-1 alpha-2 country code filter, e.g. ``"SK"``.
            page: Page number, 0-based (default 0).
            page_size: Results per page.

        Returns:
            Paginated result with ``results`` (list of directory entries), ``total``,
            ``page``, and ``page_size``.

        Example::

            results = client.peppol.directory.search(q="Telekom", country="SK")
            for entry in results["results"]:
                print(entry["peppolId"], entry["name"])
        """
        params = _build_query({"q": q, "country": country, "page": page, "page_size": page_size})
        return self._request("GET", "/peppol/directory/search", params=params)


class PeppolResource(_BaseResource):
    """SMP lookup, directory search, and Slovak company lookup."""

    directory: PeppolDirectoryResource
    """Access the Peppol Business Card directory."""

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
        self.directory = PeppolDirectoryResource(client, base_url, api_key, firm_id, max_retries=max_retries)

    def lookup(self, scheme: str, identifier: str) -> PeppolParticipant:
        """Look up a Peppol participant via SMP.

        Args:
            scheme: Peppol scheme, e.g. ``"0245"`` (SK DIČ) or ``"9950"`` (SK IČ DPH).
            identifier: Peppol identifier value, e.g. ``"12345678"``.

        Returns:
            Participant info with ``peppolId``, ``name``, ``country``, and ``capabilities``
            (list of supported document types).

        Example::

            participant = client.peppol.lookup("0245", "12345678")
            print(participant["name"])
            for cap in participant["capabilities"]:
                print(cap["documentTypeId"])
        """
        return self._request("GET", f"/peppol/participants/{quote(scheme, safe='')}/{quote(identifier, safe='')}")

    def company_lookup(self, ico: str) -> CompanyLookup:
        """Look up a Slovak company by ICO.

        Returns company info including name, DIC, IC DPH, address, and Peppol ID
        if the company is registered on Peppol.

        Args:
            ico: Slovak ICO (8-digit string).

        Returns:
            Dict with ``ico``, ``name``, ``dic``, ``icDph``, ``address``, and ``peppolId``.

        Example::

            company = client.peppol.company_lookup("12345678")
            print(company["name"], company.get("peppolId"))
        """
        return self._request("GET", f"/company/lookup/{quote(ico, safe='')}")
