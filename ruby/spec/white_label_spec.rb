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
end
