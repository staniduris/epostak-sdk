"""Resource modules for the ePostak SDK."""

from epostak.resources.account import AccountResource
from epostak.resources.connector import ConnectorResource
from epostak.resources.documents import DocumentsResource, InboxResource
from epostak.resources.events import EventsResource
from epostak.resources.extract import ExtractResource
from epostak.resources.firms import FirmsResource
from epostak.resources.inbound import InboundResource
from epostak.resources.integrator import (
    IntegratorLicensesResource,
    IntegratorResource,
)
from epostak.resources.outbound import OutboundResource
from epostak.resources.payloads import PayloadsResource
from epostak.resources.peppol import PeppolDirectoryResource, PeppolResource
from epostak.resources.reporting import ReportingResource
from epostak.resources.sapi import SapiResource
from epostak.resources.webhooks import WebhookQueueResource, WebhooksResource
from epostak.resources.white_label import WhiteLabelResource

__all__ = [
    "AccountResource",
    "ConnectorResource",
    "DocumentsResource",
    "EventsResource",
    "ExtractResource",
    "FirmsResource",
    "InboundResource",
    "InboxResource",
    "IntegratorLicensesResource",
    "IntegratorResource",
    "OutboundResource",
    "PayloadsResource",
    "PeppolDirectoryResource",
    "PeppolResource",
    "ReportingResource",
    "SapiResource",
    "WebhookQueueResource",
    "WebhooksResource",
    "WhiteLabelResource",
]
