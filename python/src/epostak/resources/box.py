"""ePostak Box resource -- durable, scheduled, and retryable dispatch layer."""

from __future__ import annotations

from typing import Any, Dict, Optional, TYPE_CHECKING, Union
from urllib.parse import quote

from epostak.resources.documents import _BaseResource, _build_query

if TYPE_CHECKING:
    from epostak.types import (
        BoxItem,
        BoxItemDetail,
        BoxCreateRequest,
        BoxListResponse,
        BoxScheduleRequest,
    )


class BoxResource(_BaseResource):
    """ePošťák Box durable execution layer for Peppol dispatch."""

    def list(
        self,
        *,
        status: Optional[str] = None,
        direction: Optional[str] = None,
        limit: Optional[int] = None,
        offset: Optional[int] = None,
    ) -> "BoxListResponse":
        params = _build_query({
            "status": status,
            "direction": direction,
            "limit": limit,
            "offset": offset,
        })
        return self._request("GET", "/box/items", params=params)

    def create(self, body: "BoxCreateRequest") -> Dict[str, Any]:
        return self._request("POST", "/box/items", json=body)

    def get(self, item_id: str) -> "BoxItemDetail":
        return self._request("GET", f"/box/items/{quote(item_id, safe='')}")

    def schedule(
        self,
        item_id: str,
        scheduled_for: Union[str, "BoxScheduleRequest"],
    ) -> "BoxItem":
        body: Dict[str, Any]
        if isinstance(scheduled_for, dict):
            body = dict(scheduled_for)
        else:
            body = {"scheduledFor": scheduled_for}
        return self._request(
            "POST",
            f"/box/items/{quote(item_id, safe='')}/schedule",
            json=body,
        )

    def send_now(self, item_id: str) -> Dict[str, Any]:
        return self._request("POST", f"/box/items/{quote(item_id, safe='')}/send-now")

    def retry(self, item_id: str) -> "BoxItem":
        return self._request("POST", f"/box/items/{quote(item_id, safe='')}/retry")

    def cancel(self, item_id: str) -> "BoxItem":
        return self._request("POST", f"/box/items/{quote(item_id, safe='')}/cancel")
