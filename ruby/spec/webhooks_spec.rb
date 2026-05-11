# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Resources::Webhooks do
  let(:base_url) { "https://epostak.sk/api/v1" }
  let(:host)     { "https://epostak.sk" }
  let(:token_response) do
    { "access_token" => "test-token", "token_type" => "Bearer", "expires_in" => 3600 }.to_json
  end

  before do
    stub_request(:post, "https://epostak.sk/sapi/v1/auth/token")
      .to_return(status: 200, body: token_response, headers: { "Content-Type" => "application/json" })
  end

  let(:client) do
    EPostak::Client.new(client_id: "sk_live_test", client_secret: "secret", base_url: base_url)
  end

  describe "#test with event param" do
    it "posts with event in body when provided" do
      stub = stub_request(:post, "#{host}/webhooks/wh-1/test")
        .with(body: { event: "document.delivered" }.to_json)
        .to_return(
          status: 200,
          body: { "success" => true, "statusCode" => 200 }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.webhooks.test("wh-1", event: "document.delivered")

      expect(stub).to have_been_requested
      expect(result["success"]).to eq(true)
    end

    it "posts with empty body when no event is given" do
      stub = stub_request(:post, "#{host}/webhooks/wh-1/test")
        .with(body: "{}")
        .to_return(
          status: 200,
          body: { "success" => true }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      client.webhooks.test("wh-1")
      expect(stub).to have_been_requested
    end
  end
end
