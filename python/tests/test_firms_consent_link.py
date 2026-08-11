from unittest.mock import MagicMock, patch

import httpx

from epostak.resources.firms import FirmsResource
from epostak.types import FirmConsentLinkResponse


def test_create_consent_link_uses_canonical_wire_body_without_firm_header() -> None:
    resource = FirmsResource(
        httpx.Client(),
        "https://epostak.sk/api/v1",
        MagicMock(),
        "firm-1",
    )
    response: FirmConsentLinkResponse = {
        "id": "49702ea6-41bf-47ef-9cb6-657450fdb299",
        "consent_url": "https://epostak.sk/auth/integrator-consent?token=one-time",
        "customer_reference": "ERP-ACME",
        "integration_path": "enterprise_api",
        "requested_interfaces": ["enterprise_api"],
        "scopes": ["documents:read", "documents:send", "firms:manage"],
        "status": "issued",
        "expires_at": "2026-08-18T10:00:00.000Z",
        "created_at": "2026-08-11T10:00:00.000Z",
    }

    with patch.object(resource, "_request", return_value=response) as request:
        result = resource.create_consent_link(
            dic="2022988022",
            customer_reference="ERP-ACME",
            scopes=["firms:manage", "documents:send", "documents:read"],
        )

    request.assert_called_once_with(
        "POST",
        "/firms/consent-link",
        json={
            "dic": "2022988022",
            "customer_reference": "ERP-ACME",
            "scopes": ["firms:manage", "documents:send", "documents:read"],
        },
        omit_firm_id=True,
    )
    assert result["consent_url"].endswith("token=one-time")
