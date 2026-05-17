"""Peppol resource -- SMP lookup, directory search, and company lookup.

Provides :class:`PeppolResource` for querying the Peppol SMP network and
:class:`PeppolDirectoryResource` (accessible via ``client.peppol.directory``)
for searching the Peppol Business Card directory.  Also includes a Slovak
company lookup by ICO.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    from epostak.types import (
        CompanyLookup,
        DirectorySearchResult,
        PeppolCapabilitiesResult,
        PeppolLookupBatchResponse,
        PeppolParticipant,
        PeppolParticipantRef,
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
        token_manager: object,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
    ) -> None:
        super().__init__(client, base_url, token_manager, firm_id, max_retries=max_retries)
        self.directory = PeppolDirectoryResource(client, base_url, token_manager, firm_id, max_retries=max_retries)

    def lookup(self, scheme: str, identifier: str) -> PeppolParticipant:
        """Look up a Peppol participant via SMP.

        Args:
            scheme: Peppol scheme. For Slovak participants use ``"0245"`` (DIČ) — per Slovak PASR the ``"9950"`` VAT-number form is not supported.
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

    def company_search(self, q: str, limit: Optional[int] = None) -> Dict[str, Any]:
        """Search Slovak companies by name."""
        params = _build_query({"q": q, "limit": limit})
        return self._request("GET", "/company/search", params=params)

    def capabilities(
        self,
        scheme: str,
        identifier: str,
        document_type: Optional[str] = None,
    ) -> PeppolCapabilitiesResult:
        """Check a participant's advertised Peppol capabilities.

        Verifies that a participant exists on SMP and (optionally) that it
        accepts a specific document type.  Prefer this over :meth:`lookup`
        when you only need a yes/no answer for a given doc type.

        Args:
            scheme: Peppol scheme (e.g. ``"0245"``).
            identifier: Identifier value.
            document_type: Optional UBL document type ID to check for acceptance.

        Returns:
            Dict with ``found`` (bool), ``accepts`` (bool),
            ``supportedDocumentTypes`` (list of IDs), and
            ``matchedDocumentType`` (the matched ID, if any).

        Example::

            caps = client.peppol.capabilities(
                scheme="0245",
                identifier="12345678",
                document_type="urn:peppol:pint:billing-1@aunz-1",
            )
            if caps["found"] and caps["accepts"]:
                print("Receiver supports that document type")
        """
        body: Dict[str, Any] = {"scheme": scheme, "identifier": identifier}
        if document_type is not None:
            body["documentType"] = document_type
        return self._request("POST", "/peppol/capabilities", json=body)

    def lookup_batch(
        self,
        participants: List[PeppolParticipantRef],
    ) -> PeppolLookupBatchResponse:
        """Look up many Peppol participants in a single request (max 100).

        Each result matches the order of the input list and indicates whether
        the participant was found on SMP.

        Args:
            participants: List of ``{"scheme": str, "identifier": str}`` dicts.

        Returns:
            Dict with ``total``, ``found``, ``notFound``, and ``results``
            (per-participant ``{scheme, identifier, found, participant?, error?}``).

        Example::

            batch = client.peppol.lookup_batch([
                {"scheme": "0245", "identifier": "12345678"},
                {"scheme": "0245", "identifier": "87654321"},
            ])
            for r in batch["results"]:
                print(r["identifier"], "->", r["found"])
        """
        return self._request(
            "POST",
            "/peppol/participants/batch",
            json={"participants": participants},
        )
