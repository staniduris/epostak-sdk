"""Account resource -- account info and usage.

Provides :class:`AccountResource` for retrieving the authenticated account's
firm details, subscription plan, and current billing-period usage.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from epostak.types import Account

from epostak.resources.documents import _BaseResource


class AccountResource(_BaseResource):
    """Account and firm information."""

    def get(self) -> Account:
        """Get account info including firm details, plan, and usage.

        Returns:
            Dict with ``firm`` (name, ICO, Peppol status), ``plan`` (name, status),
            and ``usage`` (outbound/inbound counts for the current billing period).

        Example::

            account = client.account.get()
            print(account["firm"]["name"], account["plan"]["name"])
            print(f"Sent: {account['usage']['outbound']}")
        """
        return self._request("GET", "/account")
