# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Resources::Connector do
  let(:base_url) { "https://epostak.sk/api/v1" }
  let(:host)     { "https://epostak.sk/api/v1" }
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

  it "posts Connector send with Idempotency-Key" do
    stub = stub_request(:post, "#{host}/connector/send")
      .with(headers: { "Idempotency-Key" => "erp-1" })
      .to_return(
        status: 201,
        body: { "documentId" => "doc-1", "status" => "accepted" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    result = client.connector.send_document(
      { receiverPeppolId: "0245:1234567890", document: { invoiceNumber: "FV-1" } },
      idempotency_key: "erp-1",
    )

    expect(stub).to have_been_requested
    expect(result["documentId"]).to eq("doc-1")
  end

  it "lists Connector inbox and events with cursor params" do
    inbox_stub = stub_request(:get, "#{host}/connector/inbox")
      .with(query: hash_including("cursor" => "cur-1", "limit" => "25"))
      .to_return(
        status: 200,
        body: { "documents" => [], "nextCursor" => nil, "hasMore" => false }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    events_stub = stub_request(:get, "#{host}/connector/events")
      .with(query: hash_including("limit" => "10"))
      .to_return(
        status: 200,
        body: { "events" => [], "nextCursor" => nil, "hasMore" => false }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    client.connector.inbox(cursor: "cur-1", limit: 25)
    client.connector.events(limit: 10)

    expect(inbox_stub).to have_been_requested
    expect(events_stub).to have_been_requested
  end

  it "gets status, inbox detail, and ack by document id" do
    status_stub = stub_request(:get, "#{host}/connector/status/doc-1")
      .to_return(
        status: 200,
        body: { "documentId" => "doc-1", "status" => "delivered" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    detail_stub = stub_request(:get, "#{host}/connector/inbox/doc-1")
      .to_return(
        status: 200,
        body: { "documentId" => "doc-1", "status" => "received" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    ack_stub = stub_request(:post, "#{host}/connector/inbox/doc-1/ack")
      .with(body: "{}")
      .to_return(
        status: 200,
        body: { "documentId" => "doc-1", "status" => "processed", "acknowledged" => true }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    client.connector.status("doc-1")
    client.connector.get_inbox_document("doc-1")
    client.connector.ack("doc-1")

    expect(status_stub).to have_been_requested
    expect(detail_stub).to have_been_requested
    expect(ack_stub).to have_been_requested
  end

  it "uses Connector outbox paths" do
    stage_stub = stub_request(:post, "#{host}/connector/outbox")
      .with(body: /FA-1/)
      .to_return(
        status: 201,
        body: { "total" => 1, "items" => [{ "outboxId" => "outbox-1", "status" => "ready" }] }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    list_stub = stub_request(:get, "#{host}/connector/outbox")
      .with(query: hash_including("status" => "blocked", "limit" => "10", "offset" => "20"))
      .to_return(status: 200, body: { "items" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    detail_stub = stub_request(:get, "#{host}/connector/outbox/outbox-1")
      .to_return(status: 200, body: { "outboxId" => "outbox-1" }.to_json, headers: { "Content-Type" => "application/json" })
    send_stub = stub_request(:post, "#{host}/connector/outbox/outbox-1/send")
      .with(body: { force: true }.to_json)
      .to_return(status: 200, body: { "outboxId" => "outbox-1", "status" => "sent" }.to_json, headers: { "Content-Type" => "application/json" })
    batch_stub = stub_request(:post, "#{host}/connector/outbox/send")
      .with(body: { ids: ["outbox-1"], force: true }.to_json)
      .to_return(status: 200, body: { "results" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    cancel_stub = stub_request(:delete, "#{host}/connector/outbox/outbox-1")
      .to_return(status: 200, body: { "outboxId" => "outbox-1", "status" => "cancelled" }.to_json, headers: { "Content-Type" => "application/json" })

    client.connector.stage_outbox(items: [{ externalId: "FA-1", payload: { invoiceNumber: "FA-1" } }])
    client.connector.list_outbox(status: "blocked", limit: 10, offset: 20)
    client.connector.get_outbox_item("outbox-1")
    client.connector.send_outbox_item("outbox-1", force: true)
    client.connector.send_outbox_batch(ids: ["outbox-1"], force: true)
    client.connector.cancel_outbox_item("outbox-1")

    expect(stage_stub).to have_been_requested
    expect(list_stub).to have_been_requested
    expect(detail_stub).to have_been_requested
    expect(send_stub).to have_been_requested
    expect(batch_stub).to have_been_requested
    expect(cancel_stub).to have_been_requested
  end
end
