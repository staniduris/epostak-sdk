"""Workflow-first Enterprise API namespace."""

from __future__ import annotations

from typing import Any


class EnterprisePullResource:
    """Cursor-based Enterprise pull APIs."""

    def __init__(self, inbound: Any, outbound: Any) -> None:
        self.inbound = inbound
        self.outbound = outbound


class EnterpriseResource:
    """Groups all `/api/v1/*` Enterprise resources under one namespace."""

    def __init__(self, client: Any) -> None:
        self.auth = client.auth
        self.audit = client.audit
        self.documents = client.documents
        self.inbox = client.documents.inbox
        self.firms = client.firms
        self.peppol = client.peppol
        self.webhooks = client.webhooks
        self.reporting = client.reporting
        self.extract = client.extract
        self.account = client.account
        self.integrator = client.integrator
        self.connector = client.connector
        self.pull = EnterprisePullResource(client.inbound, client.outbound)
