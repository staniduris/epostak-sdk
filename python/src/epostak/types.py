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

InvoiceResponseCode = Literal["AB", "IP", "UQ", "CA", "RE", "AP", "PD"]
"""Invoice response codes (UBL Application Response, UNCL4343 / EN 16931).

- ``"AB"`` accepted for billing
- ``"IP"`` in process
- ``"UQ"`` under query
- ``"CA"`` conditionally accepted
- ``"RE"`` rejected
- ``"AP"`` accepted
- ``"PD"`` paid
"""

WebhookEvent = Literal[
    "document.created",
    "document.sent",
    "document.received",
    "document.validated",
    "document.delivered",
    "document.delivery_failed",
    "document.rejected",
    "document.response_received",
]
"""Event types that a webhook subscription can listen for."""


class WebhookPayloadData(TypedDict, total=False):
    """Business-data shape carried by webhook events.

    Common fields (``document_id``, ``direction``, ``doctype_key``,
    ``status``, ``previous_status``) are always present.  Event-specific
    extras (``sent_at``, ``response_code``, etc.) are optional so a single
    handler can branch on ``event`` without casts.
    """

    document_id: str  # type: ignore[misc]  # Document UUID
    direction: Literal["inbound", "outbound"]  # type: ignore[misc]  # Document direction
    doctype_key: str  # type: ignore[misc]  # Peppol doctype key, e.g. "invoice"
    status: str  # type: ignore[misc]  # Document status after this event
    previous_status: Optional[str]  # type: ignore[misc]  # Status before this event, None for create events

    # Common billing fields (present on most events)
    document_number: Optional[str]  # Human invoice/document number
    total_amount: Optional[str]  # Total amount as string-encoded decimal
    currency: Optional[str]  # ISO 4217 currency code
    issue_date: Optional[str]  # YYYY-MM-DD issue date
    due_date: Optional[str]  # YYYY-MM-DD due date
    sender_peppol_id: Optional[str]  # Sender Peppol participant identifier
    receiver_peppol_id: Optional[str]  # Receiver Peppol participant identifier

    # Event-specific extras

    # document.sent
    sent_at: Optional[str]  # Wall-clock time the AS4 send succeeded

    # document.received
    received_at: Optional[str]  # AS4 ingest moment

    # document.delivered
    delivered_at: Optional[str]  # When the receiving AP confirmed delivery

    # document.delivered / document.sent / document.received
    as4_message_id: Optional[str]  # AS4 EBMS message ID

    # document.rejected
    rejected_at: Optional[str]  # When the rejection arrived

    # document.response_received
    responded_at: Optional[str]  # When the buyer response arrived

    # document.rejected / document.response_received
    response_code: Optional[InvoiceResponseCode]  # Buyer response code
    response_reason: Optional[str]  # Human-readable rejection/response note
    responder: Optional[Literal["peer_ap", "buyer", "loopback"]]  # Which side produced the response

    # document.delivery_failed
    failure_reason: Optional[str]  # Final error message from the queue (truncated 400 chars)
    attempts: Optional[int]  # Total number of attempts before giving up


class WebhookPayload(TypedDict):
    """Common envelope for every v1 webhook payload.

    Received via push (POST to your URL) or pull (one item from
    ``webhooks.queue.pull()``).  ``webhook_id`` is ``None`` for pulled items;
    ``webhook_event_id`` is ``None`` when no pull subscription exists.
    """

    event: WebhookEvent
    event_version: Literal["1"]
    webhook_id: Optional[str]
    webhook_event_id: Optional[str]
    timestamp: str
    data: WebhookPayloadData


DocumentDirection = Literal["inbound", "outbound"]
"""Direction of a document relative to the authenticated firm."""

ConvertInputFormat = Literal["json", "ubl"]
"""Input format for the ``/documents/convert`` endpoint."""

ConvertOutputFormat = Literal["ubl", "json"]
"""Output format for the ``/documents/convert`` endpoint."""

InboxStatus = Literal["RECEIVED", "ACKNOWLEDGED"]
"""Status filter for inbox queries: ``"RECEIVED"`` (new) or ``"ACKNOWLEDGED"`` (processed)."""

# ---------------------------------------------------------------------------
# Connector
# ---------------------------------------------------------------------------

ConnectorOutcome = Literal["ready", "ready_with_warnings", "blocked", "retry_later"]


class ConnectorPreflightRequest(TypedDict):
    """Request body for ``POST /connector/preflight``."""

    receiverPeppolId: str
    document: Dict[str, Any]


class ConnectorRepairItem(TypedDict):
    code: str
    field: Optional[str]
    message: str
    category: str
    autoFixable: bool


class ConnectorSafeFix(TypedDict):
    code: str
    field: Optional[str]
    message: str
    applied: bool


class ConnectorRepairReport(TypedDict):
    summary: str
    blocking: List[ConnectorRepairItem]
    warnings: List[ConnectorRepairItem]


class ConnectorPreflightResponse(TypedDict, total=False):
    ready: bool
    outcome: ConnectorOutcome
    repairReport: ConnectorRepairReport
    warnings: List[ConnectorRepairItem]
    safeFixes: List[ConnectorSafeFix]
    recipient: Optional[Dict[str, Any]]
    documentProfile: Optional[Dict[str, Any]]
    checks: Dict[str, Any]


ConnectorSendRequest = Dict[str, Any]
"""Arbitrary ERP document payload accepted by ``POST /connector/send``."""


class ConnectorSendResponse(TypedDict, total=False):
    documentId: str
    status: str
    outcome: str
    links: Dict[str, Any]


class ConnectorEvent(TypedDict, total=False):
    id: str
    documentId: Optional[str]
    type: str
    occurredAt: Optional[str]
    status: str
    data: Dict[str, Any]


class ConnectorStatusResponse(TypedDict, total=False):
    documentId: str
    status: str
    deliveredAt: Optional[str]
    events: List[ConnectorEvent]


class ConnectorInboxDocument(TypedDict, total=False):
    documentId: str
    status: str
    direction: Optional[str]
    documentType: Optional[str]
    documentNumber: Optional[str]
    senderPeppolId: Optional[str]
    receiverPeppolId: Optional[str]
    acknowledgedAt: Optional[str]
    payload: Optional[str]
    payloadFormat: Optional[str]
    links: Dict[str, str]


class ConnectorInboxListResponse(TypedDict, total=False):
    documents: List[ConnectorInboxDocument]
    nextCursor: Optional[str]
    hasMore: bool


class ConnectorAckResponse(TypedDict, total=False):
    documentId: str
    status: str
    acknowledged: bool
    idempotent: bool
    acknowledgedAt: Optional[str]


class ConnectorEventsResponse(TypedDict, total=False):
    events: List[ConnectorEvent]
    nextCursor: Optional[str]
    hasMore: bool


ConnectorBusinessDocumentType = Literal[
    "invoice",
    "credit_note",
    "self_billing_invoice",
    "self_billing_credit_note",
]
ConnectorBusinessState = Literal[
    "queued",
    "sending",
    "delivered",
    "received",
    "processed",
    "needs_attention",
    "failed",
    "cancelled",
]
ConnectorBusinessEventType = Literal[
    "document.queued",
    "document.delivered",
    "document.received",
    "document.processed",
    "document.needs_attention",
    "document.cancelled",
    "document.failed",
]


class _ConnectorBusinessEventDataRequired(TypedDict):
    direction: Optional[Literal["outbound", "inbound"]]
    type: ConnectorBusinessDocumentType
    number: Optional[str]
    response: Optional["ConnectorBusinessInvoiceResponse"]


class ConnectorBusinessEventData(_ConnectorBusinessEventDataRequired, total=False):
    customerRef: str


class ConnectorBusinessEvent(TypedDict):
    id: str
    customerRef: str
    documentId: str
    type: ConnectorBusinessEventType
    state: ConnectorBusinessState
    occurredAt: str
    data: ConnectorBusinessEventData


class ConnectorWebhookTestEvent(ConnectorBusinessEvent):
    test: Literal[True]


class ConnectorWebhook(TypedDict):
    id: str
    url: str
    events: List[ConnectorBusinessEventType]
    active: bool
    failedAttempts: int
    createdAt: str
    updatedAt: str


class _ConnectorWebhookConfigurationRequired(TypedDict):
    webhook: Optional[ConnectorWebhook]


class ConnectorWebhookConfiguration(_ConnectorWebhookConfigurationRequired, total=False):
    secret: str


class ConnectorWebhookTestResponse(TypedDict):
    deliveryId: str
    status: Literal["queued"]
    event: ConnectorWebhookTestEvent


class ConnectorWebhookDelivery(TypedDict):
    id: str
    webhookId: str
    eventId: Optional[str]
    customerRef: Optional[str]
    type: ConnectorBusinessEventType
    status: Literal["PENDING", "SUCCESS", "FAILED", "RETRYING"]
    attempts: int
    responseStatus: Optional[int]
    responseTimeMs: Optional[int]
    lastAttemptAt: Optional[str]
    nextRetryAt: Optional[str]
    createdAt: str


class ConnectorWebhookDeliveriesResponse(TypedDict):
    deliveries: List[ConnectorWebhookDelivery]
    nextCursor: Optional[str]
    hasMore: bool


class ConnectorBusinessEventsResponse(TypedDict):
    events: List[ConnectorBusinessEvent]
    nextCursor: Optional[str]
    hasMore: bool


class ConnectorBusinessAddress(TypedDict, total=False):
    street: str
    city: str
    postalCode: str


class _ConnectorBusinessRecipientRequired(TypedDict):
    country: str


class ConnectorBusinessRecipient(_ConnectorBusinessRecipientRequired, total=False):
    name: str
    companyId: str
    taxId: str
    vatId: str
    networkId: str
    address: ConnectorBusinessAddress


class _ConnectorBusinessLineRequired(TypedDict):
    description: str
    quantity: float
    unitPrice: float
    vatRate: float


class ConnectorBusinessLine(_ConnectorBusinessLineRequired, total=False):
    unit: str
    taxTreatment: str
    discount: float
    deliveryDate: str
    lineType: str
    advanceInvoiceReference: str
    customsTariffCode: str
    commodityClassificationCode: str
    commodityClassificationListId: str
    reverseChargeParagraphLetter: str
    controlStatementType: str
    controlStatementQuantity: float
    controlStatementUnit: str


class _ConnectorBusinessPrepaymentRequired(TypedDict):
    amountWithVat: float


class ConnectorBusinessPrepayment(_ConnectorBusinessPrepaymentRequired, total=False):
    advanceInvoiceRef: str
    taxDocumentRef: str
    settlementDate: str
    amountWithoutVat: float
    vatAmount: float
    vatRate: float
    taxTreatment: str


class _ConnectorBusinessAttachmentRequired(TypedDict):
    fileName: str
    mimeType: str
    content: str


class ConnectorBusinessAttachment(_ConnectorBusinessAttachmentRequired, total=False):
    description: str


class _ConnectorSubmitDocumentRequestRequired(TypedDict):
    externalId: str
    number: str
    recipient: ConnectorBusinessRecipient
    lines: List[ConnectorBusinessLine]


class ConnectorBusinessDocumentRequest(_ConnectorSubmitDocumentRequestRequired, total=False):
    type: ConnectorBusinessDocumentType
    precedingDocumentNumber: str
    issueDate: str
    dueDate: str
    currency: str
    note: str
    iban: str
    paymentMethod: str
    variableSymbol: str
    buyerReference: str
    prepaidAmount: float
    prepayments: List[ConnectorBusinessPrepayment]
    attachments: List[ConnectorBusinessAttachment]


class ConnectorSubmitDocumentRequest(TypedDict, total=False):
    """Autopilot submit payload retained for source compatibility."""

    customerRef: str
    mode: "ConnectorAutopilotMode"
    externalId: Optional[str]
    idempotencyKey: Optional[str]
    payload: ConnectorSendRequest
    send: "ConnectorSendPolicyOptions"
    options: Dict[str, Any]


class ConnectorBusinessAmounts(TypedDict, total=False):
    withoutTax: Optional[float]
    tax: Optional[float]
    total: Optional[float]
    due: Optional[float]


class ConnectorBusinessParty(TypedDict, total=False):
    name: Optional[str]
    country: Optional[str]
    companyId: Optional[str]
    taxId: Optional[str]
    vatId: Optional[str]
    resolution: Optional[Literal["verified"]]


class ConnectorBusinessInvoiceResponse(TypedDict):
    """Latest business response projected on Connector list/detail results."""

    status: "ConnectorInvoiceResponseStatus"
    direction: Literal["sent", "received"]
    reason: Optional[str]
    respondedAt: Optional[str]


class ConnectorBusinessDocument(TypedDict, total=False):
    id: str
    customerRef: str
    externalId: Optional[str]
    direction: Literal["outbound", "inbound"]
    type: ConnectorBusinessDocumentType
    number: Optional[str]
    state: ConnectorBusinessState
    replayed: bool
    currency: Optional[str]
    amounts: ConnectorBusinessAmounts
    sender: ConnectorBusinessParty
    recipient: ConnectorBusinessParty
    issueDate: Optional[str]
    dueDate: Optional[str]
    processedAt: Optional[str]
    processedReference: Optional[str]
    createdAt: Optional[str]
    updatedAt: Optional[str]
    response: Optional[ConnectorBusinessInvoiceResponse]
    links: Dict[str, str]


class ConnectorBusinessDocumentListResponse(TypedDict):
    documents: List[ConnectorBusinessDocument]
    nextCursor: Optional[str]
    hasMore: bool


class ConnectorBusinessAcknowledgeResponse(TypedDict):
    id: str
    customerRef: str
    state: ConnectorBusinessState
    processedAt: str
    reference: str
    idempotent: bool


ConnectorInvoiceResponseStatus = Literal[
    "received",
    "in_process",
    "under_query",
    "conditionally_accepted",
    "rejected",
    "accepted",
    "paid",
]


class _ConnectorInvoiceResponseRequestRequired(TypedDict):
    status: ConnectorInvoiceResponseStatus


class ConnectorInvoiceResponseRequest(_ConnectorInvoiceResponseRequestRequired, total=False):
    note: str


class ConnectorInvoiceResponseDelivery(TypedDict):
    status: ConnectorInvoiceResponseStatus
    direction: Literal["sent"]
    delivery: Literal["sent", "queued"]
    respondedAt: str


class ConnectorInvoiceResponseResult(TypedDict):
    id: str
    customerRef: str
    response: ConnectorInvoiceResponseDelivery
    idempotent: bool


ConnectorAutopilotMode = Literal["shadow", "stage", "send"]
ConnectorAutopilotLifecycleStatus = Literal[
    "received",
    "shadow_validated",
    "needs_repair",
    "staged",
    "sending",
    "sent",
    "delivered",
    "processed",
    "retry_later",
    "failed_external",
    "failed_internal",
    "cancelled",
]


class ConnectorAutopilotRequest(TypedDict, total=False):
    customerRef: str
    mode: ConnectorAutopilotMode
    externalId: Optional[str]
    idempotencyKey: Optional[str]
    payload: ConnectorSendRequest
    send: "ConnectorSendPolicyOptions"
    options: Dict[str, Any]


class ConnectorAutopilotRunResponse(TypedDict, total=False):
    autopilotId: str
    externalId: Optional[str]
    idempotencyKey: Optional[str]
    mode: str
    lifecycleStatus: str
    replayed: bool
    preflight: Optional[Dict[str, Any]]
    repairReport: Optional[Dict[str, Any]]
    safeFixes: List[ConnectorSafeFix]
    send: Optional[Dict[str, Any]]
    status: Optional[Dict[str, Any]]
    lastError: Optional[Dict[str, Any]]
    documentId: Optional[str]
    outboxId: Optional[str]
    sentAt: Optional[str]
    deliveredAt: Optional[str]
    createdAt: Optional[str]
    updatedAt: Optional[str]
    nextActions: List[str]
    links: Dict[str, str]


ConnectorMapperSourceType = Literal["json", "csv", "xml", "pdf_text", "text"]
ConnectorMapperExecute = Literal["preview", "stage", "send"]


class ConnectorMapperRequest(TypedDict, total=False):
    templateKey: str
    sourceType: ConnectorMapperSourceType
    sourceText: str
    sourceJson: Dict[str, Any]
    customerRef: str
    execute: ConnectorMapperExecute
    confirmed: bool
    fieldMap: Dict[str, Any]
    defaults: Dict[str, Any]


class ConnectorMapperPreviewRequest(TypedDict, total=False):
    """Customer-scoped Mapper preview/normalization input."""

    templateKey: str
    sourceType: ConnectorMapperSourceType
    sourceText: str
    sourceJson: Dict[str, Any]
    customerRef: str
    execute: Literal["preview"]
    fieldMap: Dict[str, Any]
    defaults: Dict[str, Any]


ConnectorReconcileStatus = Literal["exceptions", "all"]
ConnectorReconcileItemType = Literal["autopilot_run", "outbox_item", "inbox_document"]
ConnectorReconcileOwner = Literal["sender_erp", "epostak", "receiver"]


class ConnectorReconcileItem(TypedDict, total=False):
    type: ConnectorReconcileItemType
    id: str
    externalId: Optional[str]
    lifecycleStatus: str
    reason: str
    owner: ConnectorReconcileOwner
    updatedAt: Optional[str]
    repairReport: Optional[Dict[str, Any]]
    lastError: Optional[Dict[str, Any]]
    links: Dict[str, str]


class ConnectorReconcileResponse(TypedDict, total=False):
    status: ConnectorReconcileStatus
    since: Optional[str]
    generatedAt: str
    total: int
    items: List[ConnectorReconcileItem]


ConnectorSendPolicy = Literal["stage_only", "immediate", "scheduled_at", "daily_batch", "paused"]


class ConnectorSendPolicyOptions(TypedDict, total=False):
    policy: ConnectorSendPolicy
    sendAt: Optional[str]


class ConnectorZenInputRequest(TypedDict, total=False):
    customerRef: str
    previewOnly: bool
    mode: ConnectorAutopilotMode
    externalId: Optional[str]
    idempotencyKey: Optional[str]
    invoiceNo: Optional[str]
    invoiceNumber: Optional[str]
    receiverPeppolId: Optional[str]
    receiver: Dict[str, Any]
    buyer: Dict[str, Any]
    customer: Dict[str, Any]
    lines: List[Dict[str, Any]]
    items: List[Dict[str, Any]]
    send: ConnectorSendPolicyOptions


class ConnectorMailboxListResponse(TypedDict, total=False):
    mailboxes: List[Dict[str, Any]]


class ConnectorMailboxRepairRequest(TypedDict, total=False):
    customerRef: str


class ConnectorMailboxUpdateResponse(TypedDict, total=False):
    mailbox: Dict[str, Any]


class ConnectorSyncResponse(TypedDict, total=False):
    items: List[Dict[str, Any]]
    nextCursor: Optional[str]
    hasMore: bool


class ConnectorActionRequest(TypedDict, total=False):
    sendAt: Optional[str]
    status: Optional[str]
    note: Optional[str]


class ConnectorActionResponse(TypedDict, total=False):
    action: Dict[str, Any]


ConnectorOutboxStatus = Literal["ready", "blocked", "scheduled", "sending", "sent", "failed", "cancelled"]


class ConnectorOutboxStageItem(TypedDict, total=False):
    externalId: Optional[str]
    idempotencyKey: Optional[str]
    scheduledFor: Optional[str]
    payload: ConnectorSendRequest


class ConnectorOutboxStageRequest(TypedDict, total=False):
    items: List[ConnectorOutboxStageItem]
    payload: ConnectorSendRequest
    externalId: Optional[str]
    scheduledFor: Optional[str]


class ConnectorOutboxItem(TypedDict, total=False):
    outboxId: str
    externalId: Optional[str]
    status: ConnectorOutboxStatus
    scheduledFor: Optional[str]
    documentId: Optional[str]
    ready: bool
    repairReport: Optional[ConnectorRepairReport]
    safeFixes: List[ConnectorSafeFix]
    lastError: Optional[Dict[str, Any]]
    attemptCount: int
    sentAt: Optional[str]
    cancelledAt: Optional[str]
    createdAt: Optional[str]
    updatedAt: Optional[str]
    links: Dict[str, str]


class ConnectorOutboxStageResponse(TypedDict, total=False):
    total: int
    ready: int
    blocked: int
    staged: int
    items: List[ConnectorOutboxItem]


class ConnectorOutboxListResponse(TypedDict, total=False):
    items: List[ConnectorOutboxItem]
    total: int
    limit: int
    offset: int


class ConnectorOutboxBatchSendResponse(TypedDict, total=False):
    total: int
    sent: int
    failed: int
    skipped: int
    results: List[ConnectorOutboxItem]


BoxStatus = Literal[
    "ready",
    "scheduled",
    "sending",
    "retrying",
    "sent",
    "received",
    "needs_repair",
    "failed",
    "cancelled",
    "expired",
]
BoxDirection = Literal["outbound", "inbound"]


class BoxListParams(TypedDict, total=False):
    status: str
    direction: BoxDirection
    limit: int
    offset: int


class BoxItem(TypedDict, total=False):
    boxItemId: str
    firmId: str
    integratorId: Optional[str]
    direction: str
    status: str
    source: str
    invoiceId: Optional[str]
    documentId: Optional[str]
    peppolMessageId: Optional[str]
    documentTypeId: Optional[str]
    processId: Optional[str]
    scheduledFor: Optional[str]
    nextAttemptAt: Optional[str]
    lockedAt: Optional[str]
    lockedBy: Optional[str]
    attemptCount: int
    payloadSha256: Optional[str]
    storageBytes: int
    retention: Dict[str, Any]
    sentAt: Optional[str]
    receivedAt: Optional[str]
    cancelledAt: Optional[str]
    lastError: Optional[Dict[str, Any]]
    createdAt: Optional[str]
    updatedAt: Optional[str]
    links: Dict[str, str]


class BoxItemDetail(BoxItem, total=False):
    storage: Dict[str, Any]
    timeline: List[Dict[str, Any]]


class BoxListResponse(TypedDict):
    items: List[BoxItem]
    total: int
    limit: int
    offset: int


class BoxCreateRequestOptional(TypedDict, total=False):
    scheduledFor: str
    externalId: str
    metadata: Dict[str, Any]


class BoxCreateRequest(BoxCreateRequestOptional):
    payloadXml: str


class BoxScheduleRequest(TypedDict):
    scheduledFor: str

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
    vatCategoryCode: str  # UBL VAT category code, e.g. "S", "Z", or "AE"
    vatCategory: str  # Alias for vatCategoryCode
    vat_category: str  # Snake_case alias for vatCategoryCode
    taxTreatment: str  # Higher-level tax treatment mapped to vatCategoryCode
    tax_treatment: str  # Snake_case alias for taxTreatment
    deliveryDate: str  # Line delivery date in YYYY-MM-DD format
    lineType: str  # Line type, e.g. "standard" or "advance_deduction"
    advanceInvoiceReference: str  # Advance invoice reference for deduction lines
    customsTariffCode: str  # Customs tariff / combined nomenclature code
    commodityClassificationCode: str  # Generic item classification code
    commodityClassificationListId: str  # Classification list identifier, e.g. "HS"
    reverseChargeParagraphLetter: str  # Domestic reverse-charge paragraph letter
    controlStatementType: str  # Slovak control-statement type, e.g. "IO" or "MT"
    controlStatementQuantity: float  # type: ignore[misc]  # Control-statement quantity
    controlStatementUnit: str  # Control-statement unit, e.g. "kg", "t", "m", or "ks"


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


class DocumentAttachment(TypedDict, total=False):
    """Invoice attachment (BG-24) embedded as base64 into the UBL XML.

    Embedded via ``AdditionalDocumentReference`` / ``EmbeddedDocumentBinaryObject``
    so the receiver sees the file inline with the invoice. MIME type is
    verified by magic-byte sniffing server-side.

    Limits: max 20 files per invoice, 10 MB per file, 15 MB total.
    """

    fileName: str  # Original file name (max 255 chars)
    mimeType: str  # One of: application/pdf, image/png, image/jpeg, text/csv, xlsx, ods
    content: str  # Base64-encoded file content (no data: prefix), max 10 MB decoded
    description: str  # Optional short description (max 100 chars)


class _PrepaymentOptional(TypedDict, total=False):
    """Optional fields on a structured settled prepayment."""

    advanceInvoiceRef: str  # Advance/prepayment invoice reference from the ERP
    taxDocumentRef: str  # Tax document number for the received advance payment
    settlementDate: str  # Settlement date in YYYY-MM-DD format
    amountWithoutVat: float  # type: ignore[misc]  # Settled amount without VAT
    vatAmount: float  # type: ignore[misc]  # VAT amount from the settled prepayment
    vatRate: float  # type: ignore[misc]  # VAT rate of the prepayment, when known
    vatCategoryCode: str  # Optional VAT category of the prepayment, e.g. "S" or "AE"


class Prepayment(_PrepaymentOptional):
    """Structured settled prepayment for final-invoice JSON mode."""

    amountWithVat: float  # type: ignore[misc]  # Settled amount including VAT


class _SendDocumentBase(TypedDict, total=False):
    """Optional structured JSON send fields."""

    invoiceNumber: str  # Invoice number, e.g. "FV-2026-001"
    issueDate: str  # Issue date in ISO 8601 / YYYY-MM-DD
    dueDate: str  # Payment due date in ISO 8601 / YYYY-MM-DD
    currency: str  # ISO 4217 currency code, default "EUR"
    note: str  # Free-text note included in the document
    iban: str  # Seller bank account IBAN for payment
    paymentMethod: str  # Payment method code, e.g. "30" (credit transfer)
    variableSymbol: str  # Slovak variable symbol for bank payments (max 10 digits)
    buyerReference: str  # Buyer's reference / purchase order number
    receiverIco: str  # Receiver ICO, 8 digits
    receiverDic: str  # Receiver tax ID (DIC)
    receiverIcDph: str  # Receiver VAT ID (IC DPH)
    receiverAddress: str  # Receiver street address
    receiverStreet: str  # Receiver street and number
    receiverCity: str  # Receiver city
    receiverPostalCode: str  # Receiver postal code
    receiverCountry: str  # Receiver ISO country code, e.g. "SK"
    prepaidAmount: float  # type: ignore[misc]  # Amount paid in advance
    prepayments: List[Prepayment]  # Structured settled prepayments for final invoices
    attachments: List[DocumentAttachment]  # JSON mode only -- embedded as BG-24 in UBL


class SendDocumentJsonRequest(_SendDocumentBase):
    """Structured JSON send request. The API generates UBL XML."""

    receiverPeppolId: str  # type: ignore[misc]  # Peppol ID of the receiver
    receiverName: str  # Receiver company name required by the live JSON schema
    items: List[LineItem]  # Line items (JSON mode) -- mutually exclusive with ``xml``


class SendDocumentXmlRequest(TypedDict):
    """Raw UBL XML send request."""

    receiverPeppolId: str  # type: ignore[misc]  # Peppol ID of the receiver
    xml: str  # Pre-built UBL XML string (XML mode) -- mutually exclusive with ``items``


SendDocumentRequest = Union[SendDocumentJsonRequest, SendDocumentXmlRequest]
"""Request body for ``documents.send()``: structured JSON mode or raw XML mode."""


class SendDocumentResponse(TypedDict, total=False):
    """Successful response from sending a document (HTTP 201)."""

    documentId: str  # type: ignore[misc]  # UUID of the created document
    messageId: str  # type: ignore[misc]  # Peppol AS4 message ID
    status: str  # type: ignore[misc]  # Initial status, typically "SENT"
    payload_sha256: str  # Hex SHA-256 over the canonical UBL XML wire payload


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


class DocumentStatusBatchResult(DocumentStatusResponse, total=False):
    """Single result from ``POST /documents/status/batch``."""

    error: Literal["not_found"]  # Returned for missing/cross-tenant IDs
    direction: str  # "inbound" or "outbound" when found
    peppolMessageId: Optional[str]  # Alias for the AS4 message ID


class DocumentStatusBatchResponse(TypedDict):
    """Response from ``POST /documents/status/batch``."""

    total: int
    found: int
    notFound: int
    results: List[DocumentStatusBatchResult]


# ---------------------------------------------------------------------------
# Document lifecycle - evidence
# ---------------------------------------------------------------------------


class _InvoiceResponseEvidence(TypedDict, total=False):
    """Invoice response evidence embedded in delivery evidence."""

    status: Optional[str]  # Response status (one of :data:`InvoiceResponseCode`)
    document: Dict[str, Any]  # type: ignore[misc]  # Raw invoice response document


class _TddEvidence(TypedDict, total=False):
    """SK Tax Data Document (TDD) reporting evidence.

    Present only for documents that have been reported to FS SR.
    """

    reportedAt: str  # type: ignore[misc]  # ISO 8601 timestamp of the TDD report
    reported: bool  # type: ignore[misc]  # True if FS SR acknowledged the TDD submission


class DocumentEvidenceResponse(TypedDict, total=False):
    """Delivery evidence for a sent document (AS4 receipts, MLR, invoice response, TDD)."""

    documentId: str  # type: ignore[misc]  # Document UUID
    as4Receipt: Optional[Dict[str, Any]]  # AS4 receipt from the access point
    mlrDocument: Optional[Dict[str, Any]]  # Message Level Response document
    invoiceResponse: Optional[_InvoiceResponseEvidence]  # Invoice response from the receiver
    tdd: Optional[_TddEvidence]  # SK Tax Data Document reporting evidence, if applicable
    deliveredAt: Optional[str]  # ISO 8601 timestamp of delivery confirmation
    sentAt: Optional[str]  # ISO 8601 timestamp when the document was sent


# ---------------------------------------------------------------------------
# Invoice response (respond to received document)
# ---------------------------------------------------------------------------


class InvoiceRespondResponse(TypedDict, total=False):
    """Response after sending an invoice response (accept/reject/query/etc.).

    The server dispatches the invoice response via Peppol AS4 synchronously.
    On dispatch success the call returns 200; on dispatch failure the
    response is still persisted and the call returns 202 with
    ``dispatchStatus="failed_queued"`` plus ``dispatchError``.
    """

    documentId: str  # type: ignore[misc]  # UUID of the document responded to
    responseStatus: str  # type: ignore[misc]  # The response code sent (one of :data:`InvoiceResponseCode`)
    respondedAt: str  # type: ignore[misc]  # ISO 8601 timestamp of the response
    peppolMessageId: Optional[str]  # type: ignore[misc]  # Peppol AS4 message ID of the outbound response, None on dispatch failure
    dispatchStatus: str  # type: ignore[misc]  # "sent" on success, "failed_queued" when AS4 dispatch errored
    dispatchError: Optional[str]  # Dispatch error message, present only when dispatchStatus is "failed_queued"


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


class ConvertRequest(TypedDict):
    """Request body for ``POST /documents/convert``."""

    input_format: str  # "json" or "ubl" -- format of the supplied ``document``
    output_format: str  # "ubl" or "json" -- desired output format
    document: Union[str, Dict[str, Any]]  # UBL XML string (input_format="ubl") or JSON dict (input_format="json")


class ConvertResult(TypedDict, total=False):
    """Result of a JSON-to-UBL or UBL-to-JSON conversion."""

    output_format: str  # type: ignore[misc]  # "ubl" or "json" -- format of ``document``
    document: Union[str, Dict[str, Any]]  # type: ignore[misc]  # UBL XML string (output_format="ubl") or parsed JSON dict (output_format="json")
    warnings: List[str]  # Non-fatal warnings emitted during conversion


# ---------------------------------------------------------------------------
# Peppol
# ---------------------------------------------------------------------------


class SmpParticipantCapability(TypedDict):
    """A single document type capability advertised via SMP."""

    documentTypeId: str  # UBL document type identifier
    processId: str  # Peppol process identifier
    transportProfile: str  # Transport profile, e.g. "peppol-transport-as4-v2_0"


PeppolRoutingStatus = Literal[
    "ready",
    "participant_not_found",
    "document_type_not_supported",
    "process_not_supported",
    "endpoint_not_found",
    "certificate_invalid",
    "certificate_expired",
    "lookup_failed",
]


class PeppolAccessPoint(TypedDict, total=False):
    """Peppol AS4 access point metadata returned by SMP lookup."""

    url: str
    transportProfile: Optional[str]


class PeppolCertificateInfo(TypedDict, total=False):
    """Certificate metadata published by the participant's SMP endpoint."""

    present: bool
    fingerprintSha256: str
    subject: str
    issuer: str
    serialNumber: str
    notBefore: str
    notAfter: str
    serviceActivationDate: str
    serviceExpirationDate: str
    valid: bool
    error: Literal["malformed", "invalid_x509"]


class PeppolParticipant(TypedDict, total=False):
    """Peppol participant invoice capability returned by SMP lookup."""

    found: bool  # type: ignore[misc]  # True if the participant exists in Peppol SMP/SML
    accepts: bool  # type: ignore[misc]  # True if the default BIS Billing invoice capability is routable
    routingStatus: PeppolRoutingStatus  # Machine-readable capability status
    participantId: str  # type: ignore[misc]  # Full Peppol participant ID (scheme:id)
    scheme: str  # type: ignore[misc]  # Peppol identifier scheme, e.g. "0245"
    identifier: str  # type: ignore[misc]  # Identifier value inside the scheme
    accessPoint: Optional[PeppolAccessPoint]
    certificate: Optional[PeppolCertificateInfo]
    supportedDocumentTypes: List[str]  # type: ignore[misc]  # Supported document type IDs
    source: Optional[str]
    temporaryFailure: bool


class DirectoryEntry(TypedDict, total=False):
    """A single entry from the Peppol Business Card directory."""

    participantId: str  # type: ignore[misc]  # Peppol participant ID, e.g. "0245:12345678"
    name: str  # type: ignore[misc]  # Registered business name
    countryCode: str  # type: ignore[misc]  # ISO country code
    registrationDate: Optional[str]  # Registration date (YYYY-MM-DD), None if unknown


class DirectorySearchResult(TypedDict):
    """Paginated result from a Peppol directory search.

    Pagination uses a cursor-style ``has_next`` flag instead of a total count
    because the underlying directory has ~3.6M entries and counting hits is
    expensive. Use ``page`` + ``page_size`` to advance.
    """

    items: List[DirectoryEntry]  # Matching directory entries on this page
    page: int  # Current page number (1-based)
    page_size: int  # Requested page size
    has_next: bool  # True if another page of results is available


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

    scheme: str  # Peppol scheme. SK: "0245" only (DIČ). "9950" is not supported per PASR.
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


class CreateWebhookRequest(TypedDict, total=False):
    """Request body for creating a new webhook subscription.

    Since v0.10.0 ``url`` is optional: omit it (or pass ``None``) to create a
    pull-only subscription whose events land in the queue read via
    ``webhooks.queue.pull()``.  Pass an HTTPS URL to receive POST deliveries.
    Register two subscriptions if you want both push and pull.
    """

    url: Optional[str]  # HTTPS URL where payloads are POSTed, or None for pull-only
    events: List[WebhookEvent]  # Event types to subscribe to (defaults to all if omitted)
    isActive: bool  # Whether the webhook is active (defaults to True)


class UpdateWebhookRequest(TypedDict, total=False):
    """Request body for updating an existing webhook subscription.

    Omit fields to leave them unchanged.  Set ``url=None`` to switch an
    existing push subscription to pull-only.
    """

    url: Optional[str]  # New HTTPS URL, or None to switch to pull-only
    events: List[WebhookEvent]  # Updated list of event types to subscribe to
    isActive: bool  # Set to False to pause without deleting


class Webhook(TypedDict, total=False):
    """Webhook subscription (list view, no secret)."""

    id: str  # type: ignore[misc]  # Webhook UUID
    url: Optional[str]  # type: ignore[misc]  # Delivery URL, or None for pull-only subscriptions
    events: List[str]  # type: ignore[misc]  # Subscribed event types
    isActive: bool  # type: ignore[misc]  # Whether the webhook is enabled
    failedAttempts: int  # type: ignore[misc]  # Consecutive delivery failures (for auto-disable)
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp


class WebhookDetail(TypedDict, total=False):
    """Webhook subscription detail returned on creation (includes secret).

    The ``secret`` is only returned once at creation time — store it securely.
    """

    id: str  # type: ignore[misc]  # Webhook UUID
    url: Optional[str]  # type: ignore[misc]  # Delivery URL, or None for pull-only subscriptions
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
    idempotency_key: Optional[str]  # Idempotency key for the delivery, if set


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

    event_id: str  # Event UUID (use this to acknowledge)
    firm_id: str  # UUID of the firm the event belongs to
    event: str  # Event type, e.g. "document.received"
    created_at: str  # ISO 8601 timestamp when the event was created
    payload: WebhookPayload  # Full v1 webhook envelope


class WebhookQueueResponse(TypedDict):
    """Response from pulling the webhook event queue."""

    items: List[WebhookQueueItem]  # Events on this page
    has_more: bool  # True if there are more events still in the queue


# ---------------------------------------------------------------------------
# Webhook queue all (integrator - cross-firm)
# ---------------------------------------------------------------------------


class WebhookQueueAllEvent(TypedDict):
    """A single event from the cross-firm webhook queue (integrator only)."""

    event_id: str  # Event UUID (use this to acknowledge)
    firm_id: str  # UUID of the firm the event belongs to
    event: str  # Event type, e.g. "document.received"
    payload: WebhookPayload  # Full v1 webhook envelope
    created_at: str  # ISO 8601 timestamp when the event was created


class WebhookQueueAllResponse(TypedDict):
    """Response from pulling events across all firms (integrator only)."""

    items: List[WebhookQueueAllEvent]  # Cross-firm events
    has_more: bool  # True if there are more events still in the queue


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------


ReportingPeriod = Literal["month", "quarter", "year"]
"""Convenience period selector for ``client.reporting.statistics``."""


class StatisticsParams(TypedDict, total=False):
    """Optional parameters for ``client.reporting.statistics``."""

    from_: str  # Start date (ISO 8601 / YYYY-MM-DD), inclusive
    to: str  # End date (ISO 8601 / YYYY-MM-DD), inclusive
    period: ReportingPeriod  # Convenience window selector


class _StatsByType(TypedDict, total=False):
    """Per-document-type counts. Keys are document type slugs (e.g.
    ``"invoice"``, ``"credit_note"``)."""

    invoice: int
    credit_note: int
    correction: int
    self_billing: int
    reverse_charge: int
    self_billing_credit_note: int


class _StatsSent(TypedDict):
    """Sent (outbound) document statistics."""

    total: int  # Total sent documents in the period
    by_type: Dict[str, int]  # Per-document-type counts


class _StatsReceived(TypedDict):
    """Received (inbound) document statistics."""

    total: int  # Total received documents in the period
    by_type: Dict[str, int]  # Per-document-type counts


class StatisticsTopParty(TypedDict):
    """Top counterparty entry returned by ``stats.top_recipients`` /
    ``stats.top_senders``."""

    peppol_id: str  # Peppol participant identifier
    name: Optional[str]  # Resolved party name (None if unknown)
    count: int  # Documents exchanged in the period


class Statistics(TypedDict):
    """Aggregated document statistics for a date range."""

    period: Dict[str, str]  # Resolved {"from", "to"} date range
    sent: _StatsSent  # Sent (outbound) totals + per-type breakdown
    received: _StatsReceived  # Received (inbound) totals + per-type breakdown
    delivery_rate: float  # 0.0–1.0 fraction delivered for sent docs
    top_recipients: List[StatisticsTopParty]  # Up to 5 top recipients (sent)
    top_senders: List[StatisticsTopParty]  # Up to 5 top senders (received)


class ReportingSubmissionsParams(TypedDict, total=False):
    """Optional parameters for ``client.reporting.submissions``."""

    limit: int
    offset: int
    report_type: str  # EUSR | TSR


class ReportingSubmission(TypedDict):
    """One FS SR report submission record."""

    id: str
    report_type: str
    period: Dict[str, str]
    status: str
    message_id: Optional[str]
    submitted_at: Optional[str]
    has_error: bool


class ReportingSubmissionsResponse(TypedDict):
    """Paginated FS SR report submission history."""

    items: List[ReportingSubmission]
    total: int
    limit: int
    offset: int


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
    ocr_extractions: int  # Number of OCR extractions in the current calendar month


class _AccountLimits(TypedDict):
    """Plan limits for the current billing period.

    ``-1`` means unlimited on a given dimension.
    """

    documents_per_month: int  # Documents-per-month cap; -1 = unlimited
    ocr_per_month: int  # OCR extractions-per-month cap; -1 = unlimited


class Account(TypedDict):
    """Full account information including firm, plan, usage, and plan limits."""

    firm: _AccountFirm  # Firm details for the authenticated API key
    plan: _AccountPlan  # Current subscription plan
    usage: _AccountUsage  # Document usage in the current billing period
    limits: _AccountLimits  # Plan caps for the current billing period


# ---------------------------------------------------------------------------
# Auth status (key introspection)
# ---------------------------------------------------------------------------


class AuthStatusKey(TypedDict, total=False):
    """API key metadata returned by ``GET /auth/status``."""

    id: str  # type: ignore[misc]  # API key UUID
    name: Optional[str]  # Human-readable label for the key
    prefix: str  # type: ignore[misc]  # Visible key prefix (e.g. "sk_live_abcd")
    permissions: List[str]  # type: ignore[misc]  # Permission scopes granted to the key
    active: bool  # type: ignore[misc]  # True if the key is enabled
    createdAt: str  # type: ignore[misc]  # ISO 8601 creation timestamp
    lastUsedAt: Optional[str]  # ISO 8601 timestamp of last use, or None


class AuthStatusFirm(TypedDict):
    """Firm info in the status response.

    Only ``id`` and ``peppolStatus`` are returned here — call
    :meth:`~epostak.resources.account.AccountResource.get` for the full
    firm profile.
    """

    id: str  # Firm UUID
    peppolStatus: str  # Peppol registration status, e.g. "ACTIVE"


class AuthStatusPlan(TypedDict):
    """Subscription plan info embedded in the status response."""

    name: str  # Plan slug, e.g. "api-enterprise", "free"
    expiresAt: Optional[str]  # ISO 8601 plan expiry timestamp, None for non-expiring plans
    active: bool  # True if the plan is currently active (non-free and not expired)


class AuthStatusRateLimit(TypedDict):
    """Rate-limit thresholds applicable to the current key."""

    perMinute: int  # Allowed requests per minute (typically 200)
    window: str  # Window size as a human-readable string, e.g. "60s"


class AuthStatusIntegrator(TypedDict):
    """Integrator info present only when the authenticated key is an integrator key."""

    id: str  # Integrator UUID


class AuthStatusResponse(TypedDict):
    """Authentication introspection for the current API key.

    Mirrors ``GET /auth/status``. Integrators see ``integrator`` populated;
    direct (single-firm) keys have ``integrator: None``.
    """

    key: AuthStatusKey  # Current API key metadata
    firm: AuthStatusFirm  # Resolved firm for this request
    plan: AuthStatusPlan  # Current subscription plan
    rateLimit: AuthStatusRateLimit  # Rate-limit configuration
    integrator: Optional[AuthStatusIntegrator]  # Integrator info, None for direct keys


# Back-compat aliases (v1 names) — keep code that still types against the
# old names compiling. These alias to the v2 shapes verbatim.
AccountStatus = AuthStatusResponse
_AccountStatusKey = AuthStatusKey
_AccountStatusFirm = AuthStatusFirm
_AccountStatusPlan = AuthStatusPlan
_AccountStatusRateLimit = AuthStatusRateLimit
_AccountStatusIntegrator = AuthStatusIntegrator


# ---------------------------------------------------------------------------
# Rotate secret
# ---------------------------------------------------------------------------


class RotateSecretResponse(TypedDict):
    """Response returned after rotating an API key secret.

    The new plaintext ``key`` is shown exactly once -- store it securely.
    The old key is deactivated atomically before the new one is created.
    Rejected with HTTP 403 for integrator keys (``sk_int_*``).
    """

    key: str  # New plaintext API key -- shown ONCE, store securely
    prefix: str  # Visible prefix of the new key, safe to store/log
    message: str  # Human-readable confirmation message


# ---------------------------------------------------------------------------
# Batch send
# ---------------------------------------------------------------------------


class BatchSendResultItem(TypedDict, total=False):
    """Result for a single item in a batch send request."""

    index: int  # type: ignore[misc]  # Zero-based position of the item in the request
    status: int  # type: ignore[misc]  # HTTP status code (201 on success, 4xx/5xx on failure)
    result: Dict[str, Any]  # type: ignore[misc]  # Per-item response body (send response on success, error object on failure)


class BatchSendResponse(TypedDict):
    """Response from ``documents.send_batch()``."""

    total: int  # Total items submitted
    succeeded: int  # Items that were accepted and queued for send
    failed: int  # Items rejected during validation
    results: List[BatchSendResultItem]  # Per-item results in submission order


# ---------------------------------------------------------------------------
# Mark document state
# ---------------------------------------------------------------------------

DocumentMarkState = Literal["delivered", "processed", "failed", "read"]
"""States a document may be manually marked as via ``documents.mark()``."""


class DocumentMarkResponse(TypedDict, total=False):
    """Response from ``documents.mark()``."""

    id: str  # type: ignore[misc]  # Document UUID
    state: str  # type: ignore[misc]  # Applied state (one of :data:`DocumentMarkState`)
    status: str  # type: ignore[misc]  # Resulting document status
    deliveredAt: Optional[str]  # ISO 8601 timestamp if marked delivered
    acknowledgedAt: Optional[str]  # ISO 8601 timestamp of the mark action
    readAt: Optional[str]  # ISO 8601 read timestamp, if applicable


# ---------------------------------------------------------------------------
# Peppol capabilities + batch lookup
# ---------------------------------------------------------------------------


class PeppolCapabilitiesResult(TypedDict, total=False):
    """Response from ``peppol.capabilities()``."""

    found: bool  # type: ignore[misc]  # True if the participant is registered on SMP
    accepts: bool  # type: ignore[misc]  # True if the participant accepts the requested document type
    participant: "PeppolParticipantRef"
    accessPoint: Optional[PeppolAccessPoint]
    supportedDocumentTypes: List[str]  # type: ignore[misc]  # All advertised UBL document type IDs
    matchedDocumentType: Optional[str]  # The matched document type ID, if any
    source: Optional[str]
    certificate: Optional[PeppolCertificateInfo]
    capability: Dict[str, Any]


class PeppolParticipantRef(TypedDict):
    """Participant reference used in batch lookup requests."""

    scheme: str  # Peppol scheme (e.g. "0245")
    identifier: str  # Identifier value


class PeppolLookupBatchItem(TypedDict, total=False):
    """Per-participant result in a batch lookup response."""

    index: int  # type: ignore[misc]  # Zero-based position in the original request
    participant: "PeppolParticipantRef"  # Echoed participant with optional id on valid input
    found: bool  # type: ignore[misc]  # True if the participant was found on SMP
    accepts: bool  # type: ignore[misc]  # True if the default invoice capability is routable
    routingStatus: PeppolRoutingStatus
    accessPoint: Optional[PeppolAccessPoint]
    certificate: Optional[PeppolCertificateInfo]
    internal: bool
    supportedDocumentTypes: List[str]  # type: ignore[misc]
    source: Optional[str]
    temporaryFailure: bool
    error: Optional[str]  # Error message if the lookup failed


class PeppolLookupBatchResponse(TypedDict):
    """Response from ``peppol.lookup_batch()``."""

    total: int  # Number of participants queried
    found: int  # Number of participants registered on SMP
    notFound: int  # Number of participants not found or errored
    results: List[PeppolLookupBatchItem]  # Per-participant results in request order


# ---------------------------------------------------------------------------
# Outbox
# ---------------------------------------------------------------------------


class OutboxListResponse(TypedDict):
    """Paginated list of outbox (sent) documents."""

    documents: List[Document]  # Outbound documents on the current page
    total: int  # Total number of matching documents
    offset: int  # Current pagination offset
    limit: int  # Requested page size


# ---------------------------------------------------------------------------
# Invoice responses list
# ---------------------------------------------------------------------------


class InvoiceResponseItem(TypedDict, total=False):
    """A single Invoice Response record for a document."""

    id: str  # type: ignore[misc]  # Response UUID
    responseCode: str  # type: ignore[misc]  # Response status code (one of InvoiceResponseCode)
    note: Optional[str]  # Optional note accompanying the response
    senderPeppolId: str  # type: ignore[misc]  # Peppol participant ID of the sender
    createdAt: str  # type: ignore[misc]  # ISO 8601 timestamp when the response was created


class InvoiceResponsesListResponse(TypedDict):
    """Response from GET /documents/{id}/responses."""

    documentId: str  # Document UUID the responses belong to
    responses: List[InvoiceResponseItem]  # Array of Invoice Response records


# ---------------------------------------------------------------------------
# Document events
# ---------------------------------------------------------------------------


class DocumentEvent(TypedDict, total=False):
    """A single event in a document's audit trail."""

    id: str  # type: ignore[misc]  # Event UUID
    eventType: str  # type: ignore[misc]  # Event type identifier, e.g. "status_changed"
    actor: str  # type: ignore[misc]  # Actor that triggered the event
    detail: Optional[str]  # Human-readable detail about the event
    meta: Dict[str, Any]  # type: ignore[misc]  # Structured metadata attached to the event
    occurredAt: str  # type: ignore[misc]  # ISO 8601 timestamp when the event occurred


class DocumentEventsResponse(TypedDict):
    """Response from GET /documents/{id}/events."""

    documentId: str  # Document UUID the events belong to
    events: List[DocumentEvent]  # Ordered array of events
    nextCursor: Optional[str]  # Cursor for next page, or None when no more pages


# ---------------------------------------------------------------------------
# Extract
# ---------------------------------------------------------------------------


class ExtractMissingField(TypedDict, total=False):
    """Field the integrator must review or complete before sending."""

    field: str
    label: str
    message: str
    blocking: bool
    value: Any


class ExtractFieldSource(TypedDict, total=False):
    """Provenance for a value returned by OCR or enrichment."""

    source: str
    value: Any
    confidence: float


class ExtractNextAction(TypedDict, total=False):
    """Recommended next action after OCR extraction."""

    type: str
    label: str
    message: str
    endpoint: str
    method: str


class ExtractResult(TypedDict, total=False):
    """Result of AI-powered extraction from a single file."""

    extraction: Dict[str, Any]  # type: ignore[misc]  # Structured invoice data extracted by AI
    document_type: str  # type: ignore[misc]  # Resolved document type, e.g. invoice or self_billing
    direction: str  # type: ignore[misc]  # "inbound" returns UBL; "outbound" returns a review send payload when supported
    send_payload: Optional[Dict[str, Any]]  # type: ignore[misc]  # Draft body for POST /documents/send
    send_payload_missing_fields: List[str]  # type: ignore[misc]  # Fields required before posting send_payload
    send_ready: bool  # type: ignore[misc]  # True when blocking send fields are present
    ubl_xml: str  # type: ignore[misc]  # Generated UBL XML from the extraction
    confidence: Union[str, float]  # type: ignore[misc]  # Confidence bucket returned by API; float kept for older callers
    confidence_scores: Dict[str, float]  # type: ignore[misc]  # Per-field confidence scores
    needs_review: bool  # type: ignore[misc]  # Human review recommended
    missing_fields: List[ExtractMissingField]  # type: ignore[misc]  # Review checklist
    field_sources: Dict[str, ExtractFieldSource]  # type: ignore[misc]  # Field provenance map
    next_action: ExtractNextAction  # type: ignore[misc]  # Recommended next API action
    file_name: str  # type: ignore[misc]  # Name of the processed file


class BatchExtractItem(TypedDict, total=False):
    """Result for a single file within a batch extraction."""

    file_name: str  # type: ignore[misc]  # Name of the processed file
    document_type: str  # Resolved document type
    direction: str  # "inbound" or "outbound"
    send_payload: Optional[Dict[str, Any]]  # Draft body for POST /documents/send
    send_payload_missing_fields: List[str]  # Fields required before posting send_payload
    send_ready: bool  # True when blocking send fields are present
    extraction: Dict[str, Any]  # Structured invoice data (present on success)
    ubl_xml: str  # Generated UBL XML (present on success)
    confidence: str  # AI confidence score as string (present on success)
    confidence_scores: Dict[str, float]  # Per-field confidence scores
    needs_review: bool  # Human review recommended
    missing_fields: List[ExtractMissingField]  # Review checklist
    field_sources: Dict[str, ExtractFieldSource]  # Field provenance map
    next_action: ExtractNextAction  # Recommended next API action
    error: str  # Error message (present on failure)


class BatchExtractResult(TypedDict):
    """Result of batch AI extraction across multiple files."""

    batch_id: str  # Unique batch processing ID
    total: int  # Total number of files submitted
    successful: int  # Number of successfully extracted files
    failed: int  # Number of files that failed extraction
    results: List[BatchExtractItem]  # Per-file results


# ---------------------------------------------------------------------------
# Auth (OAuth client_credentials flow + IP allowlist) — v2.0
# ---------------------------------------------------------------------------


class TokenResponse(TypedDict, total=False):
    """OAuth access + refresh token pair returned by ``client.auth.token`` /
    ``client.auth.renew``.

    Mirrors RFC 6749 ``client_credentials`` / ``refresh_token`` grant
    responses. The access token is a JWT valid for ``expires_in`` seconds
    (typically 900). The refresh token is a 30-day rotating string —
    every successful renew invalidates the previous refresh token, so
    always overwrite stored state with the new value.
    """

    access_token: str  # JWT access token
    refresh_token: str  # 30-day rotating refresh token
    token_type: str  # "Bearer"
    expires_in: int  # Access-token lifetime in seconds (typically 900)
    scope: str  # Granted scopes (space-separated)


class RevokeResponse(TypedDict, total=False):
    """Response from ``client.auth.revoke``. Idempotent — returned even
    when the token was unknown or already revoked."""

    revoked: bool  # Always True


class IpAllowlistResponse(TypedDict):
    """Response from ``client.auth.ip_allowlist.get`` /
    ``client.auth.ip_allowlist.update``."""

    ip_allowlist: List[str]  # Active CIDR / IP entries (empty = no restriction)


# ---------------------------------------------------------------------------
# Audit feed (Wave 3.4) — v2.0
# ---------------------------------------------------------------------------


AuditActorType = Literal["user", "apiKey", "integratorKey", "system"]
"""Type of principal that triggered an audit event."""


class AuditEvent(TypedDict, total=False):
    """A single audit-feed row."""

    id: str  # type: ignore[misc]  # Audit row UUID
    occurred_at: str  # type: ignore[misc]  # ISO-8601 timestamp
    event: str  # type: ignore[misc]  # Event slug, e.g. "jwt.issued", "key.rotated"
    actor_type: AuditActorType  # type: ignore[misc]  # Principal class
    actor_id: Optional[str]  # Principal UUID (None for system events)
    firm_id: Optional[str]  # Tenant UUID
    ip: Optional[str]  # Source IP address
    user_agent: Optional[str]  # Source user-agent
    metadata: Dict[str, Any]  # Free-form event metadata


class AuditListParams(TypedDict, total=False):
    """Optional parameters for ``client.audit.list``."""

    event: str  # Exact-match event slug
    actor_type: AuditActorType  # Restrict to a principal class
    since: str  # ISO-8601 lower bound (inclusive)
    until: str  # ISO-8601 upper bound (exclusive)
    cursor: str  # Opaque cursor from a previous page's next_cursor
    limit: int  # 1–100, default 20


# ---------------------------------------------------------------------------
# Cursor pagination — v2.0
# ---------------------------------------------------------------------------


class CursorPage(TypedDict):
    """One page of a cursor-paginated response.

    ``next_cursor`` is ``None`` when the feed is exhausted; otherwise
    pass it back as the ``cursor`` parameter of the next call to fetch
    the following page.
    """

    items: List[Any]  # Rows in this page
    next_cursor: Optional[str]  # Opaque cursor or None when finished


# ---------------------------------------------------------------------------
# Pull API — inbound documents (v0.9.0)
# ---------------------------------------------------------------------------


class PullInboundDocument(TypedDict, total=False):
    """A received (inbound) document as returned by the Pull API."""

    id: str  # type: ignore[misc]  # Document UUID
    kind: str  # type: ignore[misc]  # Document kind, e.g. "invoice", "credit_note"
    status: str  # type: ignore[misc]  # Current status, e.g. "RECEIVED", "ACKNOWLEDGED"
    sender_peppol_id: Optional[str]  # Sender Peppol participant ID
    receiver_peppol_id: Optional[str]  # Receiver Peppol participant ID
    peppol_message_id: Optional[str]  # Peppol AS4 message ID
    client_acked_at: Optional[str]  # ISO 8601 timestamp of last client ack, or None
    client_reference: Optional[str]  # Client reference set on ack, or None
    received_at: str  # type: ignore[misc]  # ISO 8601 timestamp when the document was received
    created_at: str  # type: ignore[misc]  # ISO 8601 creation timestamp


class PullInboundListResponse(TypedDict, total=False):
    """Cursor-paginated list of received documents."""

    documents: List[PullInboundDocument]  # type: ignore[misc]
    next_cursor: Optional[str]  # Opaque cursor for the next page, None when exhausted
    has_more: bool  # type: ignore[misc]  # True if more documents exist past this page


# PullInboundAckResponse is the full document shape post-ack
PullInboundAckResponse = PullInboundDocument


# ---------------------------------------------------------------------------
# Pull API — outbound documents (v0.9.0)
# ---------------------------------------------------------------------------


class _OutboundAttempt(TypedDict, total=False):
    """Single delivery attempt in the outbound document detail."""

    attempted_at: str  # type: ignore[misc]
    result: str  # type: ignore[misc]
    error: Optional[str]


class PullOutboundDocument(TypedDict, total=False):
    """A sent (outbound) document as returned by the Pull API."""

    id: str  # type: ignore[misc]
    kind: str  # type: ignore[misc]
    status: str  # type: ignore[misc]
    business_status: Optional[str]
    sender_peppol_id: Optional[str]
    receiver_peppol_id: Optional[str]
    peppol_message_id: Optional[str]
    attempt_history: List[_OutboundAttempt]  # Only present in detail (get), not in list
    created_at: str  # type: ignore[misc]
    updated_at: str  # type: ignore[misc]


class PullOutboundListResponse(TypedDict, total=False):
    """Cursor-paginated list of sent documents."""

    documents: List[PullOutboundDocument]  # type: ignore[misc]
    next_cursor: Optional[str]
    has_more: bool  # type: ignore[misc]


class _OutboundEvent(TypedDict, total=False):
    """A single delivery event for an outbound document."""

    id: str  # type: ignore[misc]
    document_id: str  # type: ignore[misc]
    type: str  # type: ignore[misc]
    actor: Optional[str]
    detail: Optional[str]
    meta: Dict[str, Any]  # type: ignore[misc]
    occurred_at: str  # type: ignore[misc]


class PullOutboundEventsResponse(TypedDict, total=False):
    """Cursor-paginated delivery event stream."""

    events: List[_OutboundEvent]  # type: ignore[misc]
    next_cursor: Optional[str]
    has_more: bool  # type: ignore[misc]


# ---------------------------------------------------------------------------
# Rate-limit info (v0.9.0)
# ---------------------------------------------------------------------------


class RateLimitInfo(TypedDict):
    """Parsed X-RateLimit-* headers from the last API response."""

    limit: int  # Total requests allowed in the window
    remaining: int  # Requests remaining in the current window
    reset_at: Any  # datetime of the window reset (datetime.datetime object)


# ---------------------------------------------------------------------------
# Integrator billing aggregate -- v2.2
# ---------------------------------------------------------------------------


class IntegratorPricingTier(TypedDict, total=False):
    """One tier in the integrator pricing table.

    Last entry has ``upTo`` and ``rate`` set to ``None`` (open-ended) and
    ``contactRequired`` set to ``True`` -- volumes above ``contactThreshold``
    require a manual sales contract.
    """

    upTo: Optional[int]  # Inclusive upper bound, ``None`` for the open tier
    rate: Optional[float]  # Per-doc rate in EUR, ``None`` for the open tier
    label: str  # Human-readable label (e.g. "Individuálne")
    contactRequired: bool  # ``True`` on the open tier


class IntegratorBillableUsage(TypedDict):
    """Aggregate over firms on the ``integrator-managed`` plan."""

    managedFirms: int
    outboundCount: int
    inboundApiCount: int
    outboundCharge: float  # Tier rates applied to AGGREGATE outboundCount
    inboundApiCharge: float  # Tier rates applied to AGGREGATE inboundApiCount
    totalCharge: float  # outboundCharge + inboundApiCharge, cents-rounded
    currency: str  # "EUR"


class IntegratorNonManagedUsage(TypedDict):
    """Linked firms that pay their own plan (not billed to integrator)."""

    firms: int
    outboundCount: int
    inboundApiCount: int


class IntegratorFirmUsage(TypedDict):
    """Per-firm row in the ``firms`` page."""

    firmId: str
    name: Optional[str]
    ico: Optional[str]
    managed: bool  # ``True`` -> counts in billable; ``False`` -> nonManaged
    outboundCount: int
    inboundApiCount: int


class _IntegratorInfo(TypedDict):
    id: str
    name: str
    plan: str
    monthlyDocumentLimit: Optional[int]


class _IntegratorPricing(TypedDict):
    model: str  # "tiered"
    currency: str  # "EUR"
    outboundTiers: List[IntegratorPricingTier]
    inboundApiTiers: List[IntegratorPricingTier]


class _IntegratorPagination(TypedDict):
    limit: int
    offset: int
    total: int


class IntegratorKey(TypedDict):
    id: str
    keyPrefix: str
    name: Optional[str]
    scopes: List[str]
    ipAllowlist: List[str]
    isActive: bool
    lastUsedAt: Optional[str]
    createdAt: str


class IntegratorKeysResponse(TypedDict):
    keys: List[IntegratorKey]


class DeactivateIntegratorKeyResponse(TypedDict):
    success: bool
    message: str


class IntegratorLicenseInfo(TypedDict):
    """Response shape of ``GET /api/v1/integrator/licenses/info``.

    Tier rates are applied to the AGGREGATE count across all the integrator's
    ``integrator-managed`` firms. Above ``contactThreshold`` (5 000)
    ``exceedsAutoTier`` flips ``True`` -- auto-billing pauses, sales
    handles invoicing manually.
    """

    integrator: _IntegratorInfo
    period: str  # "YYYY-MM" (SK timezone)
    nextResetAt: str  # ISO 8601 -- 1st of next month, SK midnight in UTC
    billable: IntegratorBillableUsage
    nonManaged: IntegratorNonManagedUsage
    exceedsAutoTier: bool
    contactThreshold: int  # 5000
    pricing: _IntegratorPricing
    firms: List[IntegratorFirmUsage]
    pagination: _IntegratorPagination
