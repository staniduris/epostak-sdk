"""ePošťák SDK -- Official Python SDK for the ePošťák API."""

from epostak.client import EPostak, validate
from epostak.errors import (
    DuplicateInvoiceExistingDocument,
    DuplicateInvoiceNumberError,
    DuplicateInvoiceRecipient,
    EPostakError,
)
from epostak.oauth import OAuth
from epostak.webhook_signature import (
    VerifyWebhookSignatureResult,
    verify_webhook_signature,
)

__all__ = [
    "EPostak",
    "EPostakError",
    "DuplicateInvoiceNumberError",
    "DuplicateInvoiceRecipient",
    "DuplicateInvoiceExistingDocument",
    "OAuth",
    "VerifyWebhookSignatureResult",
    "validate",
    "verify_webhook_signature",
]
__version__ = "0.8.0"
