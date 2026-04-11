"""Type definitions for the ePostak Enterprise API.

This module contains every request/response type used by the SDK, defined as
:class:`~typing.TypedDict` subclasses for full static-typing support.

All types target Python 3.9+ compatibility.  Optional fields use
``NotRequired`` on 3.11+ or ``total=False`` base classes on older versions.

Literal aliases (e.g. :data:`InvoiceResponseCode`, :data:`WebhookEvent`) are
also exported for use in type annotations.
"""

from __future__ import annotations

import sys
from typing import Any, Dict, List, Literal, Optional, Union

if sys.version_info >= (3, 11):
    from typing import NotRequired, TypedDict
else:
    from typing import TypedDict

    # For Python 3.9/3.10: we use total=False on base classes for optional fields

# ---------------------------------------------------------------------------
# Shared primitives
# ---------------------------------------------------------------------------

InvoiceResponseCode = Literal["AP", "RE", "UQ"]
"""Response code for an invoice: ``"AP"`` (accepted), ``"RE"`` (rejected), ``"UQ"`` (under query)."""

WebhookEvent = Literal[
    "document.created",
    "document.sent",
    "document.received",
    "document.validated",
]
"""Event types that a webhook subscription can listen for."""

DocumentDirection = Literal["inbound", "outbound"]
"""Direction of a document relative to the authenticated firm."""

ConvertDirection = Literal["json_to_ubl", "ubl_to_json"]
"""Conversion direction for the ``/documents/convert`` endpoint."""

InboxStatus = Literal["RECEIVED", "ACKNOWLEDGED"]
"""Status filter for inbox queries: ``"RECEIVED"`` (new) or ``"ACKNOWLEDGED"`` (processed)."""

# ---------------------------------------------------------------------------
# Line items
# ---------------------------------------------------------------------------


class LineItem(TypedDict, total=False):
    """Line item for sending/validating documents."""

    description: str  # type: ignore[misc]  # Product or service description
    quantity: float  # type: ignore[misc]  # Number of units
    unit: str  # Unit of measure, e.g. "ks" (pieces), "hod" (hours)
    unitPrice: float  # type: ignore[misc]  # Price per unit excluding VAT
    vatRate: float  # type: ignore[misc]  # VAT rate as a percentage, e.g. 23
    discount: float  # Discount percentage (0-100) applied to the line total


class LineItemResponse(TypedDict, total=False):
    """Line item as returned by the API (read-only)."""

    description: str  # type: ignore[misc]  # Product or service description
    quantity: float  # type: ignore[misc]  # Number of units
    unit: str  # Unit of measure, e.g. "ks", "hod"
    unitPrice: float  # type: ignore[misc]  # Price per unit excluding VAT
    vatRate: float  # type: ignore[misc]  # VAT rate as a percentage, e.g. 23
    vatCategory: str  # UBL tax category code, e.g. "S" (standard)
    lineTotal: float  # type: ignore[misc]  # Calculated line total (quantity * unitPrice)


# ---------------------------------------------------------------------------
# Party
# ---------------------------------------------------------------------------


class PartyAddress(TypedDict, total=False):
    """Postal address of a business party."""

    street: str  # Street name and number
    city: str  # City or municipality
    zip: str  # Postal / ZIP code
    country: str  # ISO 3166-1 alpha-2 country code, e.g. "SK"


class Party(TypedDict, total=False):
    """Business entity appearing as supplier or customer on a document."""

    name: str  # Legal name of the company
    ico: str  # Slovak company ID (ICO), 8 digits
    dic: str  # Tax ID (DIC)
    icDph: str  # VAT ID (IC DPH), e.g. "SK2020123456"
    address: PartyAddress  # Postal address
    peppolId: str  # Peppol participant ID, e.g. "0245:1234567890"


# ---------------------------------------------------------------------------
# Send document
# ---------------------------------------------------------------------------


class _SendDocumentBase(TypedDict, total=False):
    """Base fields shared by JSON-mode and XML-mode send requests."""

    receiverPeppolId: str  # type: ignore[misc]  # Peppol ID of the receiver (required)
    invoiceNumber: str  # Invoice number, e.g. "FV-2026-001"
    issueDate: str  # Issue date in ISO 8601 / YYYY-MM-DD
    dueDate: str  # Payment due date in ISO 8601 / YYYY-MM-DD
    currency: str  # ISO 4217 currency code, default "EUR"
    note: str  # Free-text note included in the document
    iban: str  # Seller bank account IBAN for payment
    paymentMethod: str  # Payment method code, e.g. "30" (credit transfer)
    variableSymbol: str  # Slovak variable symbol for bank payments (max 10 digits)
    buyerReference: str  # Buyer's reference / purchase order number
    receiverName: str  # Receiver company name (auto-resolved if omitted)
    receiverIco: str  # Receiver ICO, 8 digits
    receiverDic: str  # Receiver tax ID (DIC)
    receiverIcDph: str  # Receiver VAT ID (IC DPH)
    receiverAddress: str  # Receiver street address
    receiverCountry: str  # Receiver ISO country code, e.g. "SK"
    items: List[LineItem]  # Line items (JSON mode) -- mutually exclusive with ``xml``
    xml: str  # Pre-built UBL XML string (XML mode) -- mutually exclusive with ``items``


# Union type: either JSON mode (with items) or XML mode (with xml)
SendDocumentRequest = Dict[str, Any]
"""Request body for ``documents.send()``.  Use ``items`` for JSON mode or ``xml`` for XML mode."""


class SendDocumentResponse(TypedDict):
    """Successful response from sending a document."""

    documentId: str  # UUID of the created document
    messageId: str  # Peppol AS4 message ID
    status: str  # Initial status, typically "SENT"


# ---------------------------------------------------------------------------
# Update document (draft only)
# ---------------------------------------------------------------------------


class UpdateDocumentRequest(TypedDict, total=False):
    """Fields that can be patched on a draft document. All are optional."""

    invoiceNumber: str  # New invoice number
    issueDate: str  # New issue date (ISO 8601 / YYYY-MM-DD)
    dueDate: Optional[str]  # New due date, or None to clear
    currency: str  # ISO 4217 currency code
    note: Optional[str]  # Free-text note, or None to clear
    iban: Optional[str]  # Seller IBAN, or None to clear
    variableSymbol: Optional[str]  # Variable symbol, or None to clear
    buyerReference: Optional[str]  # Buyer reference, or None to clear
    receiverName: str  # Receiver company name
    receiverIco: Optional[str]  # Receiver ICO
    receiverDic: Optional[str]  # Receiver tax ID (DIC)
    receiverIcDph: Optional[str]  # Receiver VAT ID (IC DPH)
    receiverAddress: Optional[str]  # Receiver street address
    receiverCountry: Optional[str]  # Receiver ISO country code
    receiverPeppolId: Optional[str]  # Receiver Peppol ID
    items: List[LineItem]  # Replacement line items (full overwrite)


# ---------------------------------------------------------------------------
# Document (shared response shape)
# ---------------------------------------------------------------------------


class DocumentTotals(TypedDict):
    """Monetary totals for a document."""

    withoutVat: float  # Total amount excluding VAT
    vat: float  # Total VAT amount
    withVat: float  # Total amount including VAT


class Document(TypedDict, total=False):
    """Full document as returned by the API."""

    id: str  # type: ignore[misc]  # Unique document UUID
    number: str  # type: ignore[misc]  # Invoice / document number
    status: str  # type: ignore[misc]  # Current status, e.g. "SENT", "DELIVERED", "DRAFT"
    direction: str  # type: ignore[misc]  # "inbound" or "outbound"
    docType: str  # type: ignore[misc]  # UBL document type, e.g. "Invoice", "CreditNote"
    issueDate: str  # type: ignore[misc]  # Issue date (YYYY-MM-DD)
    dueDate: Optional[str]  # Payment due date (YYYY-MM-DD), may be absent
    currency: str  # type: ignore[misc]  # ISO 4217 currency code
    supplier: Party  # Supplier (seller) party details
    customer: Party  # Customer (buyer) party details
    lines: List[LineItemResponse]  # Document line items
    totals: DocumentTotals  # Calculated monetary totals
    peppolMessageId: Optional[str]  # Peppol AS4 message ID, None for drafts
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp
    updatedAt: str  # type: ignore[misc]  # ISO 8601 last-update timestamp


# ---------------------------------------------------------------------------
# Inbox
# ---------------------------------------------------------------------------


class InboxListResponse(TypedDict):
    """Paginated list of inbox (received) documents."""

    documents: List[Document]  # Array of documents on the current page
    total: int  # Total number of matching documents
    limit: int  # Requested page size
    offset: int  # Current pagination offset


class InboxDocumentDetailResponse(TypedDict):
    """Detail view of a single inbox document including its UBL payload."""

    document: Document  # Full document object
    payload: Optional[str]  # Raw UBL XML string, or None if unavailable


class AcknowledgeResponse(TypedDict):
    """Response after acknowledging an inbox document."""

    documentId: str  # UUID of the acknowledged document
    status: str  # New status, typically "ACKNOWLEDGED"
    acknowledgedAt: str  # ISO 8601 timestamp of acknowledgement


# ---------------------------------------------------------------------------
# Inbox all (integrator - cross-firm inbox)
# ---------------------------------------------------------------------------


class _InboxAllParty(TypedDict, total=False):
    """Abbreviated party info in cross-firm inbox responses (snake_case)."""

    name: Optional[str]  # Company name
    ico: Optional[str]  # Slovak company ID (ICO)
    peppol_id: Optional[str]  # Peppol participant ID


class _InboxAllTotals(TypedDict, total=False):
    """Monetary totals in cross-firm inbox responses (snake_case)."""

    without_vat: Optional[float]  # Total excluding VAT
    vat: Optional[float]  # VAT amount
    with_vat: Optional[float]  # Total including VAT


class InboxAllDocument(TypedDict, total=False):
    """Document in a cross-firm (integrator) inbox listing.

    Uses snake_case field names, unlike single-firm :class:`Document`.
    """

    firm_id: str  # type: ignore[misc]  # UUID of the firm that received the document
    firm_name: Optional[str]  # Human-readable firm name
    id: str  # type: ignore[misc]  # Document UUID
    number: Optional[str]  # Invoice / document number
    status: str  # type: ignore[misc]  # Current status
    direction: str  # type: ignore[misc]  # Always "inbound" for inbox
    doc_type: str  # type: ignore[misc]  # UBL document type
    issue_date: Optional[str]  # Issue date (YYYY-MM-DD)
    due_date: Optional[str]  # Due date (YYYY-MM-DD)
    currency: str  # type: ignore[misc]  # ISO 4217 currency code
    supplier: _InboxAllParty  # Supplier party (abbreviated)
    customer: _InboxAllParty  # Customer party (abbreviated)
    totals: _InboxAllTotals  # Monetary totals
    peppol_message_id: Optional[str]  # Peppol AS4 message ID
    created_at: str  # type: ignore[misc]  # ISO 8601 creation timestamp


class InboxAllResponse(TypedDict):
    """Paginated cross-firm inbox listing (integrator only)."""

    documents: List[InboxAllDocument]  # Documents on the current page
    total: int  # Total matching documents across all firms
    limit: int  # Requested page size
    offset: int  # Current pagination offset


# ---------------------------------------------------------------------------
# Document lifecycle - status
# ---------------------------------------------------------------------------


class StatusHistoryEntry(TypedDict, total=False):
    """Single entry in a document's status history timeline."""

    status: str  # type: ignore[misc]  # Status at this point, e.g. "SENT", "DELIVERED"
    timestamp: str  # type: ignore[misc]  # ISO 8601 timestamp of the transition
    detail: Optional[str]  # Human-readable detail or reason for the transition


class DocumentStatusResponse(TypedDict, total=False):
    """Full document status with lifecycle history and delivery metadata."""

    id: str  # type: ignore[misc]  # Document UUID
    status: str  # type: ignore[misc]  # Current status
    documentType: Optional[str]  # UBL document type, e.g. "Invoice"
    senderPeppolId: Optional[str]  # Sender Peppol ID
    receiverPeppolId: Optional[str]  # Receiver Peppol ID
    statusHistory: List[StatusHistoryEntry]  # type: ignore[misc]  # Chronological status transitions
    validationResult: Optional[Dict[str, Any]]  # Validation result if applicable
    deliveredAt: Optional[str]  # ISO 8601 timestamp of successful delivery
    acknowledgedAt: Optional[str]  # ISO 8601 timestamp of receiver acknowledgement
    invoiceResponseStatus: Optional[str]  # "AP", "RE", or "UQ" if responded to
    as4MessageId: Optional[str]  # Peppol AS4 message ID
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp
    updatedAt: str  # type: ignore[misc]  # ISO 8601 last-update timestamp


# ---------------------------------------------------------------------------
# Document lifecycle - evidence
# ---------------------------------------------------------------------------


class _InvoiceResponseEvidence(TypedDict, total=False):
    """Invoice response evidence embedded in delivery evidence."""

    status: Optional[str]  # Response status: "AP", "RE", or "UQ"
    document: Dict[str, Any]  # type: ignore[misc]  # Raw invoice response document


class DocumentEvidenceResponse(TypedDict, total=False):
    """Delivery evidence for a sent document (AS4 receipts, MLR, invoice response)."""

    documentId: str  # type: ignore[misc]  # Document UUID
    as4Receipt: Optional[Dict[str, Any]]  # AS4 receipt from the access point
    mlrDocument: Optional[Dict[str, Any]]  # Message Level Response document
    invoiceResponse: Optional[_InvoiceResponseEvidence]  # Invoice response from the receiver
    deliveredAt: Optional[str]  # ISO 8601 timestamp of delivery confirmation
    sentAt: Optional[str]  # ISO 8601 timestamp when the document was sent


# ---------------------------------------------------------------------------
# Invoice response (respond to received document)
# ---------------------------------------------------------------------------


class InvoiceRespondResponse(TypedDict):
    """Response after sending an invoice response (accept/reject/query)."""

    documentId: str  # UUID of the document responded to
    responseStatus: str  # The response code sent: "AP", "RE", or "UQ"
    respondedAt: str  # ISO 8601 timestamp of the response


# ---------------------------------------------------------------------------
# Validate / preflight / convert
# ---------------------------------------------------------------------------


class ValidationResult(TypedDict, total=False):
    """Result of document validation (without sending)."""

    valid: bool  # type: ignore[misc]  # True if the document passes all checks
    warnings: List[str]  # type: ignore[misc]  # Non-fatal warnings (may still send)
    ubl: Optional[str]  # Generated UBL XML string (JSON mode only)


class PreflightResult(TypedDict, total=False):
    """Result of a preflight check on a receiver's Peppol capability."""

    receiverPeppolId: str  # type: ignore[misc]  # Peppol ID that was checked
    registered: bool  # type: ignore[misc]  # True if the participant is registered on Peppol
    supportsDocumentType: bool  # type: ignore[misc]  # True if the participant accepts the doc type
    smpUrl: Optional[str]  # SMP lookup URL used for the check


class ConvertResult(TypedDict):
    """Result of a JSON-to-UBL or UBL-to-JSON conversion."""

    direction: str  # The conversion direction that was used
    result: Union[str, Dict[str, Any]]  # UBL XML string (json_to_ubl) or parsed JSON dict (ubl_to_json)


# ---------------------------------------------------------------------------
# Peppol
# ---------------------------------------------------------------------------


class SmpParticipantCapability(TypedDict):
    """A single document type capability advertised via SMP."""

    documentTypeId: str  # UBL document type identifier
    processId: str  # Peppol process identifier
    transportProfile: str  # Transport profile, e.g. "peppol-transport-as4-v2_0"


class PeppolParticipant(TypedDict, total=False):
    """Peppol participant info returned by SMP lookup."""

    peppolId: str  # type: ignore[misc]  # Full Peppol participant ID (scheme:id)
    name: Optional[str]  # Registered business name, if available
    country: Optional[str]  # ISO country code, if available
    capabilities: List[SmpParticipantCapability]  # type: ignore[misc]  # Supported document types


class DirectoryEntry(TypedDict, total=False):
    """A single entry from the Peppol Business Card directory."""

    peppolId: str  # type: ignore[misc]  # Peppol participant ID
    name: str  # type: ignore[misc]  # Registered business name
    country: str  # type: ignore[misc]  # ISO country code
    registeredAt: Optional[str]  # ISO 8601 registration date, if known


class DirectorySearchResult(TypedDict):
    """Paginated result from a Peppol directory search."""

    results: List[DirectoryEntry]  # Matching directory entries
    total: int  # Total number of matches
    page: int  # Current page number (0-based)
    page_size: int  # Requested page size


class CompanyLookup(TypedDict, total=False):
    """Slovak company info returned by ICO lookup."""

    ico: str  # type: ignore[misc]  # Slovak company ID (ICO), 8 digits
    name: str  # type: ignore[misc]  # Legal company name
    dic: Optional[str]  # Tax ID (DIC)
    icDph: Optional[str]  # VAT ID (IC DPH)
    address: PartyAddress  # Registered address
    peppolId: Optional[str]  # Peppol ID if the company is registered on Peppol


# ---------------------------------------------------------------------------
# Firms
# ---------------------------------------------------------------------------


class FirmSummary(TypedDict, total=False):
    """Abbreviated firm info returned in list views."""

    id: str  # type: ignore[misc]  # Firm UUID
    name: str  # type: ignore[misc]  # Legal company name
    ico: Optional[str]  # Slovak company ID (ICO)
    peppolId: Optional[str]  # Primary Peppol participant ID
    peppolStatus: str  # type: ignore[misc]  # Peppol registration status, e.g. "ACTIVE"


class FirmPeppolIdentifier(TypedDict):
    """A Peppol identifier registered for a firm."""

    scheme: str  # Peppol scheme, e.g. "0245" (SK DIČ) or "9950" (SK IČ DPH)
    identifier: str  # Identifier value within the scheme


class FirmDetail(TypedDict, total=False):
    """Full firm detail including all identifiers and address."""

    id: str  # type: ignore[misc]  # Firm UUID
    name: str  # type: ignore[misc]  # Legal company name
    ico: Optional[str]  # Slovak company ID (ICO)
    peppolId: Optional[str]  # Primary Peppol participant ID
    peppolStatus: str  # type: ignore[misc]  # Peppol registration status
    dic: Optional[str]  # Tax ID (DIC)
    icDph: Optional[str]  # VAT ID (IC DPH)
    address: PartyAddress  # Registered address
    peppolIdentifiers: List[FirmPeppolIdentifier]  # type: ignore[misc]  # All registered Peppol IDs
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp


class PeppolIdentifierResponse(TypedDict):
    """Response after registering a Peppol identifier for a firm."""

    peppolId: str  # Full Peppol ID (scheme:identifier)
    scheme: str  # Peppol scheme used
    identifier: str  # Identifier value
    registeredAt: str  # ISO 8601 registration timestamp


# ---------------------------------------------------------------------------
# Firm assignment (integrator)
# ---------------------------------------------------------------------------


class _AssignedFirm(TypedDict, total=False):
    """Firm info returned in assignment responses (snake_case)."""

    id: str  # type: ignore[misc]  # Firm UUID
    name: Optional[str]  # Legal company name
    ico: Optional[str]  # Slovak company ID (ICO)
    peppol_id: Optional[str]  # Peppol participant ID
    peppol_status: str  # type: ignore[misc]  # Peppol registration status


class AssignFirmResponse(TypedDict):
    """Response after assigning a single firm to the integrator."""

    firm: _AssignedFirm  # The assigned firm
    status: str  # Assignment status, e.g. "active"


class BatchAssignFirmResult(TypedDict, total=False):
    """Result for one ICO in a batch assignment request."""

    ico: str  # type: ignore[misc]  # The ICO that was processed
    firm: _AssignedFirm  # Firm details (present on success)
    status: str  # "active" on success, "error" on failure
    error: str  # Error code if assignment failed
    message: str  # Human-readable error message


class BatchAssignFirmsResponse(TypedDict):
    """Response from batch firm assignment."""

    results: List[BatchAssignFirmResult]  # One result per ICO in the request


# ---------------------------------------------------------------------------
# Webhooks
# ---------------------------------------------------------------------------


class Webhook(TypedDict, total=False):
    """Webhook subscription (list view, no secret)."""

    id: str  # type: ignore[misc]  # Webhook UUID
    url: str  # type: ignore[misc]  # Delivery URL
    events: List[str]  # type: ignore[misc]  # Subscribed event types
    isActive: bool  # type: ignore[misc]  # Whether the webhook is enabled
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp


class WebhookDetail(TypedDict, total=False):
    """Webhook subscription detail returned on creation (includes secret)."""

    id: str  # type: ignore[misc]  # Webhook UUID
    url: str  # type: ignore[misc]  # Delivery URL
    events: List[str]  # type: ignore[misc]  # Subscribed event types
    isActive: bool  # type: ignore[misc]  # Whether the webhook is enabled
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp
    secret: str  # HMAC-SHA256 signing secret -- store securely for verification


class WebhookDelivery(TypedDict, total=False):
    """Record of a single webhook delivery attempt."""

    id: str  # type: ignore[misc]  # Delivery UUID
    webhookId: str  # type: ignore[misc]  # Parent webhook UUID
    event: str  # type: ignore[misc]  # Event type, e.g. "document.received"
    status: str  # type: ignore[misc]  # Delivery status: "success", "failed", "pending"
    attempts: int  # type: ignore[misc]  # Number of delivery attempts made
    responseStatus: Optional[int]  # HTTP status code from your endpoint, or None
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp


class WebhookWithDeliveries(TypedDict, total=False):
    """Webhook detail including recent delivery history."""

    id: str  # type: ignore[misc]  # Webhook UUID
    url: str  # type: ignore[misc]  # Delivery URL
    events: List[str]  # type: ignore[misc]  # Subscribed event types
    isActive: bool  # type: ignore[misc]  # Whether the webhook is enabled
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp
    deliveries: List[WebhookDelivery]  # type: ignore[misc]  # Recent delivery attempts


# ---------------------------------------------------------------------------
# Webhook pull queue
# ---------------------------------------------------------------------------


class WebhookQueueItem(TypedDict):
    """A single event from the webhook pull queue."""

    id: str  # Event UUID (use this to acknowledge)
    type: str  # Event type, e.g. "document.received"
    created_at: str  # ISO 8601 timestamp when the event was created
    payload: Dict[str, Any]  # Event-specific payload data


class WebhookQueueResponse(TypedDict):
    """Response from pulling the webhook event queue."""

    items: List[WebhookQueueItem]  # Events on this page
    has_more: bool  # True if more events are available to pull


# ---------------------------------------------------------------------------
# Webhook queue all (integrator - cross-firm)
# ---------------------------------------------------------------------------


class WebhookQueueAllEvent(TypedDict):
    """A single event from the cross-firm webhook queue (integrator only)."""

    event_id: str  # Event UUID (use this to acknowledge)
    firm_id: str  # UUID of the firm the event belongs to
    event: str  # Event type, e.g. "document.received"
    payload: Dict[str, Any]  # Event-specific payload data
    created_at: str  # ISO 8601 timestamp when the event was created


class WebhookQueueAllResponse(TypedDict):
    """Response from pulling events across all firms (integrator only)."""

    events: List[WebhookQueueAllEvent]  # Cross-firm events
    count: int  # Number of events returned


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------


class _StatsPeriod(TypedDict):
    """Date range for a statistics query."""

    from_: str  # Start date (ISO 8601 / YYYY-MM-DD)
    to: str  # End date (ISO 8601 / YYYY-MM-DD)


class _StatsOutbound(TypedDict):
    """Outbound (sent) document statistics."""

    total: int  # Total outbound documents in the period
    delivered: int  # Successfully delivered documents
    failed: int  # Failed delivery attempts


class _StatsInbound(TypedDict):
    """Inbound (received) document statistics."""

    total: int  # Total inbound documents in the period
    acknowledged: int  # Documents marked as acknowledged
    pending: int  # Documents still awaiting acknowledgement


class Statistics(TypedDict):
    """Aggregated document statistics for a date range."""

    period: Dict[str, str]  # {"from": "...", "to": "..."} date range
    outbound: _StatsOutbound  # Outbound document counts
    inbound: _StatsInbound  # Inbound document counts


# ---------------------------------------------------------------------------
# Account
# ---------------------------------------------------------------------------


class _AccountFirm(TypedDict, total=False):
    """Firm info embedded in the account response."""

    name: str  # type: ignore[misc]  # Legal company name
    ico: Optional[str]  # Slovak company ID (ICO)
    peppolId: Optional[str]  # Primary Peppol participant ID
    peppolStatus: str  # type: ignore[misc]  # Peppol registration status


class _AccountPlan(TypedDict):
    """Subscription plan info."""

    name: str  # Plan name, e.g. "Enterprise", "Starter"
    status: str  # Plan status, e.g. "active", "trialing"


class _AccountUsage(TypedDict):
    """Current billing period document usage."""

    outbound: int  # Number of outbound documents sent
    inbound: int  # Number of inbound documents received


class Account(TypedDict):
    """Full account information including firm, plan, and usage."""

    firm: _AccountFirm  # Firm details for the authenticated API key
    plan: _AccountPlan  # Current subscription plan
    usage: _AccountUsage  # Document usage in the current billing period


# ---------------------------------------------------------------------------
# Extract
# ---------------------------------------------------------------------------


class ExtractResult(TypedDict, total=False):
    """Result of AI-powered extraction from a single file."""

    extraction: Dict[str, Any]  # type: ignore[misc]  # Structured invoice data extracted by AI
    ubl_xml: str  # type: ignore[misc]  # Generated UBL XML from the extraction
    confidence: float  # type: ignore[misc]  # AI confidence score (0.0 - 1.0)
    file_name: str  # type: ignore[misc]  # Name of the processed file


class BatchExtractItem(TypedDict, total=False):
    """Result for a single file within a batch extraction."""

    file_name: str  # type: ignore[misc]  # Name of the processed file
    extraction: Dict[str, Any]  # Structured invoice data (present on success)
    ubl_xml: str  # Generated UBL XML (present on success)
    confidence: str  # AI confidence score as string (present on success)
    error: str  # Error message (present on failure)


class BatchExtractResult(TypedDict):
    """Result of batch AI extraction across multiple files."""

    batch_id: str  # Unique batch processing ID
    total: int  # Total number of files submitted
    successful: int  # Number of successfully extracted files
    failed: int  # Number of files that failed extraction
    results: List[BatchExtractItem]  # Per-file results
