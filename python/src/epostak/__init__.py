"""ePošťák SDK -- Official Python SDK for the ePošťák API."""

from epostak.client import EPostak, validate
from epostak.errors import (
    DuplicateInvoiceExistingDocument,
    DuplicateInvoiceNumberError,
    DuplicateInvoiceRecipient,
    EPostakError,
    UBL_RULES,
    UblValidationError,
)
from epostak.oauth import OAuth
from epostak.webhook_signature import (
    VerifyWebhookSignatureResult,
    verify_webhook_signature,
)

from epostak.types import (
    CreateWebhookRequest,
    UpdateWebhookRequest,
    WebhookEvent,
    WebhookPayload,
    WebhookPayloadData,
)

__all__ = [
    "EPostak",
    "EPostakError",
    "DuplicateInvoiceNumberError",
    "DuplicateInvoiceRecipient",
    "DuplicateInvoiceExistingDocument",
    "UblValidationError",
    "UBL_RULES",
    "OAuth",
    "VerifyWebhookSignatureResult",
    "validate",
    "verify_webhook_signature",
    "CreateWebhookRequest",
    "UpdateWebhookRequest",
    "WebhookEvent",
    "WebhookPayload",
    "WebhookPayloadData",
]
__version__ = "0.10.0"
