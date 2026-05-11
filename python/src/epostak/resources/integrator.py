"""Integrator-aggregate resource (``sk_int_*`` keys only).

Exposes endpoints that operate at the integrator level (no ``X-Firm-Id``).
For per-firm ``/account`` and ``/licenses/info`` views (which integrators
also reach via ``X-Firm-Id``), use :mod:`epostak.resources.account` instead.
"""

from __future__ import annotations

from typing import TYPE_CHECKING, Any, Dict, Optional

import httpx

if TYPE_CHECKING:
    from epostak.types import IntegratorLicenseInfo

from epostak.resources.documents import _BaseResource, _build_query


class IntegratorLicensesResource(_BaseResource):
    """``/integrator/licenses/*`` — billing aggregate views."""

    def info(
        self,
        offset: int = 0,
        limit: int = 50,
    ) -> IntegratorLicenseInfo:
        """Aggregate plan + current-period usage across managed firms.

        Wraps ``GET /api/v1/integrator/licenses/info``. Tier rates are applied
        to the AGGREGATE document count (not per-firm summed) — a 100-firm ×
        50-doc integrator lands in tier 2-3, not tier 1 like a standalone
        firm would.

        Volumes above ``contactThreshold`` (5 000 / month) flip
        ``exceedsAutoTier`` to ``True``; auto-billing pauses there and sales
        handles invoicing manually.

        Args:
            offset: Pagination offset for the per-firm list (default 0).
            limit: Page size for the per-firm list, max 100 (default 50).

        Returns:
            Dict with ``integrator``, ``period``, ``nextResetAt``,
            ``billable`` (managed-plan aggregate + tier-applied charges),
            ``nonManaged`` (firms paying their own plan),
            ``exceedsAutoTier``, ``contactThreshold``, ``pricing``,
            ``firms`` (paginated per-firm rows), and ``pagination``.

        Requires the ``account:read`` scope on a ``sk_int_*`` integrator key.
        No ``X-Firm-Id`` header — the endpoint is integrator-scoped.

        Example::

            usage = client.integrator.licenses.info(limit=100)
            if usage["exceedsAutoTier"]:
                # sales review required, auto-billing has paused
                ...
            print(usage["billable"]["totalCharge"], "EUR this month")
        """
        params = _build_query({"offset": offset, "limit": limit})
        return self._request("GET", "/integrator/licenses/info", params=params)


class IntegratorResource:
    """Integrator-aggregate endpoints (``sk_int_*`` only)."""

    def __init__(
        self,
        client: httpx.Client,
        base_url: str,
        token_manager: object,
        firm_id: Optional[str],
        *,
        max_retries: int = 3,
        _rate_limit_store: Optional[list] = None,
    ) -> None:
        self.licenses = IntegratorLicensesResource(
            client, base_url, token_manager, firm_id, max_retries=max_retries,
            _rate_limit_store=_rate_limit_store,
        )
