# frozen_string_literal: true

require "spec_helper"

RSpec.describe "Client#last_rate_limit" do
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

  it "is nil before any request" do
    expect(client.last_rate_limit).to be_nil
  end

  it "is populated after a successful request with rate-limit headers" do
    reset_epoch = (Time.now + 60).to_i
    stub_request(:get, "#{host}/inbound/documents")
      .with(query: hash_including("limit" => "100"))
      .to_return(
        status: 200,
        body: { "documents" => [], "has_more" => false }.to_json,
        headers: {
          "Content-Type"          => "application/json",
          "X-RateLimit-Limit"     => "1000",
          "X-RateLimit-Remaining" => "997",
          "X-RateLimit-Reset"     => reset_epoch.to_s,
        },
      )

    client.inbound.list

    rl = client.last_rate_limit
    expect(rl).not_to be_nil
    expect(rl.limit).to eq(1000)
    expect(rl.remaining).to eq(997)
    expect(rl.reset_at).to be_a(Time)
    expect(rl.reset_at.to_i).to eq(reset_epoch)
  end

  it "returns nil reset_at when no reset header is present" do
    stub_request(:get, "#{host}/inbound/documents")
      .with(query: hash_including("limit" => "100"))
      .to_return(
        status: 200,
        body: { "documents" => [], "has_more" => false }.to_json,
        headers: {
          "Content-Type"          => "application/json",
          "X-RateLimit-Limit"     => "1000",
          "X-RateLimit-Remaining" => "999",
        },
      )

    client.inbound.list

    rl = client.last_rate_limit
    expect(rl.reset_at).to be_nil
  end

  it "is not populated when response has no rate-limit headers" do
    stub_request(:get, "#{host}/inbound/documents")
      .with(query: hash_including("limit" => "100"))
      .to_return(
        status: 200,
        body: { "documents" => [], "has_more" => false }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    client.inbound.list

    expect(client.last_rate_limit).to be_nil
  end
end
