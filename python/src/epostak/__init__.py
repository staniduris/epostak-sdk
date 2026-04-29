"""ePošťák SDK -- Official Python SDK for the ePošťák API."""

from epostak.client import EPostak, validate
from epostak.errors import EPostakError
from epostak.webhook_signature import (
    VerifyWebhookSignatureResult,
    verify_webhook_signature,
)

__all__ = [
    "EPostak",
    "EPostakError",
    "VerifyWebhookSignatureResult",
    "validate",
    "verify_webhook_signature",
]
__version__ = "2.0.0"
