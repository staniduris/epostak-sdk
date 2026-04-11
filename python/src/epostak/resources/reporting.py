"""Reporting resource -- aggregated document statistics.

Provides :class:`ReportingResource` for querying document volume statistics
broken down by inbound/outbound and delivery status.
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
    ) -> Statistics:
        """Get aggregated document statistics for a date range.

        If no dates are provided, returns statistics for the current billing period.

        Args:
            from_date: Start date (ISO 8601 or YYYY-MM-DD).  Inclusive.
            to_date: End date (ISO 8601 or YYYY-MM-DD).  Inclusive.

        Returns:
            Dict with ``period`` (date range), ``outbound`` (total/delivered/failed),
            and ``inbound`` (total/acknowledged/pending).

        Example::

            stats = client.reporting.statistics(from_date="2026-01-01", to_date="2026-03-31")
            print(f"Sent: {stats['outbound']['total']}, Received: {stats['inbound']['total']}")
        """
        params = _build_query({"from": from_date, "to": to_date})
        return self._request("GET", "/reporting/statistics", params=params)
