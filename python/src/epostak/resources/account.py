"""Account resource -- account info and usage.

Provides :class:`AccountResource` for retrieving the authenticated account's
firm details, subscription plan, and current billing-period usage.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from epostak.types import Account, AccountStatus, RotateSecretResponse

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

    def status(self) -> AccountStatus:
        """Inspect the authenticated API key, firm, plan, and rate limits.

        Useful for debugging credentials, verifying which firm an integrator
        key is currently scoped to, and discovering per-minute rate limits.

        Returns:
            Dict with ``key`` (id/name/prefix/permissions/active/createdAt/lastUsedAt),
            ``firm`` (id/peppolStatus), ``plan`` (name/expiresAt/active),
            ``rateLimit`` (perMinute/window), and ``integrator`` (``{id}``
            for ``sk_int_*`` keys, otherwise None).

        Example::

            info = client.account.status()
            print(info["firm"]["id"], info["plan"]["name"])
            print(f"Rate limit: {info['rateLimit']['perMinute']}/min")
        """
        return self._request("GET", "/auth/status")

    def rotate_secret(self) -> RotateSecretResponse:
        """Rotate the plaintext secret for the current API key.

        Atomically deactivates the current key and issues a new one under
        the same name / permissions. The returned ``key`` is shown exactly
        once -- store it immediately.

        Integrator keys (``sk_int_*``) cannot be rotated through this
        endpoint; the server returns HTTP 403, which raises
        :class:`~epostak.errors.EPostakError`.

        Returns:
            Dict with ``key`` (new plaintext, shown ONCE), ``prefix``, and ``message``.

        Example::

            new = client.account.rotate_secret()
            save_somewhere_secure(new["key"])
        """
        return self._request("POST", "/auth/rotate-secret")
