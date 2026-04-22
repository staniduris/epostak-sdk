"""Documents resource -- send, receive, validate, and manage Peppol documents.

This module contains :class:`DocumentsResource` (the main entry point) and
:class:`InboxResource` (accessible via ``client.documents.inbox``).  It also
defines the shared :class:`_BaseResource` base class used by all other
resource modules.
"""

from __future__ import annotations

import random
import time
from typing import Any, Dict, List, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    import httpx

    from epostak.types import (
        AcknowledgeResponse,
        BatchSendResponse,
        ConvertResult,
        Document,
        DocumentEvidenceResponse,
        DocumentMarkResponse,
        DocumentMarkState,
        DocumentStatusResponse,
        InboxAllResponse,
        InboxDocumentDetailResponse,
        InboxListResponse,
        InvoiceRespondResponse,
        PreflightResult,
        SendDocumentResponse,
        ValidationResult,
    )

_RETRY_METHODS = frozenset({"GET", "DELETE"})
_RETRY_STATUS_CODES = frozenset({429, 500, 502, 503, 504})
_BASE_DELAY = 0.5
_MAX_DELAY = 30.0


def _build_query(params: Dict[str, Any]) -> Dict[str, str]:
    """Build query params dict, dropping None values."""
    return {k: str(v) for k, v in params.items() if v is not None}


class _BaseResource:
    """Shared base for all resource classes."""

    def __init__(
        self,
        client: httpx.Client,
        base_url: str,
        api_key: str,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
    ) -> None:
        self._client = client
        self._base_url = base_url
        self._api_key = api_key
        self._firm_id = firm_id
        self._max_retries = max_retries

    def _headers(self) -> Dict[str, str]:
        headers: Dict[str, str] = {"Authorization": f"Bearer {self._api_key}"}
        if self._firm_id:
            headers["X-Firm-Id"] = self._firm_id
        return headers

    def _should_retry(self, method: str, status_code: int) -> bool:
        return method.upper() in _RETRY_METHODS and status_code in _RETRY_STATUS_CODES

    def _sleep_for_retry(self, attempt: int, response: Any = None) -> None:
        # Respect Retry-After header on 429
        retry_after: Optional[float] = None
        if response is not None:
            raw = response.headers.get("retry-after")
            if raw is not None:
                try:
                    retry_after = float(raw)
                except (ValueError, TypeError):
                    pass

        if retry_after is not None:
            delay = min(retry_after, _MAX_DELAY)
        else:
            jitter = random.random()  # noqa: S311
            delay = min(_BASE_DELAY * (2 ** attempt) + jitter, _MAX_DELAY)

        time.sleep(delay)

    def _request(
        self,
        method: str,
        path: str,
        *,
        json: Any = None,
        data: Any = None,
        files: Any = None,
        params: Optional[Dict[str, str]] = None,
        raw: bool = False,
        content: Any = None,
        extra_headers: Optional[Dict[str, str]] = None,
    ) -> Any:
        from epostak.errors import EPostakError

        url = f"{self._base_url}{path}"
        last_exc: Optional[EPostakError] = None

        merged_headers = self._headers()
        if extra_headers:
            merged_headers.update(extra_headers)

        for attempt in range(self._max_retries + 1):
            response = None
            try:
                response = self._client.request(
                    method,
                    url,
                    headers=merged_headers,
                    json=json,
                    data=data,
                    files=files,
                    content=content,
                    params=params,
                    timeout=30.0,
                )
            except Exception as exc:
                last_exc = EPostakError(0, {"error": str(exc)})
                last_exc.__cause__ = exc
                # Network errors are retryable for idempotent methods
                if attempt < self._max_retries and method.upper() in _RETRY_METHODS:
                    self._sleep_for_retry(attempt)
                    continue
                raise last_exc from exc

            if not response.is_success:
                try:
                    body = response.json()
                except Exception:
                    body = {"error": response.reason_phrase or "API request failed"}
                last_exc = EPostakError(response.status_code, body)

                if attempt < self._max_retries and self._should_retry(method, response.status_code):
                    self._sleep_for_retry(attempt, response)
                    continue
                raise last_exc

            if raw:
                return response

            # 204 No Content
            if response.status_code == 204 or response.headers.get("content-length") == "0":
                return None

            return response.json()

        # Should not reach here, but guard against it
        raise last_exc or EPostakError(0, {"error": "Request failed after retries"})


class InboxResource(_BaseResource):
    """Sub-resource for received (inbox) documents."""

    def list(
        self,
        offset: int = 0,
        limit: int = 20,
        status: Optional[str] = None,
        since: Optional[str] = None,
    ) -> InboxListResponse:
        """List received documents.

        Args:
            offset: Pagination offset (default 0).
            limit: Page size, 1-100 (default 20).
            status: Filter by ``"RECEIVED"`` or ``"ACKNOWLEDGED"``.
            since: ISO 8601 timestamp -- only return documents created after this date.

        Returns:
            Paginated response with ``documents``, ``total``, ``limit``, and ``offset``.

        Example::

            inbox = client.documents.inbox.list(limit=10, status="RECEIVED")
            for doc in inbox["documents"]:
                print(doc["id"], doc["number"])
        """
        params = _build_query({"offset": offset, "limit": limit, "status": status, "since": since})
        return self._request("GET", "/documents/inbox", params=params)

    def get(self, id: str) -> InboxDocumentDetailResponse:
        """Get full detail of a received document, including UBL XML payload.

        Args:
            id: Document UUID.

        Returns:
            Dict with ``document`` (full document object) and ``payload`` (UBL XML string or None).

        Example::

            detail = client.documents.inbox.get("doc-uuid")
            print(detail["document"]["number"])
            print(detail["payload"])  # UBL XML
        """
        return self._request("GET", f"/documents/inbox/{quote(id, safe='')}")

    def acknowledge(self, id: str) -> AcknowledgeResponse:
        """Mark a received document as acknowledged/processed.

        Args:
            id: Document UUID.

        Returns:
            Dict with ``documentId``, ``status`` (``"ACKNOWLEDGED"``), and ``acknowledgedAt``.

        Example::

            result = client.documents.inbox.acknowledge("doc-uuid")
            print(result["acknowledgedAt"])
        """
        return self._request("POST", f"/documents/inbox/{quote(id, safe='')}/acknowledge")

    def list_all(
        self,
        offset: int = 0,
        limit: int = 50,
        status: Optional[str] = None,
        since: Optional[str] = None,
        firm_id: Optional[str] = None,
    ) -> InboxAllResponse:
        """List received documents across all firms (integrator only).

        Args:
            offset: Pagination offset (default 0).
            limit: Page size, 1-200 (default 50).
            status: Filter by ``"RECEIVED"`` or ``"ACKNOWLEDGED"``.
            since: ISO 8601 timestamp.
            firm_id: Optional firm UUID filter.

        Returns:
            Paginated response with ``documents`` (each including ``firm_id``/``firm_name``),
            ``total``, ``limit``, and ``offset``.

        Example::

            all_docs = client.documents.inbox.list_all(limit=100, status="RECEIVED")
            for doc in all_docs["documents"]:
                print(doc["firm_name"], doc["id"])
        """
        params = _build_query({
            "offset": offset,
            "limit": limit,
            "status": status,
            "since": since,
            "firm_id": firm_id,
        })
        return self._request("GET", "/documents/inbox/all", params=params)


class DocumentsResource(_BaseResource):
    """Send, receive, validate, and manage Peppol documents."""

    inbox: InboxResource
    """Access received (inbox) documents."""

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
        self.inbox = InboxResource(client, base_url, api_key, firm_id, max_retries=max_retries)

    def get(self, id: str) -> Document:
        """Get a document by ID.

        Args:
            id: Document UUID.

        Returns:
            Full document object with parties, line items, totals, and metadata.

        Example::

            doc = client.documents.get("doc-uuid")
            print(doc["number"], doc["status"], doc["totals"]["withVat"])
        """
        return self._request("GET", f"/documents/{quote(id, safe='')}")

    def update(self, id: str, **kwargs: Any) -> Document:
        """Update a draft document. Only documents with status ``draft`` can be updated.

        Pass any updatable fields as keyword arguments (e.g. ``invoiceNumber``,
        ``dueDate``, ``items``).  See :class:`~epostak.types.UpdateDocumentRequest`
        for the full list of updatable fields.

        Args:
            id: Document UUID.
            **kwargs: Fields to update.

        Returns:
            The updated document object.

        Example::

            updated = client.documents.update(
                "doc-uuid",
                invoiceNumber="FV-2026-002",
                dueDate="2026-05-01",
            )
        """
        return self._request("PATCH", f"/documents/{quote(id, safe='')}", json=kwargs)

    def send(self, body: Dict[str, Any]) -> SendDocumentResponse:
        """Send a document via Peppol.

        Supports two modes:

        - **JSON mode**: pass structured data with ``items`` list, UBL is auto-generated.
        - **XML mode**: pass ``xml`` with a pre-built UBL XML string.

        Both modes require ``receiverPeppolId``.

        Args:
            body: Request body.  See :class:`~epostak.types._SendDocumentBase` for available fields.

        Returns:
            Dict with ``documentId``, ``messageId``, and ``status``.

        Example::

            result = client.documents.send({
                "receiverPeppolId": "0245:1234567890",
                "invoiceNumber": "FV-2026-001",
                "issueDate": "2026-04-04",
                "dueDate": "2026-04-18",
                "items": [
                    {"description": "Consulting", "quantity": 10, "unitPrice": 50, "vatRate": 23},
                ],
            })
            print(result["documentId"])
        """
        return self._request("POST", "/documents/send", json=body)

    def status(self, id: str) -> DocumentStatusResponse:
        """Get full document status with lifecycle history.

        Args:
            id: Document UUID.

        Returns:
            Status response including ``statusHistory``, delivery timestamps, and
            ``invoiceResponseStatus`` if applicable.

        Example::

            status = client.documents.status("doc-uuid")
            for entry in status["statusHistory"]:
                print(entry["status"], entry["timestamp"])
        """
        return self._request("GET", f"/documents/{quote(id, safe='')}/status")

    def evidence(self, id: str) -> DocumentEvidenceResponse:
        """Get delivery evidence (AS4 receipt, MLR, invoice response).

        Args:
            id: Document UUID.

        Returns:
            Evidence response with ``as4Receipt``, ``mlrDocument``, ``invoiceResponse``,
            ``deliveredAt``, and ``sentAt`` (any may be None).

        Example::

            evidence = client.documents.evidence("doc-uuid")
            if evidence.get("deliveredAt"):
                print("Delivered at", evidence["deliveredAt"])
        """
        return self._request("GET", f"/documents/{quote(id, safe='')}/evidence")

    def pdf(self, id: str) -> bytes:
        """Download document as PDF.

        Args:
            id: Document UUID.

        Returns:
            Raw PDF file content as bytes.

        Example::

            pdf_bytes = client.documents.pdf("doc-uuid")
            with open("invoice.pdf", "wb") as f:
                f.write(pdf_bytes)
        """
        response = self._request("GET", f"/documents/{quote(id, safe='')}/pdf", raw=True)
        return response.content

    def ubl(self, id: str) -> str:
        """Download document as UBL XML string.

        Args:
            id: Document UUID.

        Returns:
            UBL XML document as a string.

        Example::

            xml = client.documents.ubl("doc-uuid")
            print(xml[:100])
        """
        response = self._request("GET", f"/documents/{quote(id, safe='')}/ubl", raw=True)
        return response.text

    def respond(self, id: str, status: str, note: Optional[str] = None) -> InvoiceRespondResponse:
        """Send an invoice response for a received document.

        Args:
            id: Document UUID.
            status: Response code -- ``"AP"`` (accepted), ``"RE"`` (rejected), or ``"UQ"`` (under query).
            note: Optional note to include with the response.

        Returns:
            Dict with ``documentId``, ``responseStatus``, and ``respondedAt``.

        Example::

            result = client.documents.respond("doc-uuid", status="AP", note="Accepted")
            print(result["respondedAt"])
        """
        body: Dict[str, Any] = {"status": status}
        if note is not None:
            body["note"] = note
        return self._request("POST", f"/documents/{quote(id, safe='')}/respond", json=body)

    def validate(self, body: Dict[str, Any]) -> ValidationResult:
        """Validate a document without sending. Same body format as ``send()``.

        Args:
            body: Document data -- same format as :meth:`send`.

        Returns:
            Validation result with ``valid`` (bool), ``warnings`` (list), and ``ubl``
            (generated UBL XML string, JSON mode only).

        Example::

            result = client.documents.validate({
                "receiverPeppolId": "0245:1234567890",
                "items": [{"description": "Test", "quantity": 1, "unitPrice": 100, "vatRate": 23}],
            })
            if result["valid"]:
                print("Document is valid")
        """
        return self._request("POST", "/documents/validate", json=body)

    def preflight(
        self,
        receiver_peppol_id: str,
        document_type_id: Optional[str] = None,
    ) -> PreflightResult:
        """Check if a receiver is registered on Peppol and supports a document type.

        Args:
            receiver_peppol_id: Peppol participant ID to check, e.g. ``"0245:1234567890"``.
            document_type_id: Optional UBL document type identifier to verify support for.

        Returns:
            Dict with ``registered``, ``supportsDocumentType``, and ``smpUrl``.

        Example::

            check = client.documents.preflight("0245:1234567890")
            if check["registered"]:
                print("Receiver is on Peppol")
        """
        body: Dict[str, Any] = {"receiverPeppolId": receiver_peppol_id}
        if document_type_id is not None:
            body["documentTypeId"] = document_type_id
        return self._request("POST", "/documents/preflight", json=body)

    def convert(
        self,
        input_format: str,
        output_format: str,
        document: Any,
    ) -> ConvertResult:
        """Convert a document between JSON and UBL XML.

        Args:
            input_format: Format of ``document``: ``"json"`` or ``"ubl"``.
            output_format: Desired output format: ``"ubl"`` or ``"json"``.
            document: The document payload -- a dict when ``input_format="json"``,
                an XML string when ``input_format="ubl"``.

        Returns:
            Dict with ``output_format``, ``document`` (UBL XML string or parsed
            JSON dict, matching ``output_format``), and optional ``warnings``.

        Example::

            # JSON -> UBL
            result = client.documents.convert(
                input_format="json",
                output_format="ubl",
                document={"invoiceNumber": "FV-001", "items": [...]},
            )
            print(result["document"])  # UBL XML string

            # UBL -> JSON
            result = client.documents.convert(
                input_format="ubl",
                output_format="json",
                document='<Invoice xmlns="urn:oasis:...">...</Invoice>',
            )
            print(result["document"])  # parsed JSON dict
        """
        body: Dict[str, Any] = {
            "input_format": input_format,
            "output_format": output_format,
            "document": document,
        }
        return self._request("POST", "/documents/convert", json=body)

    def send_batch(self, items: List[Dict[str, Any]]) -> BatchSendResponse:
        """Send up to 100 documents in a single request.

        Each item uses the same body format as :meth:`send` and may carry an
        optional ``idempotencyKey`` field to make retries safe.  Partial
        failures do not fail the whole request -- inspect ``results`` and
        ``failed`` for per-item outcomes.

        Args:
            items: List of send request bodies.

        Returns:
            Dict with ``total``, ``succeeded``, ``failed``, and ``results``
            (per-item ``{index, status, result}`` entries).

        Example::

            batch = client.documents.send_batch([
                {
                    "receiverPeppolId": "0245:1234567890",
                    "invoiceNumber": "FV-2026-010",
                    "items": [{"description": "Audit", "quantity": 1, "unitPrice": 500, "vatRate": 23}],
                    "idempotencyKey": "batch-2026-04-22-001",
                },
                {
                    "receiverPeppolId": "0245:0987654321",
                    "invoiceNumber": "FV-2026-011",
                    "items": [{"description": "Consulting", "quantity": 2, "unitPrice": 300, "vatRate": 23}],
                },
            ])
            print(batch["succeeded"], "/", batch["total"])
        """
        return self._request("POST", "/documents/send/batch", json={"items": items})

    def parse(self, xml: str) -> Dict[str, Any]:
        """Parse a UBL XML invoice into a structured JSON representation.

        Convenience wrapper around the ``/documents/parse`` endpoint that
        streams the XML as the raw request body with
        ``Content-Type: application/xml``.

        Args:
            xml: UBL 2.1 XML invoice or credit-note document as a string.

        Returns:
            Parsed invoice dict with supplier, customer, lines, totals, etc.

        Example::

            with open("invoice.xml", "r", encoding="utf-8") as f:
                parsed = client.documents.parse(f.read())
            print(parsed["invoiceNumber"], parsed["totals"]["withVat"])
        """
        return self._request(
            "POST",
            "/documents/parse",
            content=xml.encode("utf-8") if isinstance(xml, str) else xml,
            extra_headers={"Content-Type": "application/xml"},
        )

    def mark(
        self,
        id: str,
        state: DocumentMarkState,
        note: Optional[str] = None,
    ) -> DocumentMarkResponse:
        """Manually mark a document's lifecycle state.

        Use for documents delivered out-of-band (e.g. a receiver confirms
        over email) or to flag a failed/processed document in your own
        workflow.

        Args:
            id: Document UUID.
            state: One of ``"delivered"``, ``"processed"``, ``"failed"``, ``"read"``.
            note: Optional free-text note attached to the state change.

        Returns:
            Dict with ``id``, ``state``, ``status``, ``deliveredAt``,
            ``acknowledgedAt``, and ``readAt`` (any may be None).

        Example::

            result = client.documents.mark("doc-uuid", state="delivered", note="Confirmed by email")
            print(result["deliveredAt"])
        """
        body: Dict[str, Any] = {"state": state}
        if note is not None:
            body["note"] = note
        return self._request("POST", f"/documents/{quote(id, safe='')}/mark", json=body)
