"""ePostak SDK - Official Python SDK for the ePostak Enterprise API."""

from epostak.client import EPostak, validate
from epostak.errors import EPostakError
from epostak.resources.webhooks import verify_webhook_signature

__all__ = ["EPostak", "EPostakError", "validate", "verify_webhook_signature"]
__version__ = "0.5.0"
