"""Reporting resource -- aggregated document statistics.

Provides :class:`ReportingResource` for querying document volume statistics
broken down by direction (sent/received), document type, delivery rate,
and top counterparty rankings.
"""

from __future__ import annotations

from typing import Optional, TYPE_CHECKING

if TYPE_CHECKING:
    from epostak.types import Statistics

from epostak.resources.documents import _BaseResource, _build_query


class ReportingResource(_BaseResource):
    """Document statistics and reports."""

    def statistics(
        self,
        from_date: Optional[str] = None,
        to_date: Optional[str] = None,
        period: Optional[str] = None,
    ) -> Statistics:
        """Get aggregated document statistics for a date range.

        You may pass either an explicit ``from_date``/``to_date`` window
        OR a convenience ``period`` selector. ``period`` always wins on
        the server when both are present.

        Args:
            from_date: Start date (ISO 8601 or ``YYYY-MM-DD``). Inclusive.
            to_date: End date (ISO 8601 or ``YYYY-MM-DD``). Inclusive.
            period: Convenience window: ``"month"``, ``"quarter"``, or
                ``"year"``.

        Returns:
            Dict with ``period`` (resolved date range), ``sent`` and
            ``received`` (each ``{total, by_type}``), ``delivery_rate``
            (0–1 float), ``top_recipients``, and ``top_senders``.

        Example::

            stats = client.reporting.statistics(period="month")
            print(stats["sent"]["total"], stats["sent"]["by_type"])
            print(stats["received"]["total"], stats["delivery_rate"])
            for party in stats["top_recipients"]:
                print(party["peppol_id"], party["count"])
        """
        params = _build_query({"from": from_date, "to": to_date, "period": period})
        return self._request("GET", "/reporting/statistics", params=params)
