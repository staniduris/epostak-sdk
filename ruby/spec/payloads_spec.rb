# frozen_string_literal: true

require "spec_helper"
require "stringio"

RSpec.describe EPostak::Resources::Payloads do
  let(:base_url) { "https://epostak.sk/api/v1" }
  let(:client) do
    EPostak::Client.new(
      client_id: "sk_live_test",
      client_secret: "secret",
      base_url: base_url,
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

  it "sends corrected OCR fields as JSON in the multipart request" do
    request = stub_request(:post, "#{base_url}/payloads/extract")
      .with do |candidate|
        candidate.body.include?('name="fields"') &&
          candidate.body.include?('"vendor_dic":"2020123456"') &&
          candidate.body.include?('"iban":"SK6807200002891987426353"')
      end
      .to_return(
        status: 200,
        body: { needs_review: true, applied_overrides: %w[vendor_dic iban] }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    response = client.payloads.extract(
      StringIO.new("pdf"),
      "application/pdf",
      file_name: "invoice.pdf",
      corrected_fields: {
        vendor_dic: "2020123456",
        iban: "SK6807200002891987426353",
      },
    )

    expect(request).to have_been_requested
    expect(response["applied_overrides"]).to eq(%w[vendor_dic iban])
  end
end
