"""White Label participant registration and migration lifecycle."""

from __future__ import annotations

from typing import Optional, TYPE_CHECKING
from urllib.parse import quote

from epostak.resources.documents import _BaseResource, _build_query, _idempotency_headers

if TYPE_CHECKING:
    from epostak.types import (
        WhiteLabelMigrationCodeResponse,
        WhiteLabelParticipant,
        WhiteLabelParticipantList,
        WhiteLabelParticipantMigrationRequest,
        WhiteLabelParticipantOperation,
        WhiteLabelParticipantRegistrationRequest,
    )


def _white_label_idempotency_key(value: str) -> str:
    if not isinstance(value, str) or not value.strip() or len(value.encode("utf-8")) > 255:
        raise ValueError("White Label idempotency key must be 1-255 UTF-8 bytes")
    return value


class WhiteLabelResource(_BaseResource):
    """Integrator-scoped White Label participant lifecycle.

    Calls never send ``X-Firm-Id``. The authenticated White Label integrator
    determines participant ownership on the server.
    """

    def list_participants(
        self,
        *,
        limit: Optional[int] = None,
        cursor: Optional[str] = None,
    ) -> WhiteLabelParticipantList:
        return self._request(
            "GET",
            "/white-label/participants",
            params=_build_query({"limit": limit, "cursor": cursor}),
            omit_firm_id=True,
        )

    def register_participant(
        self,
        request: WhiteLabelParticipantRegistrationRequest,
        *,
        idempotency_key: str,
    ) -> WhiteLabelParticipantOperation:
        key = _white_label_idempotency_key(idempotency_key)
        return self._request(
            "POST",
            "/white-label/participants/registrations",
            json=request,
            extra_headers=_idempotency_headers(key),
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def migrate_participant(
        self,
        request: WhiteLabelParticipantMigrationRequest,
        *,
        idempotency_key: str,
    ) -> WhiteLabelParticipantOperation:
        key = _white_label_idempotency_key(idempotency_key)
        return self._request(
            "POST",
            "/white-label/participants/migrations",
            json=request,
            extra_headers=_idempotency_headers(key),
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def get_participant(self, participant_id: str) -> WhiteLabelParticipant:
        return self._request(
            "GET",
            f"/white-label/participants/{quote(participant_id, safe='')}",
            omit_firm_id=True,
        )

    def request_migration_code(
        self,
        participant_id: str,
        *,
        idempotency_key: str,
    ) -> WhiteLabelMigrationCodeResponse:
        key = _white_label_idempotency_key(idempotency_key)
        return self._request(
            "POST",
            f"/white-label/participants/{quote(participant_id, safe='')}/migration-code",
            extra_headers=_idempotency_headers(key),
            omit_firm_id=True,
            retry_on_failure=True,
        )

    def get_operation(self, operation_id: str) -> WhiteLabelParticipantOperation:
        return self._request(
            "GET",
            f"/white-label/operations/{quote(operation_id, safe='')}",
            omit_firm_id=True,
        )
