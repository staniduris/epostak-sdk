"""ePostak SDK - Official Python SDK for the ePostak Enterprise API."""

from epostak.client import EPostak, validate
from epostak.errors import EPostakError

__all__ = ["EPostak", "EPostakError", "validate"]
__version__ = "0.2.0"
