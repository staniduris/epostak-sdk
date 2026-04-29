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
        DocumentEventsResponse,
        DocumentMarkResponse,
        DocumentMarkState,
        DocumentStatusResponse,
        InboxAllResponse,
        InboxDocumentDetailResponse,
        InboxListResponse,
        InvoiceRespondResponse,
        InvoiceResponsesListResponse,
        OutboxListResponse,
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


def _idempotency_headers(idempotency_key: Optional[str]) -> Optional[Dict[str, str]]:
    """Build the ``Idempotency-Key`` header dict, or ``None`` when no key was passed."""
    if not idempotency_key:
        return None
    return {"Idempotency-Key": idempotency_key}


class _BaseResource:
    """Shared base for all resource classes."""

    def __init__(
        self,
        client: httpx.Client,
        base_url: str,
        token_manager: Any,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
    ) -> None:
        self._client = client
        self._base_url = base_url
        self._token_manager = token_manager
        self._firm_id = firm_id
        self._max_retries = max_retries

    def _headers(self) -> Dict[str, str]:
        token = self._token_manager.get_access_token()
        headers: Dict[str, str] = {"Authorization": f"Bearer {token}"}
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
                last_exc = EPostakError(response.status_code, body, response.headers)

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
        token_manager: Any,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
    ) -> None:
        super().__init__(client, base_url, token_manager, firm_id, max_retries=max_retries)
        self.inbox = InboxResource(client, base_url, token_manager, firm_id, max_retries=max_retries)

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

    def send(
        self,
        body: Dict[str, Any],
        *,
        idempotency_key: Optional[str] = None,
    ) -> SendDocumentResponse:
        """Send a document via Peppol.

        Supports two modes:

        - **JSON mode**: pass structured data with ``items`` list, UBL is auto-generated.
        - **XML mode**: pass ``xml`` with a pre-built UBL XML string.

        Both modes require ``receiverPeppolId``.

        Document types (``docType``): ``"invoice"``, ``"credit_note"``,
        ``"correction"``, ``"self_billing"``, ``"reverse_charge"``,
        ``"self_billing_credit_note"``. Defaults to ``"invoice"`` when omitted.

        **Supplier-party pinning (XML mode).** When submitting raw UBL via
        ``xml``, the server pins the seller identity (Name, IČO, IČ DPH,
        Postal Address, Legal Entity name) to the authenticated firm.
        Caller-supplied values in ``cac:AccountingSupplierParty/cac:Party``
        are silently overwritten before forwarding to Peppol. The Peppol
        ``EndpointID`` is the only supplier-party field still validated
        against the firm's registered Peppol ID; mismatched EndpointIDs are
        rejected with HTTP 422. BG-24 attachments, line items, payment
        terms, and custom note fields are preserved as-is. For self-billing
        document types (UBL typecodes ``261``/``389``), the customer party
        (which is the authenticated firm) is rewritten instead.

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

            # Replay-safe send. The server returns 409 (idempotency_conflict)
            # if the same key is replayed before the original finishes.
            result = client.documents.send(
                {...},
                idempotency_key="fv-2026-001-send",
            )
            print(result["payload_sha256"])  # hex SHA-256 of UBL wire bytes
        """
        return self._request(
            "POST",
            "/documents/send",
            json=body,
            extra_headers=_idempotency_headers(idempotency_key),
        )

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

    def envelope(self, id: str) -> bytes:
        """Download the signed AS4 envelope for a document.

        Streams the exact multipart AS4 payload that was transmitted on the
        Peppol network (signed, timestamped, tamper-evident) straight from
        the 10-year WORM archive. The underlying MinIO bucket uses S3 Object
        Lock in COMPLIANCE mode, so the bytes you receive are cryptographically
        immutable — we cannot modify or delete them before the retention
        window expires.

        Available on the ``api-enterprise`` plan for every document that
        ever flowed through our Access Point, with no age limit during an
        active contract. Works for both inbound and outbound documents.

        The server also returns the following response headers (not exposed
        here because the method returns raw bytes):

        - ``X-Envelope-Archived-At`` -- ISO 8601 timestamp of when the
          envelope was written to WORM storage.
        - ``X-Envelope-Direction`` -- ``"inbound"`` or ``"outbound"``.
        - ``Content-Disposition: attachment; filename="{id}.as4"``.

        The archive cron runs on a short interval; brand-new documents may
        briefly 404 until their envelope has been persisted. Retry shortly
        or contact support if the condition persists.

        Args:
            id: Document UUID.

        Returns:
            Raw AS4 envelope bytes (multipart MIME, S/MIME-signed).

        Example::

            envelope_bytes = client.documents.envelope("doc-uuid")
            with open("doc-uuid.as4", "wb") as f:
                f.write(envelope_bytes)
        """
        response = self._request("GET", f"/documents/{quote(id, safe='')}/envelope", raw=True)
        return response.content

    def respond(self, id: str, status: str, note: Optional[str] = None) -> InvoiceRespondResponse:
        """Send an invoice response for a received document.

        Dispatches an Invoice Response (UBL Application Response) back to
        the supplier via Peppol AS4. The call returns synchronously:
        HTTP 200 on successful dispatch, HTTP 202 on dispatch failure
        (the response is still persisted and can be retried).

        An initial ``"UQ"`` (under query) response may be followed by a
        final status (``AP``/``RE``/``CA``/etc.); any other non-UQ status
        is terminal.

        Args:
            id: Document UUID.
            status: Response code. Must be one of the seven UBL Application
                Response codes (see :data:`~epostak.types.InvoiceResponseCode`):

                - ``"AB"`` accepted for billing
                - ``"IP"`` in process
                - ``"UQ"`` under query
                - ``"CA"`` conditionally accepted
                - ``"RE"`` rejected
                - ``"AP"`` accepted
                - ``"PD"`` paid

            note: Optional note to include with the response (max 500 chars).

        Returns:
            Dict with ``documentId``, ``responseStatus``, ``respondedAt``,
            ``peppolMessageId`` (None on dispatch failure), ``dispatchStatus``
            (``"sent"`` or ``"failed_queued"``), and optional ``dispatchError``.

        Example::

            result = client.documents.respond("doc-uuid", status="AP", note="Accepted")
            if result["dispatchStatus"] == "sent":
                print("Dispatched as", result["peppolMessageId"])
            else:
                print("Queued for retry:", result.get("dispatchError"))
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

    def send_batch(
        self,
        items: List[Dict[str, Any]],
        *,
        idempotency_key: Optional[str] = None,
    ) -> BatchSendResponse:
        """Send up to 50 documents in a single request.

        Each item uses the same body format as :meth:`send` and may carry an
        optional ``idempotencyKey`` field to make retries safe.  Partial
        failures do not fail the whole request -- inspect ``results`` and
        ``failed`` for per-item outcomes. Maximum request body size is 20 MB.

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
        return self._request(
            "POST",
            "/documents/send/batch",
            json={"items": items},
            extra_headers=_idempotency_headers(idempotency_key),
        )

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

    def outbox(
        self,
        offset: int = 0,
        limit: int = 20,
        status: Optional[str] = None,
        peppol_message_id: Optional[str] = None,
        since: Optional[str] = None,
    ) -> OutboxListResponse:
        """List sent (outbound) documents in the outbox.

        Args:
            offset: Pagination offset (default 0).
            limit: Page size, 1-100 (default 20).
            status: Filter by document status.
            peppol_message_id: Filter by Peppol AS4 message ID.
            since: ISO 8601 timestamp -- only return documents created after this date.

        Returns:
            Paginated response with ``documents``, ``total``, ``offset``, and ``limit``.

        Example::

            outbox = client.documents.outbox(limit=50, status="delivered")
            for doc in outbox["documents"]:
                print(doc["id"], doc["status"])
        """
        params = _build_query({
            "offset": offset,
            "limit": limit,
            "status": status,
            "peppolMessageId": peppol_message_id,
            "since": since,
        })
        return self._request("GET", "/documents/outbox", params=params)

    def responses(self, id: str) -> InvoiceResponsesListResponse:
        """List Invoice Responses associated with a document.

        Args:
            id: Document UUID.

        Returns:
            Dict with ``documentId`` and ``responses`` list.

        Example::

            resp = client.documents.responses("doc-uuid")
            for r in resp["responses"]:
                print(r["responseCode"], r["createdAt"])
        """
        return self._request("GET", f"/documents/{quote(id, safe='')}/responses")

    def events(
        self,
        id: str,
        limit: Optional[int] = None,
        cursor: Optional[str] = None,
    ) -> DocumentEventsResponse:
        """List audit trail events for a document.

        Args:
            id: Document UUID.
            limit: Maximum number of events to return (default 20).
            cursor: Cursor from previous page for cursor-based pagination.

        Returns:
            Dict with ``documentId``, ``events`` list, and ``nextCursor`` (str or None).

        Example::

            result = client.documents.events("doc-uuid", limit=50)
            for event in result["events"]:
                print(event["eventType"], event["occurredAt"])
            if result["nextCursor"]:
                next_page = client.documents.events("doc-uuid", cursor=result["nextCursor"])
        """
        params = _build_query({"limit": limit, "cursor": cursor})
        return self._request("GET", f"/documents/{quote(id, safe='')}/events", params=params)

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
