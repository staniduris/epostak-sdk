"""ePošťák SDK -- Official Python SDK for the ePošťák API."""

from epostak.client import EPostak, validate
from epostak.errors import EPostakError
from epostak.oauth import OAuth
from epostak.webhook_signature import (
    VerifyWebhookSignatureResult,
    verify_webhook_signature,
)

__all__ = [
    "EPostak",
    "EPostakError",
    "OAuth",
    "VerifyWebhookSignatureResult",
    "validate",
    "verify_webhook_signature",
]
__version__ = "2.1.0"
