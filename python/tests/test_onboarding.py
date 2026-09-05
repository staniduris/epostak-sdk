import json
from unittest.mock import MagicMock

import httpx
import pytest

from epostak.resources.firms import FirmsResource
from epostak.resources.white_label import WhiteLabelResource


def test_onboarding_wire_contract_and_header_isolation():
    requests = []

    def handle(request):
        requests.append(request)
        return httpx.Response(200, json={"id": "result"})

    token = MagicMock()
    token.get_access_token.return_value = "test-token"
    with httpx.Client(transport=httpx.MockTransport(handle)) as http:
        firms = FirmsResource(http, "https://dev.epostak.sk/api/v1", token, "must-not-leak")
        white_label = WhiteLabelResource(http, "https://dev.epostak.sk/api/v1", token, "must-not-leak")
        offer = {"targetIdentifierType": "dic", "targetIdentifier": "2022988022",
                 "integrationPath": "sapi", "relationshipMode": "technical_delegation",
                 "scopes": ["documents:read"], "customerReference": "ERP-1"}
        assert firms.create_consent_offer(offer)["id"] == "result"
        firms.get_consent_offer("offer/id")
        white_label.list_customers(limit=2, cursor="next +", status="active")
        customer = {"customerRef": "ERP-1", "relationship": "represented", "country": "SK",
                    "taxId": "2022988022", "contactEmail": "test@example.com",
                    "customerAuthorization": {"confirmed": True, "evidenceReference": "contract-1"}}
        white_label.create_customer(customer, idempotency_key="customer-1")
        with pytest.raises(ValueError):
            white_label.create_customer(customer, idempotency_key=" ")

    assert len(requests) == 4
    assert all("X-Firm-Id" not in r.headers for r in requests)
    assert requests[0].url.path == "/api/v1/consent-offers"
    assert json.loads(requests[0].content) == offer
    assert requests[1].url.raw_path == b"/api/v1/consent-offers/offer%2Fid"
    assert dict(requests[2].url.params) == {"limit": "2", "cursor": "next +", "status": "active"}
    assert requests[3].url.path == "/api/v1/customers"
    assert requests[3].headers["Idempotency-Key"] == "customer-1"
    assert json.loads(requests[3].content) == customer
