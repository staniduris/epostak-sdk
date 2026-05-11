# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Resources::Inbound do
  # The HttpClient builds Faraday with url: base_url but Faraday strips the
  # path component when paths start with "/", so actual requests go to the
  # scheme+host root (e.g. https://epostak.sk/inbound/documents).
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
    it "sends GET /inbound/documents with query params" do
      stub = stub_request(:get, "#{host}/inbound/documents")
        .with(query: hash_including("limit" => "50", "since" => "2026-05-01T00:00:00Z"))
        .to_return(
          status: 200,
          body: { "documents" => [], "has_more" => false, "next_cursor" => nil }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.inbound.list(limit: 50, since: "2026-05-01T00:00:00Z")

      expect(stub).to have_been_requested
      expect(result["documents"]).to eq([])
      expect(result["has_more"]).to eq(false)
    end

    it "filters by kind and sender" do
      stub = stub_request(:get, "#{host}/inbound/documents")
        .with(query: hash_including("kind" => "invoice", "sender" => "0245:1234567890"))
        .to_return(
          status: 200,
          body: { "documents" => [], "has_more" => false }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      client.inbound.list(kind: "invoice", sender: "0245:1234567890")
      expect(stub).to have_been_requested
    end
  end

  describe "#get" do
    it "sends GET /inbound/documents/:id" do
      stub = stub_request(:get, "#{host}/inbound/documents/doc-123")
        .to_return(
          status: 200,
          body: { "id" => "doc-123", "kind" => "invoice" }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.inbound.get("doc-123")

      expect(stub).to have_been_requested
      expect(result["id"]).to eq("doc-123")
    end

    it "raises EPostak::Error on 404" do
      stub_request(:get, "#{host}/inbound/documents/missing")
        .to_return(
          status: 404,
          body: { "error" => { "code" => "NOT_FOUND", "message" => "Not found" } }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      expect { client.inbound.get("missing") }.to raise_error(EPostak::Error) do |e|
        expect(e.status).to eq(404)
      end
    end
  end

  describe "#get_ubl" do
    it "returns raw XML string" do
      xml = '<?xml version="1.0"?><Invoice />'
      stub = stub_request(:get, "#{host}/inbound/documents/doc-123/ubl")
        .to_return(status: 200, body: xml, headers: { "Content-Type" => "application/xml" })

      result = client.inbound.get_ubl("doc-123")

      expect(stub).to have_been_requested
      expect(result).to eq(xml)
    end
  end

  describe "#ack" do
    it "posts to /inbound/documents/:id/ack" do
      stub = stub_request(:post, "#{host}/inbound/documents/doc-123/ack")
        .with(body: "{}")
        .to_return(
          status: 200,
          body: { "id" => "doc-123", "clientAckedAt" => "2026-05-11T10:00:00Z" }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      result = client.inbound.ack("doc-123")

      expect(stub).to have_been_requested
      expect(result["clientAckedAt"]).to eq("2026-05-11T10:00:00Z")
    end

    it "includes client_reference when provided" do
      stub = stub_request(:post, "#{host}/inbound/documents/doc-123/ack")
        .with(body: { client_reference: "REF-001" }.to_json)
        .to_return(
          status: 200,
          body: { "id" => "doc-123" }.to_json,
          headers: { "Content-Type" => "application/json" },
        )

      client.inbound.ack("doc-123", client_reference: "REF-001")
      expect(stub).to have_been_requested
    end
  end
end
