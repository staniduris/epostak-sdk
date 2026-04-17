"""Webhooks resource -- manage webhook subscriptions and pull queue.

Provides :class:`WebhooksResource` for creating, updating, and deleting push
webhook subscriptions, and :class:`WebhookQueueResource` (accessible via
``client.webhooks.queue``) for polling-based event consumption.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional, TYPE_CHECKING
from urllib.parse import quote

if TYPE_CHECKING:
    from epostak.types import (
        Webhook,
        WebhookDetail,
        WebhookQueueAllResponse,
        WebhookQueueResponse,
        WebhookWithDeliveries,
    )

from epostak.resources.documents import _BaseResource, _build_query

import httpx


class WebhookQueueResource(_BaseResource):
    """Webhook pull queue for polling-based event consumption."""

    def pull(
        self,
        limit: Optional[int] = None,
        event_type: Optional[str] = None,
    ) -> WebhookQueueResponse:
        """Fetch pending webhook events from the pull queue.

        Args:
            limit: Max items to return, 1-100 (default 20).
            event_type: Filter by event type, e.g. ``"document.received"``.

        Returns:
            Dict with ``items`` (list of queue events) and ``has_more`` (bool).

        Example::

            queue = client.webhooks.queue.pull(limit=50)
            for item in queue["items"]:
                print(item["type"], item["payload"])
        """
        params = _build_query({"limit": limit, "event_type": event_type})
        return self._request("GET", "/webhook-queue", params=params)

    def ack(self, event_id: str) -> None:
        """Acknowledge a single webhook event (removes it from the queue).

        Args:
            event_id: The event UUID to acknowledge.

        Returns:
            None (HTTP 204).

        Example::

            client.webhooks.queue.ack("event-uuid")
        """
        self._request("DELETE", f"/webhook-queue/{quote(event_id, safe='')}")

    def batch_ack(self, event_ids: List[str]) -> None:
        """Batch acknowledge webhook events (removes them from the queue).

        Args:
            event_ids: List of event UUIDs to acknowledge.

        Returns:
            None (HTTP 204).

        Example::

            ids = [item["id"] for item in queue["items"]]
            client.webhooks.queue.batch_ack(ids)
        """
        self._request("POST", "/webhook-queue/batch-ack", json={"event_ids": event_ids})

    def pull_all(
        self,
        limit: Optional[int] = None,
        since: Optional[str] = None,
    ) -> WebhookQueueAllResponse:
        """Fetch events across all firms (integrator only).

        Args:
            limit: Max items, 1-500 (default 100).
            since: ISO 8601 timestamp -- only return events created after this time.

        Returns:
            Dict with ``events`` (each including ``firm_id``) and ``count``.

        Example::

            queue = client.webhooks.queue.pull_all(limit=200)
            for event in queue["events"]:
                print(event["firm_id"], event["event"])
        """
        params = _build_query({"limit": limit, "since": since})
        return self._request("GET", "/webhook-queue/all", params=params)

    def batch_ack_all(self, event_ids: List[str]) -> Dict[str, int]:
        """Cross-firm batch acknowledge (integrator only).

        Args:
            event_ids: List of event UUIDs from any firm.

        Returns:
            Dict with ``acknowledged`` count.

        Example::

            ids = [e["event_id"] for e in queue["events"]]
            result = client.webhooks.queue.batch_ack_all(ids)
            print(result["acknowledged"])
        """
        return self._request("POST", "/webhook-queue/all/batch-ack", json={"event_ids": event_ids})


class WebhooksResource(_BaseResource):
    """Manage webhook subscriptions and access the pull queue."""

    queue: WebhookQueueResource
    """Access the webhook pull queue for polling-based consumption."""

    def __init__(
        self,
        client: httpx.Client,
        base_url: str,
        api_key: str,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
    ) -> None:
        super().__init__(client, base_url, api_key, firm_id, max_retries=max_retries)
        self.queue = WebhookQueueResource(client, base_url, api_key, firm_id, max_retries=max_retries)

    def create(
        self,
        url: str,
        events: Optional[List[str]] = None,
    ) -> WebhookDetail:
        """Register a webhook endpoint.

        Args:
            url: The HTTPS URL to deliver webhook events to.
            events: Optional list of event types to subscribe to.  If omitted, all
                events are delivered.  Valid types: ``"document.created"``,
                ``"document.sent"``, ``"document.received"``, ``"document.validated"``.

        Returns:
            Webhook detail including the HMAC-SHA256 signing ``secret`` -- store it
            securely for verifying webhook signatures.

        Example::

            webhook = client.webhooks.create(
                url="https://example.com/webhook",
                events=["document.received", "document.sent"],
            )
            print(webhook["id"], webhook["secret"])
        """
        body: Dict[str, Any] = {"url": url}
        if events is not None:
            body["events"] = events
        return self._request("POST", "/webhooks", json=body)

    def list(self) -> List[Webhook]:
        """List all webhook subscriptions.

        Returns:
            List of webhook dicts with ``id``, ``url``, ``events``, ``isActive``,
            and ``createdAt``.

        Example::

            webhooks = client.webhooks.list()
            for wh in webhooks:
                print(wh["url"], wh["isActive"])
        """
        res = self._request("GET", "/webhooks")
        return res.get("data", res) if isinstance(res, dict) else res

    def get(self, id: str) -> WebhookWithDeliveries:
        """Get webhook detail with recent delivery history.

        Args:
            id: Webhook UUID.

        Returns:
            Webhook dict including ``deliveries`` list with recent delivery attempts.

        Example::

            detail = client.webhooks.get("webhook-uuid")
            for d in detail["deliveries"]:
                print(d["event"], d["status"], d["responseStatus"])
        """
        return self._request("GET", f"/webhooks/{quote(id, safe='')}")

    def update(
        self,
        id: str,
        url: Optional[str] = None,
        events: Optional[List[str]] = None,
        is_active: Optional[bool] = None,
    ) -> Webhook:
        """Update a webhook subscription.

        All parameters besides ``id`` are optional -- only pass the fields you want
        to change.

        Args:
            id: Webhook UUID.
            url: New delivery URL.
            events: New list of event types to subscribe to.
            is_active: Set to ``False`` to pause delivery, ``True`` to resume.

        Returns:
            The updated webhook object.

        Example::

            client.webhooks.update("webhook-uuid", is_active=False)
        """
        body: Dict[str, Any] = {}
        if url is not None:
            body["url"] = url
        if events is not None:
            body["events"] = events
        if is_active is not None:
            body["isActive"] = is_active
        return self._request("PATCH", f"/webhooks/{quote(id, safe='')}", json=body)

    def delete(self, id: str) -> None:
        """Delete a webhook subscription.

        Args:
            id: Webhook UUID.

        Returns:
            None (HTTP 204).

        Example::

            client.webhooks.delete("webhook-uuid")
        """
        self._request("DELETE", f"/webhooks/{quote(id, safe='')}")

    def test(self, id: str, event: Optional[str] = None) -> Dict[str, Any]:
        """Send a test event to a webhook endpoint.

        Args:
            id: Webhook UUID to test.
            event: Event type to simulate, e.g. ``"document.created"``.
                If omitted, the server picks a default event.

        Returns:
            Dict with ``success`` (bool), ``statusCode`` (int or None),
            ``responseTime`` (int), ``webhookId``, ``event``, and optional ``error``.

        Example::

            result = client.webhooks.test("webhook-uuid", event="document.received")
            print(result["success"], result["responseTime"])
        """
        body: Dict[str, Any] = {}
        if event is not None:
            body["event"] = event
        return self._request("POST", f"/webhooks/{quote(id, safe='')}/test", json=body)

    def deliveries(
        self,
        id: str,
        *,
        limit: Optional[int] = None,
        offset: Optional[int] = None,
        status: Optional[str] = None,
        event: Optional[str] = None,
    ) -> Dict[str, Any]:
        """Get paginated delivery history for a webhook.

        Args:
            id: Webhook UUID.
            limit: Max deliveries to return, 1-100 (default 20).
            offset: Number of deliveries to skip (default 0).
            status: Filter by status: ``"SUCCESS"``, ``"FAILED"``, ``"PENDING"``, ``"RETRYING"``.
            event: Filter by event type, e.g. ``"document.received"``.

        Returns:
            Dict with ``deliveries`` list, ``total``, ``limit``, and ``offset``.

        Example::

            result = client.webhooks.deliveries("webhook-uuid", status="FAILED", limit=50)
            for d in result["deliveries"]:
                print(d["event"], d["status"], d["attempts"])
        """
        params = _build_query({"limit": limit, "offset": offset, "status": status, "event": event})
        return self._request("GET", f"/webhooks/{quote(id, safe='')}/deliveries", params=params)

    def rotate_secret(self, id: str) -> Dict[str, Any]:
        """Rotate a webhook's HMAC-SHA256 signing secret.

        Issues a fresh signing secret and invalidates the previous one
        immediately. The new secret is returned ONCE -- store it right
        away. In-flight deliveries signed with the old secret will no
        longer verify on the receiving side. Non-destructive alternative
        to deleting and recreating the webhook when a secret leaks.

        Args:
            id: Webhook UUID whose secret to rotate.

        Returns:
            Dict with ``id``, ``secret`` (only shown once), and ``message``.

        Example::

            res = client.webhooks.rotate_secret("webhook-uuid")
            save_to_secrets_manager(res["secret"])
        """
        return self._request("POST", f"/webhooks/{quote(id, safe='')}/rotate-secret")
