"""Events pull/ack facade."""

from __future__ import annotations

from typing import Any, Dict, List, Optional
from urllib.parse import quote

from epostak.resources.documents import _BaseResource, _build_query


class EventsResource(_BaseResource):
    """Pull and acknowledge events via the preferred ``/events/*`` aliases."""

    def pull(
        self,
        limit: Optional[int] = None,
        event_type: Optional[str] = None,
    ) -> Dict[str, Any]:
        params = _build_query({"limit": limit, "event_type": event_type})
        return self._normalize_pull_response(
            self._request("GET", "/events/pull", params=params)
        )

    def ack(self, event_id: str) -> Dict[str, Any]:
        return self._request("POST", f"/events/{quote(event_id, safe='')}/ack")

    def batch_ack(self, event_ids: List[str]) -> Dict[str, Any]:
        return self._request("POST", "/events/batch-ack", json={"event_ids": event_ids})

    @staticmethod
    def _normalize_pull_response(response: Dict[str, Any]) -> Dict[str, Any]:
        if "items" not in response and isinstance(response.get("events"), list):
            return {**response, "items": response["events"]}
        return response
