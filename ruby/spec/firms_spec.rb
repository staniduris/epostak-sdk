# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Resources::Firms do
  let(:base_url) { "https://epostak.sk/api/v1" }
  let(:client) do
    EPostak::Client.new(
      client_id: "sk_int_test",
      client_secret: "secret",
      base_url: base_url,
      firm_id: "firm-1",
    )
  end

  before do
    stub_request(:post, "https://epostak.sk/sapi/v1/auth/token")
      .to_return(
        status: 200,
        body: { access_token: "test-token", token_type: "Bearer", expires_in: 3600 }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
  end

  it "creates a canonical consent link without X-Firm-Id" do
    stub = stub_request(:post, "#{base_url}/firms/consent-link")
      .with(
        body: {
          dic: "2022988022",
          customer_reference: "ERP-ACME",
          scopes: ["firms:manage", "documents:send"],
        }.to_json,
      )
      .with { |request| request.headers.keys.none? { |key| key.casecmp("X-Firm-Id").zero? } }
      .to_return(
        status: 201,
        body: {
          id: "offer-1",
          consent_url: "https://epostak.sk/auth/integrator-consent?token=one-time",
          customer_reference: "ERP-ACME",
          integration_path: "enterprise_api",
          requested_interfaces: ["enterprise_api"],
          scopes: ["firms:manage", "documents:send"],
          status: "issued",
          expires_at: "2026-08-18T10:00:00.000Z",
          created_at: "2026-08-11T10:00:00.000Z",
        }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    result = client.enterprise.firms.create_consent_link(
      dic: "2022988022",
      customer_reference: "ERP-ACME",
      scopes: ["firms:manage", "documents:send"],
    )

    expect(stub).to have_been_requested.once
    expect(result["consent_url"]).to end_with("token=one-time")
    expect(result["integration_path"]).to eq("enterprise_api")
  end
end
