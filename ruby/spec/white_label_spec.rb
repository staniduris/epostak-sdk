# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Resources::WhiteLabel do
  let(:base_url) { "https://epostak.sk/api/v1" }
  let(:client) do
    EPostak::Client.new(
      client_id: "sk_int_test",
      client_secret: "secret",
      base_url: base_url,
      firm_id: "must-not-leak"
    )
  end

  before do
    stub_request(:post, "https://epostak.sk/sapi/v1/auth/token")
      .to_return(
        status: 200,
        body: { access_token: "test-token", expires_in: 3600 }.to_json,
        headers: { "Content-Type" => "application/json" }
      )
  end

  it "registers with idempotency and without X-Firm-Id" do
    stub = stub_request(:post, "#{base_url}/white-label/participants/registrations")
      .with(
        body: {
          customerRef: "ERP-ACME",
          dic: "2022988022",
          companyEmail: "uctaren@example.sk",
          verificationToken: "one-time-secret"
        }.to_json,
        headers: { "Idempotency-Key" => "wl-register-1" }
      )
      .with { |request| request.headers.keys.none? { |key| key.casecmp("X-Firm-Id").zero? } }
      .to_return(status: 200, body: {}.to_json, headers: { "Content-Type" => "application/json" })

    expect(client.enterprise.white_label).to equal(client.white_label)
    client.white_label.register_participant(
      {
        customerRef: "ERP-ACME",
        dic: "2022988022",
        companyEmail: "uctaren@example.sk",
        verificationToken: "one-time-secret"
      },
      idempotency_key: "wl-register-1"
    )

    expect(stub).to have_been_requested.once
  end

  it "rejects a blank idempotency key before sending" do
    expect do
      client.white_label.register_participant(
        { customerRef: "ERP-ACME", verificationToken: "one-time-secret" },
        idempotency_key: "   "
      )
    end.to raise_error(ArgumentError, /1-255 UTF-8 bytes/)

    expect(a_request(:any, %r{/white-label/})).not_to have_been_made
  end
  it "creates and reads offers and customers with isolated headers" do
    offer = { targetIdentifierType: "dic", targetIdentifier: "2022988022",
      integrationPath: "sapi", relationshipMode: "technical_delegation", scopes: ["documents:read"] }
    customer = { customerRef: "ERP-1", relationship: "represented", country: "SK",
      taxId: "2022988022", contactEmail: "test@example.com",
      customerAuthorization: { confirmed: true, evidenceReference: "contract-1" } }
    stubs = [
      stub_request(:post, "#{base_url}/consent-offers").with(body: offer.to_json),
      stub_request(:get, "#{base_url}/consent-offers/offer%2Fid"),
      stub_request(:get, "#{base_url}/customers").with(query: { limit: 2, cursor: "next +", status: "active" }),
      stub_request(:post, "#{base_url}/customers").with(body: customer.to_json, headers: { "Idempotency-Key" => "customer-1" })
    ]
    stubs.each do |stub|
      stub.with { |request| request.headers.keys.none? { |key| key.casecmp("X-Firm-Id").zero? } }
        .to_return(status: 200, body: { id: "result" }.to_json, headers: { "Content-Type" => "application/json" })
    end
    expect(client.firms.create_consent_offer(offer)["id"]).to eq("result")
    client.firms.get_consent_offer("offer/id")
    client.white_label.list_customers(limit: 2, cursor: "next +", status: "active")
    client.white_label.create_customer(customer, idempotency_key: "customer-1")
    expect { client.white_label.create_customer(customer, idempotency_key: " ") }.to raise_error(ArgumentError)
    stubs.each { |stub| expect(stub).to have_been_requested.once }
  end

end
