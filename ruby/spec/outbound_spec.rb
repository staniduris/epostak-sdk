# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Resources::Outbound do
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

  describe "#list" do
    it "sends GET /outbound/documents" do
      stub = stub_request(:get, "#{host}/outbound/documents")
        .with(query: hash_including("limit" => "50"))
        .to_return(
          status: 200,
          body: { "documents" => [{ "id" => "out-1" }], "has_more" => false }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.outbound.list(limit: 50)

      expect(stub).to have_been_requested
      expect(result["documents"].first["id"]).to eq("out-1")
    end

    it "supports status and recipient filter" do
      stub = stub_request(:get, "#{host}/outbound/documents")
        .with(query: hash_including("status" => "DELIVERED", "recipient" => "0245:1234567890"))
        .to_return(
          status: 200,
          body: { "documents" => [], "has_more" => false }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      client.outbound.list(status: "DELIVERED", recipient: "0245:1234567890")
      expect(stub).to have_been_requested
    end
  end

  describe "#get" do
    it "fetches a single outbound document" do
      stub = stub_request(:get, "#{host}/outbound/documents/out-42")
        .to_return(
          status: 200,
          body: { "id" => "out-42", "status" => "DELIVERED" }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.outbound.get("out-42")

      expect(stub).to have_been_requested
      expect(result["status"]).to eq("DELIVERED")
    end
  end

  describe "#get_ubl" do
    it "returns raw XML for an outbound document" do
      xml = '<?xml version="1.0"?><Invoice />'
      stub = stub_request(:get, "#{host}/outbound/documents/out-42/ubl")
        .to_return(status: 200, body: xml, headers: { "Content-Type" => "application/xml" })

      result = client.outbound.get_ubl("out-42")

      expect(stub).to have_been_requested
      expect(result).to eq(xml)
    end
  end

  describe "#events" do
    it "sends GET /outbound/events" do
      stub = stub_request(:get, "#{host}/outbound/events")
        .to_return(
          status: 200,
          body: { "events" => [{ "id" => "ev-1", "type" => "sent" }], "has_more" => false }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.outbound.events

      expect(stub).to have_been_requested
      expect(result["events"].first["type"]).to eq("sent")
    end

    it "filters by document_id and cursor" do
      stub = stub_request(:get, "#{host}/outbound/events")
        .with(query: hash_including("document_id" => "doc-1", "cursor" => "abc"))
        .to_return(
          status: 200,
          body: { "events" => [], "has_more" => false }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      client.outbound.events(document_id: "doc-1", cursor: "abc")
      expect(stub).to have_been_requested
    end
  end
end
