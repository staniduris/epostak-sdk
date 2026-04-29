"""Resource modules for the ePostak SDK."""

from epostak.resources.account import AccountResource
from epostak.resources.documents import DocumentsResource, InboxResource
from epostak.resources.extract import ExtractResource
from epostak.resources.firms import FirmsResource
from epostak.resources.integrator import (
    IntegratorLicensesResource,
    IntegratorResource,
)
from epostak.resources.peppol import PeppolDirectoryResource, PeppolResource
from epostak.resources.reporting import ReportingResource
from epostak.resources.webhooks import WebhookQueueResource, WebhooksResource

__all__ = [
    "AccountResource",
    "DocumentsResource",
    "ExtractResource",
    "FirmsResource",
    "InboxResource",
    "IntegratorLicensesResource",
    "IntegratorResource",
    "PeppolDirectoryResource",
    "PeppolResource",
    "ReportingResource",
    "WebhookQueueResource",
    "WebhooksResource",
]
