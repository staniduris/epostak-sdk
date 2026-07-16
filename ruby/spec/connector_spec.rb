# frozen_string_literal: true

require "spec_helper"
require "uri"

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

  let(:firm_scoped_client) do
    EPostak::Client.new(client_id: "sk_live_test", client_secret: "secret", base_url: base_url, firm_id: "firm-1")
  end

  def firm_header(request)
    pair = request.headers.find { |key, _value| key.casecmp("X-Firm-Id").zero? }
    pair && pair.last
  end

  def expect_without_firm_header(method, url, query: nil)
    matcher = have_requested(method, url)
    matcher = matcher.with(query: query) unless query.nil?
    expect(WebMock).to matcher.with { |request| firm_header(request).nil? }
  end

  def expect_with_firm_header(method, url, query: nil)
    matcher = have_requested(method, url)
    matcher = matcher.with(query: query) unless query.nil?
    expect(WebMock).to matcher.with { |request| firm_header(request) == "firm-1" }
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

  it "exposes Enterprise resources under client.enterprise" do
    expect(client.enterprise.documents).to equal(client.documents)
    expect(client.enterprise.inbox).to equal(client.documents.inbox)
    expect(client.enterprise.pull.inbound).to equal(client.inbound)
    expect(client.enterprise.pull.outbound).to equal(client.outbound)
    expect(client.enterprise.connector).to equal(client.connector)
    expect(client.enterprise.box).to equal(client.box)
    expect(client.enterprise.webhooks).to equal(client.webhooks)
    expect(client.connector.advanced.documents).to equal(client.connector.documents)
    customer = client.connector.customers.for_customer("erp-customer-1")
    expect(customer.mailbox).to equal(customer.advanced.mailbox)
    expect { client.connector.customers.for_customer("  ") }
      .to raise_error(ArgumentError, /customerRef is required/)
  end

  it "manages one global Connector webhook without X-Firm-Id" do
    get_stub = stub_request(:get, "#{host}/connector/webhook")
      .to_return(status: 200, body: { "webhook" => { "id" => "wh-1", "url" => "https://erp.example/hook", "events" => ["document.received"], "active" => true, "failedAttempts" => 0, "createdAt" => "2026-07-15T10:00:00Z", "updatedAt" => "2026-07-15T10:00:00Z" } }.to_json,
                 headers: { "Content-Type" => "application/json" })
    configure_stub = stub_request(:put, "#{host}/connector/webhook")
      .with(body: {
        url: "https://erp.example/hook",
        events: ["document.received", "document.delivered"],
      }.to_json)
      .to_return(status: 201, body: { "webhook" => { "id" => "wh-1", "url" => "https://erp.example/hook", "events" => ["document.received"], "active" => true, "failedAttempts" => 0, "createdAt" => "2026-07-15T10:00:00Z", "updatedAt" => "2026-07-15T10:00:00Z" }, "secret" => "a" * 64 }.to_json,
                 headers: { "Content-Type" => "application/json" })
    rotate_stub = stub_request(:post, "#{host}/connector/webhook/rotate-secret")
      .to_return(status: 200, body: { "secret" => "whsec_new" }.to_json,
                 headers: { "Content-Type" => "application/json" })
    test_stub = stub_request(:post, "#{host}/connector/webhook/test")
      .with(body: { customerRef: "erp-customer-1" }.to_json)
      .to_return(
        status: 202,
        body: {
          "deliveryId" => "whd-1",
          "status" => "queued",
          "event" => {
            "id" => "evt-1", "type" => "document.delivered", "customerRef" => "erp-customer-1",
            "documentId" => "doc-1", "state" => "delivered", "occurredAt" => "2026-07-15T10:00:00Z",
            "data" => { "customerRef" => "erp-customer-1", "direction" => "outbound", "type" => "invoice", "number" => nil, "response" => nil },
            "test" => true,
          },
        }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    deliveries_stub = stub_request(:get, "#{host}/connector/webhook/deliveries")
      .with(query: { "cursor" => "next", "limit" => "25", "status" => "FAILED" })
      .to_return(status: 200, body: { "deliveries" => [], "nextCursor" => nil, "hasMore" => false }.to_json,
                 headers: { "Content-Type" => "application/json" })
    delete_stub = stub_request(:delete, "#{host}/connector/webhook")
      .to_return(status: 204)

    webhook = firm_scoped_client.connector.webhook
    current = webhook.get
    configured = webhook.configure(
      "  https://erp.example/hook  ",
      ["document.received", "document.delivered"],
    )
    webhook.rotate_secret
    delivery = webhook.test("\u00A0erp-customer-1\uFEFF")
    webhook.deliveries(cursor: "next", limit: 25, status: "failed")
    webhook.delete

    expect(current.dig("webhook", "id")).to eq("wh-1")
    expect(configured["secret"]).to eq("a" * 64)
    expect(delivery["deliveryId"]).to eq("whd-1")
    expect(delivery.dig("event", "customerRef")).to eq("erp-customer-1")
    expect(delivery.dig("event", "data", "customerRef")).to eq("erp-customer-1")
    expect(delivery.dig("event", "data")).to have_key("number")
    expect(delivery.dig("event", "data", "number")).to be_nil
    expect(delivery.dig("event", "data")).to have_key("response")
    expect(delivery.dig("event", "data", "response")).to be_nil
    expect(delivery.dig("event", "test")).to be(true)
    expect(get_stub).to have_been_requested
    expect(configure_stub).to have_been_requested
    expect(rotate_stub).to have_been_requested
    expect(test_stub).to have_been_requested
    expect(deliveries_stub).to have_been_requested
    expect(delete_stub).to have_been_requested
    expect_without_firm_header(:get, "#{host}/connector/webhook")
    expect_without_firm_header(:put, "#{host}/connector/webhook")
    expect_without_firm_header(:post, "#{host}/connector/webhook/rotate-secret")
    expect_without_firm_header(:post, "#{host}/connector/webhook/test")
    expect_without_firm_header(
      :get,
      "#{host}/connector/webhook/deliveries",
      query: { "cursor" => "next", "limit" => "25", "status" => "FAILED" },
    )
    expect_without_firm_header(:delete, "#{host}/connector/webhook")

    expect { webhook.configure("  ") }.to raise_error(ArgumentError, /webhook URL is required/)
    expect { webhook.test("  ") }.to raise_error(ArgumentError, /customerRef is required/)
  end

  it "debugs and replays Connector webhooks without X-Firm-Id" do
    detail_stub = stub_request(:get, "#{host}/connector/webhook/deliveries/delivery%201")
      .to_return(status: 200, body: {}.to_json, headers: { "Content-Type" => "application/json" })
    replay_stub = stub_request(:post, "#{host}/connector/webhook/deliveries/delivery%201/replay")
      .with(headers: { "Idempotency-Key" => "replay-key" })
      .to_return(status: 202, body: {}.to_json, headers: { "Content-Type" => "application/json" })
    suite_stub = stub_request(:post, "#{host}/connector/webhook/test-suite")
      .with(headers: { "Idempotency-Key" => "suite-key" })
      .to_return(status: 202, body: {}.to_json, headers: { "Content-Type" => "application/json" })
    status_stub = stub_request(:get, "#{host}/connector/webhook/test-suite/run%201")
      .to_return(status: 200, body: {}.to_json, headers: { "Content-Type" => "application/json" })

    webhook = firm_scoped_client.connector.webhook
    webhook.get_delivery("delivery 1")
    webhook.replay_delivery("delivery 1", "replay-key")
    webhook.run_test_suite("erp-acme", "suite-key")
    webhook.get_test_suite("run 1")

    expect(detail_stub).to have_been_requested
    expect(replay_stub).to have_been_requested
    expect(suite_stub).to have_been_requested
    expect(status_stub).to have_been_requested
    expect_without_firm_header(:post, "#{host}/connector/webhook/deliveries/delivery%201/replay")
    expect_without_firm_header(:post, "#{host}/connector/webhook/test-suite")
  end

  it "keeps customer events tenant-scoped while advanced legacy events stay firm-scoped" do
    customer_events_stub = stub_request(:get, "#{host}/connector/events")
      .with(query: { "customerRef" => "erp-customer-1", "limit" => "10" })
      .with { |request| firm_header(request).nil? }
      .to_return(status: 200, body: { "events" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    legacy_events_stub = stub_request(:get, "#{host}/connector/events")
      .with(query: { "limit" => "10" })
      .with { |request| firm_header(request) == "firm-1" }
      .to_return(status: 200, body: { "events" => [] }.to_json, headers: { "Content-Type" => "application/json" })

    firm_scoped_client.connector.customers.for_customer("erp-customer-1").events.list(limit: 10)
    firm_scoped_client.connector.advanced.events(limit: 10)

    expect(customer_events_stub).to have_been_requested.once
    expect(legacy_events_stub).to have_been_requested.once
  end

  it "uses public Box API paths" do
    list_stub = stub_request(:get, "#{host}/box/items")
      .with(query: hash_including("status" => "ready", "direction" => "outbound", "limit" => "10", "offset" => "5"))
      .to_return(status: 200, body: { "items" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    create_stub = stub_request(:post, "#{host}/box/items")
      .with(body: {
        payloadXml: "<Invoice/>",
        scheduledFor: "2026-07-01T00:00:00.000Z",
        externalId: "erp-doc-1",
        metadata: { source: "sdk-test" },
      }.to_json)
      .to_return(status: 201, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    detail_stub = stub_request(:get, "#{host}/box/items/box-1")
      .to_return(status: 200, body: { "boxItemId" => "box-1" }.to_json, headers: { "Content-Type" => "application/json" })
    schedule_stub = stub_request(:post, "#{host}/box/items/box-1/schedule")
      .with(body: { scheduledFor: "2026-07-01T00:00:00.000Z" }.to_json)
      .to_return(status: 200, body: { "boxItemId" => "box-1" }.to_json, headers: { "Content-Type" => "application/json" })
    send_now_stub = stub_request(:post, "#{host}/box/items/box-1/send-now")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    retry_stub = stub_request(:post, "#{host}/box/items/box-1/retry")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    cancel_stub = stub_request(:post, "#{host}/box/items/box-1/cancel")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })

    client.box.list(status: "ready", direction: "outbound", limit: 10, offset: 5)
    client.box.create(
      payload_xml: "<Invoice/>",
      scheduled_for: "2026-07-01T00:00:00.000Z",
      external_id: "erp-doc-1",
      metadata: { source: "sdk-test" },
    )
    client.box.get("box-1")
    client.box.schedule("box-1", scheduled_for: "2026-07-01T00:00:00.000Z")
    client.box.send_now("box-1")
    client.box.retry("box-1")
    client.box.cancel("box-1")

    expect(list_stub).to have_been_requested
    expect(create_stub).to have_been_requested
    expect(detail_stub).to have_been_requested
    expect(schedule_stub).to have_been_requested
    expect(send_now_stub).to have_been_requested
    expect(retry_stub).to have_been_requested
    expect(cancel_stub).to have_been_requested
  end

  it "submits customer-scoped Connector documents without X-Firm-Id" do
    stub = stub_request(:post, "#{host}/connector/documents")
      .with { |request|
        firm_header(request).nil? &&
          request.headers["Idempotency-Key"] == "connector:v1:f7be06badbccd0670a25e6df7fd654fd45ae7291d5f5043257806adc0b107045" &&
          request.body.include?('"customerRef":"erp-customer-1"') &&
          request.body.include?('"delivery":"send"') &&
          !request.body.downcase.include?("peppol")
      }
      .to_return(
        status: 201,
        body: { "id" => "doc-1", "state" => "queued" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    firm_scoped_client.connector.customers.for_customer("erp-customer-1").documents.send({
      externalId: "FA-1",
      type: "invoice",
      number: "FA-1",
      recipient: { country: "SK", taxId: "2120123456" },
      lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
    })

    expect(stub).to have_been_requested
  end

  it "wires customer stage, filtered list, and server-idempotent invoice response" do
    retry_client = EPostak::Client.new(
      client_id: "sk_live_test",
      client_secret: "secret",
      base_url: base_url,
      firm_id: "firm-1",
      max_retries: 1,
    )
    stage_body = {
      "externalId" => "FA-STAGE-1",
      "type" => "invoice",
      "number" => "FA-STAGE-1",
      "recipient" => { "country" => "SK", "taxId" => "2120123456" },
      "lines" => [{ "description" => "Licence", "quantity" => 1, "unitPrice" => 100, "vatRate" => 23 }],
      "customerRef" => "erp-customer-1",
      "delivery" => "stage",
    }
    stage_stub = stub_request(:post, "#{host}/connector/documents")
      .with { |request|
        firm_header(request).nil? &&
          request.headers["Idempotency-Key"] == "connector-stage-key" &&
          JSON.parse(request.body) == stage_body
      }
      .to_return(
        status: 201,
        body: { id: "doc-stage-1", state: "queued" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    list_stub = stub_request(:get, "#{host}/connector/documents")
      .with(query: {
        "customerRef" => "erp-customer-1",
        "direction" => "inbound",
        "state" => "received",
        "type" => "invoice",
        "createdAfter" => "2026-07-01T00:00:00Z",
        "cursor" => "cur-1",
        "limit" => "25",
      })
      .with { |request| firm_header(request).nil? }
      .to_return(
        status: 200,
        body: { documents: [], nextCursor: "cur-2", hasMore: true }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    response_bodies = []
    respond_stub = stub_request(:post, "#{host}/connector/documents/doc-in-1/respond")
      .with(query: { "customerRef" => "erp-customer-1" })
      .with { |request|
        response_bodies << request.body
        firm_header(request).nil? &&
          request.headers["Idempotency-Key"].nil? &&
          JSON.parse(request.body) == {
            "status" => "accepted",
            "note" => "Imported into ERP",
          }
      }
      .to_return(
        {
          status: 503,
          body: { error: { code: "temporary", message: "retry" } }.to_json,
          headers: { "Content-Type" => "application/json", "Retry-After" => "0" },
        },
        {
          status: 200,
          body: {
            id: "doc-in-1",
            customerRef: "erp-customer-1",
            response: {
              status: "accepted",
              direction: "sent",
              delivery: "queued",
              respondedAt: "2026-07-15T12:00:00Z",
            },
            idempotent: true,
          }.to_json,
          headers: { "Content-Type" => "application/json" },
        },
      )

    documents = retry_client.connector.customers.for_customer("erp-customer-1").documents
    documents.stage(
      {
        externalId: "FA-STAGE-1",
        type: "invoice",
        number: "FA-STAGE-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      },
      idempotency_key: "connector-stage-key",
    )
    page = documents.list(
      direction: "inbound",
      state: "received",
      type: "invoice",
      created_after: "2026-07-01T00:00:00Z",
      cursor: "cur-1",
      limit: 25,
    )
    result = documents.respond("doc-in-1", { status: "accepted", note: "Imported into ERP" })

    expect(stage_stub).to have_been_requested.once
    expect(list_stub).to have_been_requested.once
    expect(respond_stub).to have_been_requested.twice
    expect(response_bodies.uniq).to contain_exactly({ status: "accepted", note: "Imported into ERP" }.to_json)
    expect(page).to include("nextCursor" => "cur-2", "hasMore" => true)
    expect(result.dig("response", "status")).to eq("accepted")
    expect(result.dig("response", "direction")).to eq("sent")
    expect(result.dig("response", "delivery")).to eq("queued")
    expect(result["idempotent"]).to be(true)
  end

  it "keeps the customer submit_document compatibility alias on staged Autopilot" do
    stub = stub_request(:post, "#{host}/connector/autopilot")
      .with { |request|
        body = JSON.parse(request.body)
        firm_header(request).nil? &&
          body["customerRef"] == "erp-customer-1" &&
          body["mode"] == "stage"
      }
      .to_return(status: 202, body: { "documentId" => "doc-1", "status" => "staged" }.to_json,
                 headers: { "Content-Type" => "application/json" })
    input = { payload: { number: "FA-1" }, externalId: "legacy-1" }

    firm_scoped_client.connector.customers.for_customer("erp-customer-1").submit_document(input)

    expect(input).to eq(payload: { number: "FA-1" }, externalId: "legacy-1")
    expect(stub).to have_been_requested
  end

  it "normalizes string-key reserved fields without duplicate JSON keys" do
    stub = stub_request(:post, "#{host}/connector/documents")
      .with { |request|
        parsed = JSON.parse(request.body)
        request.body.scan(/\"customerRef\"/).length == 1 &&
          request.body.scan(/\"externalId\"/).length == 1 &&
          request.body.scan(/\"delivery\"/).length == 1 &&
          parsed["customerRef"] == "zákazník😀" &&
          parsed["externalId"] == "FA-žltý-1" &&
          parsed["delivery"] == "send"
      }
      .to_return(status: 201, body: { "id" => "doc-1" }.to_json,
                 headers: { "Content-Type" => "application/json" })

    firm_scoped_client.connector.customers.for_customer("\u00A0\uFEFFzákazník😀\uFEFF\u00A0").documents.send({
      "externalId" => "\uFEFFFA-žltý-1\u00A0",
      "customerRef" => "zákazník😀",
      "delivery" => "stage",
      "number" => "FA-1",
      "recipient" => { "country" => "SK", "taxId" => "2120123456" },
      "lines" => [{ "description" => "Licence", "quantity" => 1, "unitPrice" => 1, "vatRate" => 23 }],
    })

    expect(stub).to have_been_requested
  end

  it "derives bounded collision-safe Connector idempotency keys" do
    keys = [
      EPostak::Resources.connector_document_idempotency_key("a:b", "c"),
      EPostak::Resources.connector_document_idempotency_key("a", "b:c"),
      EPostak::Resources.connector_document_idempotency_key("c" * 255, "e" * 255),
      EPostak::Resources.connector_document_idempotency_key("\u00A0\uFEFFzákazník😀\uFEFF\u00A0", "\uFEFFFA-žltý-1\u00A0"),
      EPostak::Resources.connector_document_idempotency_key("\u0085zákazník😀\u0085", "\u0085FA-žltý-1\u0085"),
    ]
    expect(keys).to eq([
      "connector:v1:540e8f1c5ae653a7d7e2fe88f7eb8dcabea924d661b1542ad191bb1848e0c33d",
      "connector:v1:e482a79a788392ccae4952360dd438820641e4c162b4952b42d35e78260d70be",
      "connector:v1:7182fd43682e0689adf34c908bc3ec162aaf1687c167fdbff714ff43daa4b111",
      "connector:v1:eec0ca654af898913432fbc7b7441a05080f72099f6d2ff85852f78c7458fdfd",
      "connector:v1:ff49689a9ece4c0319420ed07fc3a2a5b2e2e7bb6d4430a68557e372fdf70080",
    ])
    expect(keys.map(&:length)).to all(eq(77))
    expect(keys[0]).not_to eq(keys[1])

    explicit_stub = stub_request(:post, "#{host}/connector/documents")
      .with(headers: { "Idempotency-Key" => "caller-key" })
      .to_return(status: 201, body: { "id" => "doc-1" }.to_json, headers: { "Content-Type" => "application/json" })
    client.connector.customers.for_customer("customer").documents.send(
      {
        externalId: "external",
        type: "invoice",
        number: "FA-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 1, vatRate: 23 }],
      },
      idempotency_key: "caller-key",
    )
    expect(explicit_stub).to have_been_requested

    expect do
      client.connector.customers.for_customer("customer").documents.send(
        {
          externalId: "external-empty-key",
          type: "invoice",
          number: "FA-2",
          recipient: { country: "SK", taxId: "2120123456" },
          lines: [{ description: "Licence", quantity: 1, unitPrice: 1, vatRate: 23 }],
        },
        idempotency_key: "",
      )
    end.to raise_error(ArgumentError, /idempotency key must be 1-255 UTF-8 bytes/i)
  end

  it "retries keyed and lifecycle POSTs but surfaces 409 once" do
    retry_client = EPostak::Client.new(
      client_id: "sk_live_test",
      client_secret: "secret",
      base_url: base_url,
      max_retries: 1,
    )
    payload = {
      externalId: "FA-retry",
      type: "invoice",
      number: "FA-retry",
      recipient: { country: "SK", taxId: "2120123456" },
      lines: [{ description: "Original", quantity: 1, unitPrice: 1, vatRate: 23 }],
    }
    attempts = 0
    bodies = []
    stub_request(:post, "#{host}/connector/documents").to_return do |request|
      attempts += 1
      bodies << request.body
      payload[:lines][0][:description] = "Mutated after first attempt" if attempts == 1
      if attempts == 1
        {
          status: 503,
          body: { error: { code: "temporary", message: "retry" } }.to_json,
          headers: {
            "Content-Type" => "application/json",
            "Retry-After" => "Wed, 21 Oct 2015 07:28:00 GMT",
          },
        }
      else
        {
          status: 201,
          body: { id: "doc-1", state: "queued" }.to_json,
          headers: { "Content-Type" => "application/json" },
        }
      end
    end

    result = retry_client.connector.customers.for_customer("customer").documents.send(payload)

    expect(result["id"]).to eq("doc-1")
    expect(attempts).to eq(2)
    expect(bodies.uniq.length).to eq(1)
    expect(bodies.first).to include("Original")
    expect(bodies.first).not_to include("Mutated after first attempt")

    lifecycle_stub = stub_request(:post, "#{host}/connector/documents/doc-retry/cancel")
      .with(query: { "customerRef" => "customer" })
      .to_return(
        {
          status: 503,
          body: { error: { code: "temporary", message: "retry" } }.to_json,
          headers: { "Content-Type" => "application/json", "Retry-After" => "0" },
        },
        {
          status: 200,
          body: { id: "doc-retry", state: "cancelled" }.to_json,
          headers: { "Content-Type" => "application/json" },
        },
      )
    retry_client.connector.customers.for_customer("customer").documents.cancel_document("doc-retry")
    expect(lifecycle_stub).to have_been_requested.twice

    ubl_retry_stub = stub_request(:get, "#{host}/connector/documents/doc-retry/ubl")
      .with(query: { "customerRef" => "customer" })
      .with { |request| firm_header(request).nil? }
      .to_return(
        {
          status: 503,
          body: { error: { code: "temporary", message: "retry" } }.to_json,
          headers: { "Content-Type" => "application/json", "Retry-After" => "0" },
        },
        {
          status: 200,
          body: "<Invoice/>",
          headers: { "Content-Type" => "application/xml" },
        },
      )
    expect(retry_client.connector.customers.for_customer("customer").advanced.documents.ubl("doc-retry"))
      .to eq("<Invoice/>")
    expect(ubl_retry_stub).to have_been_requested.twice

    conflict_stub = stub_request(:post, "#{host}/connector/documents")
      .with(headers: { "Idempotency-Key" => "conflict-key" })
      .to_return(
        status: 409,
        body: { error: { code: "idempotency_in_flight", message: "busy", retryable: true } }.to_json,
        headers: { "Content-Type" => "application/json", "Retry-After" => "0" },
      )
    expect do
      retry_client.connector.customers.for_customer("customer").documents.send(
        payload.merge(externalId: "FA-conflict"),
        idempotency_key: "conflict-key",
      )
    end.to raise_error(EPostak::Error) { |error| expect(error.status).to eq(409) }
    expect(conflict_stub).to have_been_requested.once
  end

  it "retries Connector transport failures but surfaces SAPI mutations once" do
    retry_client = EPostak::Client.new(
      client_id: "sk_live_test",
      client_secret: "secret",
      base_url: base_url,
      firm_id: "firm-1",
      max_retries: 1,
    )
    connector_attempts = []
    connector_stub = stub_request(:post, "#{host}/connector/documents")
      .to_return do |request|
        connector_attempts << [
          request.uri.to_s,
          request.body,
          request.headers["Idempotency-Key"],
          firm_header(request),
        ]
        raise Faraday::ConnectionFailed, "socket reset" if connector_attempts.length == 1

        {
          status: 201,
          body: { id: "doc-transport", state: "queued" }.to_json,
          headers: { "Content-Type" => "application/json" },
        }
      end

    retry_client.connector.customers.for_customer("erp-customer-1").documents.stage(
      {
        externalId: "FA-TRANSPORT-1",
        type: "invoice",
        number: "FA-TRANSPORT-1",
        recipient: { country: "SK", taxId: "2120123456" },
        lines: [{ description: "Licence", quantity: 1, unitPrice: 100, vatRate: 23 }],
      },
      idempotency_key: "connector-transport-key",
    )

    expect(connector_stub).to have_been_requested.twice
    expect(connector_attempts.length).to eq(2)
    expect(connector_attempts.uniq.length).to eq(1)
    expect(URI.parse(connector_attempts.first[0])).to have_attributes(
      scheme: "https",
      host: "epostak.sk",
      port: 443,
      path: "/api/v1/connector/documents",
      query: nil,
    )
    expect(connector_attempts.first[1]).to include('"customerRef":"erp-customer-1"')
    expect(connector_attempts.first[1]).to include('"delivery":"stage"')
    expect(connector_attempts.first[2]).to eq("connector-transport-key")
    expect(connector_attempts.first[3]).to be_nil

    sapi_attempts = 0
    sapi_stub = stub_request(:post, "https://epostak.sk/sapi/v1/document/send")
      .to_return do
        sapi_attempts += 1
        raise Faraday::ConnectionFailed, "socket reset"
      end

    expect do
      retry_client.sapi.participants.for_participant("0245:1234567890").documents.send(
        { xml: "<Invoice/>" },
        idempotency_key: "sapi-transport-key",
      )
    end.to raise_error(EPostak::Error) { |error| expect(error.status).to eq(0) }
    expect(sapi_stub).to have_been_requested.once
    expect(sapi_attempts).to eq(1)
  end

  it "preserves document.cancelled business events" do
    stub_request(:get, "#{host}/connector/events")
      .with(query: hash_including("customerRef" => "customer"))
      .to_return(
        status: 200,
        body: {
          events: [{ id: "evt-1", customerRef: "customer", type: "document.cancelled", documentId: "doc-1", state: "cancelled", data: { customerRef: "customer", direction: "outbound", type: "invoice", number: nil, response: nil } }],
          nextCursor: nil,
        }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    result = client.connector.customers.for_customer("customer").events.list

    expect(result.dig("events", 0, "type")).to eq("document.cancelled")
    expect(result.dig("events", 0, "data")).to have_key("response")
    expect(result.dig("events", 0, "data", "response")).to be_nil
  end

  it "sends and cancels staged customer documents without a request body" do
    send_stub = stub_request(:post, "#{host}/connector/documents/doc-1/send")
      .with(query: { "customerRef" => "erp-customer-1" })
      .with { |request| request.body.to_s.empty? && firm_header(request).nil? }
      .to_return(status: 200, body: { "id" => "doc-1", "state" => "queued" }.to_json, headers: { "Content-Type" => "application/json" })
    cancel_stub = stub_request(:post, "#{host}/connector/documents/doc-2/cancel")
      .with(query: { "customerRef" => "erp-customer-1" })
      .with { |request| request.body.to_s.empty? && firm_header(request).nil? }
      .to_return(status: 200, body: { "id" => "doc-2", "state" => "cancelled" }.to_json, headers: { "Content-Type" => "application/json" })

    documents = firm_scoped_client.connector.customers.for_customer("erp-customer-1").documents
    expect { documents.send_document("  ") }.to raise_error(ArgumentError, /documentId is required/)
    documents.send_document("doc-1")
    documents.cancel_document("doc-2")

    expect(send_stub).to have_been_requested
    expect(cancel_stub).to have_been_requested
  end

  it "binds every customer point operation and artifact to customerRef" do
    customer_ref = "customer A/1"
    document_id = "customer-b-doc"
    observed_queries = []
    json = { "id" => document_id }.to_json

    [
      [:get, ""],
      [:post, "/acknowledge"],
      [:post, "/send"],
      [:post, "/cancel"],
      [:get, "/evidence"],
      [:get, "/evidence-bundle"],
      [:get, "/support-packet"],
    ].each do |method, suffix|
      stub_request(method, "#{host}/connector/documents/#{document_id}#{suffix}")
        .with(query: { "customerRef" => customer_ref })
        .with { |request|
          suffix != "/acknowledge" || JSON.parse(request.body) == { "reference" => "ERP-ACK-1" }
        }
        .to_return do |request|
          observed_queries << request.uri.query
          { status: 200, body: json, headers: { "Content-Type" => "application/json" } }
        end
    end
    stub_request(:get, "#{host}/connector/documents/#{document_id}/ubl")
      .with(query: { "customerRef" => customer_ref })
      .to_return do |request|
        observed_queries << request.uri.query
        { status: 200, body: "<Invoice/>", headers: { "Content-Type" => "application/xml" } }
      end

    customer = firm_scoped_client.connector.customers.for_customer(customer_ref)
    customer.documents.get(document_id)
    customer.documents.acknowledge(document_id, "ERP-ACK-1")
    customer.documents.send_document(document_id)
    customer.documents.cancel_document(document_id)
    customer.advanced.documents.ubl(document_id)
    customer.advanced.documents.evidence(document_id)
    customer.advanced.documents.evidence_bundle(document_id)
    customer.advanced.documents.support_packet(document_id)

    expect(observed_queries.length).to eq(8)
    expect(observed_queries.map { |query| URI.decode_www_form(query) })
      .to all(eq([["customerRef", customer_ref]]))
  end

  it "scopes Connector customer sync with Ruby keyword names" do
    stub = stub_request(:get, "#{host}/connector/sync")
      .with(query: hash_including("customerRef" => "erp-customer-1", "limit" => "50"))
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })

    firm_scoped_client.connector.customers.for_customer("erp-customer-1").sync(limit: 50)

    expect(stub).to have_been_requested
  end

  it "scopes SAPI document calls through participant documents" do
    stub = stub_request(:post, "https://epostak.sk/sapi/v1/document/send")
      .with(headers: {
        "X-Peppol-Participant-Id" => "0245:1234567890",
        "Idempotency-Key" => "sapi-fa-1",
      })
      .to_return(
        status: 201,
        body: { "documentId" => "sapi-1", "status" => "accepted" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    client.sapi.participants.for_participant("0245:1234567890").documents.send(
      { xml: "<Invoice/>" },
      idempotency_key: "sapi-fa-1",
    )

    expect(stub).to have_been_requested
  end

  it "posts Peppol capabilities with participant envelope" do
    stub = stub_request(:post, "#{host}/peppol/capabilities")
      .with(
        body: {
          participant: { scheme: "0245", identifier: "2020305606" },
          documentType: "urn:invoice",
        }.to_json,
      )
      .to_return(status: 200, body: { "found" => true, "accepts" => true }.to_json, headers: { "Content-Type" => "application/json" })

    client.peppol.capabilities(scheme: "0245", identifier: "2020305606", document_type: "urn:invoice")

    expect(stub).to have_been_requested
  end

  it "omits X-Firm-Id on managed Connector v2 calls even when client is firm-scoped" do
    stub_request(:post, "#{host}/connector/zen-input")
      .to_return(status: 201, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:post, "#{host}/connector/autopilot")
      .to_return(status: 201, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/autopilot/auto-1")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:post, "#{host}/connector/autopilot/auto-1/send")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/reconcile")
      .with(query: hash_including("status" => "exceptions"))
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/mailbox")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:post, "#{host}/connector/mailbox/repair")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:patch, "#{host}/connector/mailbox/erp-customer-1/send-policy")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/sync")
      .with(query: hash_including("customerRef" => "erp-customer-1", "limit" => "50"))
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/documents/doc-1")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/documents/doc-1/ubl")
      .to_return(status: 200, body: "<Invoice/>", headers: { "Content-Type" => "application/xml" })
    stub_request(:get, "#{host}/connector/documents/doc-1/evidence")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/documents/doc-1/evidence-bundle")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:post, "#{host}/connector/actions/action-1")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:post, "#{host}/connector/mapper")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })

    firm_scoped_client.connector.mapper(templateKey: "pohoda-csv-v1", sourceType: "csv", sourceText: "Doklad")
    firm_scoped_client.connector.zen_input(customerRef: "erp-customer-1")
    firm_scoped_client.connector.autopilot(customerRef: "erp-customer-1")
    firm_scoped_client.connector.get_autopilot_run("auto-1")
    firm_scoped_client.connector.send_autopilot_run("auto-1")
    firm_scoped_client.connector.reconcile(status: "exceptions")
    firm_scoped_client.connector.mailboxes
    firm_scoped_client.connector.repair_mailbox(customerRef: "erp-customer-1")
    firm_scoped_client.connector.update_mailbox_send_policy("erp-customer-1", policy: "daily_batch")
    firm_scoped_client.connector.sync(customer_ref: "erp-customer-1", limit: 50)
    firm_scoped_client.connector.get_document("doc-1")
    firm_scoped_client.connector.get_document_ubl("doc-1")
    firm_scoped_client.connector.get_document_evidence("doc-1")
    firm_scoped_client.connector.get_document_evidence_bundle("doc-1")
    firm_scoped_client.connector.run_action("action-1", note: "send now")

    expect_without_firm_header(:post, "#{host}/connector/mapper")
    expect_without_firm_header(:post, "#{host}/connector/zen-input")
    expect_without_firm_header(:post, "#{host}/connector/autopilot")
    expect_without_firm_header(:get, "#{host}/connector/autopilot/auto-1")
    expect_without_firm_header(:post, "#{host}/connector/autopilot/auto-1/send")
    expect_without_firm_header(
      :get,
      "#{host}/connector/reconcile",
      query: { "status" => "exceptions" },
    )
    expect_without_firm_header(:get, "#{host}/connector/mailbox")
    expect_without_firm_header(:post, "#{host}/connector/mailbox/repair")
    expect_without_firm_header(:patch, "#{host}/connector/mailbox/erp-customer-1/send-policy")
    expect_without_firm_header(
      :get,
      "#{host}/connector/sync",
      query: { "customerRef" => "erp-customer-1", "limit" => "50" },
    )
    expect_without_firm_header(:get, "#{host}/connector/documents/doc-1")
    expect_without_firm_header(:get, "#{host}/connector/documents/doc-1/ubl")
    expect_without_firm_header(:get, "#{host}/connector/documents/doc-1/evidence")
    expect_without_firm_header(:get, "#{host}/connector/documents/doc-1/evidence-bundle")
    expect_without_firm_header(:post, "#{host}/connector/actions/action-1")
  end

  it "keeps X-Firm-Id on legacy Connector calls for firm-scoped clients" do
    stub_request(:post, "#{host}/connector/preflight")
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:post, "#{host}/connector/send")
      .to_return(status: 201, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/inbox")
      .with(query: hash_including("limit" => "20"))
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    stub_request(:get, "#{host}/connector/events")
      .with(query: hash_including("limit" => "20"))
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })

    firm_scoped_client.connector.preflight({ invoiceNumber: "FA-1" })
    firm_scoped_client.connector.send_document({ invoiceNumber: "FA-1" })
    firm_scoped_client.connector.inbox(limit: 20)
    firm_scoped_client.connector.events(limit: 20)

    expect_with_firm_header(:post, "#{host}/connector/preflight")
    expect_with_firm_header(:post, "#{host}/connector/send")
    expect_with_firm_header(:get, "#{host}/connector/inbox", query: { "limit" => "20" })
    expect_with_firm_header(:get, "#{host}/connector/events", query: { "limit" => "20" })
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

  it "uses Connector Autopilot and reconcile paths" do
    autopilot_stub = stub_request(:post, "#{host}/connector/autopilot")
      .with(body: /ERP-FA-2026-001/)
      .to_return(
        status: 201,
        body: { "autopilotId" => "auto-1", "mode" => "shadow", "lifecycleStatus" => "shadow_validated" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )
    detail_stub = stub_request(:get, "#{host}/connector/autopilot/auto-1")
      .to_return(status: 200, body: { "autopilotId" => "auto-1" }.to_json, headers: { "Content-Type" => "application/json" })
    send_stub = stub_request(:post, "#{host}/connector/autopilot/auto-1/send")
      .with(body: "{}")
      .to_return(status: 200, body: { "autopilotId" => "auto-1", "lifecycleStatus" => "sent" }.to_json, headers: { "Content-Type" => "application/json" })
    reconcile_stub = stub_request(:get, "#{host}/connector/reconcile")
      .with(query: hash_including("status" => "exceptions", "since" => "2026-06-01T00:00:00.000Z"))
      .to_return(status: 200, body: { "status" => "exceptions", "items" => [] }.to_json, headers: { "Content-Type" => "application/json" })

    client.connector.advanced.autopilot(
      customerRef: "erp-customer-1",
      mode: "shadow",
      externalId: "ERP-FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: { receiverPeppolId: "0245:1234567890", invoiceNumber: "FA-2026-001" },
    )
    client.connector.advanced.get_autopilot_run("auto-1")
    client.connector.advanced.send_autopilot_run("auto-1")
    client.connector.advanced.reconcile(status: "exceptions", since: "2026-06-01T00:00:00.000Z")

    expect(autopilot_stub).to have_been_requested
    expect(detail_stub).to have_been_requested
    expect(send_stub).to have_been_requested
    expect(reconcile_stub).to have_been_requested
  end

  it "uses managed Connector v2 paths" do
    mapper_stub = stub_request(:post, "#{host}/connector/mapper")
      .with { |request|
        body = JSON.parse(request.body)
        body["templateKey"] == "pohoda-csv-v1" &&
          body["customerRef"] == "erp-customer-1" &&
          body["execute"] == "preview"
      }
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })
    zen_stub = stub_request(:post, "#{host}/connector/zen-input")
      .with(body: /erp-customer-1/)
      .to_return(status: 201, body: { "autopilotId" => "auto-1" }.to_json, headers: { "Content-Type" => "application/json" })
    mailboxes_stub = stub_request(:get, "#{host}/connector/mailbox")
      .to_return(status: 200, body: { "mailboxes" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    repair_stub = stub_request(:post, "#{host}/connector/mailbox/repair")
      .with(body: /erp-customer-1/)
      .to_return(status: 200, body: { "repaired" => true }.to_json, headers: { "Content-Type" => "application/json" })
    policy_stub = stub_request(:patch, "#{host}/connector/mailbox/erp-customer-1/send-policy")
      .with(body: /daily_batch/)
      .to_return(status: 200, body: { "mailbox" => {} }.to_json, headers: { "Content-Type" => "application/json" })
    sync_stub = stub_request(:get, "#{host}/connector/sync")
      .with(query: hash_including("customerRef" => "erp-customer-1", "cursor" => "cur-1", "limit" => "50"))
      .to_return(status: 200, body: { "items" => [], "hasMore" => false }.to_json, headers: { "Content-Type" => "application/json" })
    document_stub = stub_request(:get, "#{host}/connector/documents/doc-1")
      .to_return(status: 200, body: { "documentId" => "doc-1" }.to_json, headers: { "Content-Type" => "application/json" })
    ubl_stub = stub_request(:get, "#{host}/connector/documents/doc-1/ubl")
      .to_return(status: 200, body: "<Invoice/>", headers: { "Content-Type" => "application/xml" })
    evidence_stub = stub_request(:get, "#{host}/connector/documents/doc-1/evidence")
      .to_return(status: 200, body: { "events" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    bundle_stub = stub_request(:get, "#{host}/connector/documents/doc-1/evidence-bundle")
      .to_return(status: 200, body: { "bundle" => [] }.to_json, headers: { "Content-Type" => "application/json" })
    action_stub = stub_request(:post, "#{host}/connector/actions/action-1")
      .with(body: /send now/)
      .to_return(status: 200, body: { "action" => {} }.to_json, headers: { "Content-Type" => "application/json" })

    client.connector.customers.for_customer("erp-customer-1").advanced.mapper(templateKey: "pohoda-csv-v1", sourceType: "csv", sourceText: "Doklad")
    expect do
      client.connector.customers.for_customer("erp-customer-1").advanced.mapper(execute: "send")
    end.to raise_error(ArgumentError, /only supports preview normalization/)
    client.connector.zen_input(customerRef: "erp-customer-1", invoiceNumber: "FA-2026-002", mode: "stage")
    client.connector.mailboxes
    client.connector.repair_mailbox(customerRef: "erp-customer-1")
    client.connector.update_mailbox_send_policy("erp-customer-1", policy: "daily_batch")
    client.connector.sync(customer_ref: "erp-customer-1", cursor: "cur-1", limit: 50)
    client.connector.get_document("doc-1")
    expect(client.connector.get_document_ubl("doc-1")).to eq("<Invoice/>")
    client.connector.get_document_evidence("doc-1")
    client.connector.get_document_evidence_bundle("doc-1")
    client.connector.run_action("action-1", note: "send now")

    expect(mapper_stub).to have_been_requested
    expect(zen_stub).to have_been_requested
    expect(mailboxes_stub).to have_been_requested
    expect(repair_stub).to have_been_requested
    expect(policy_stub).to have_been_requested
    expect(sync_stub).to have_been_requested
    expect(document_stub).to have_been_requested
    expect(ubl_stub).to have_been_requested
    expect(evidence_stub).to have_been_requested
    expect(bundle_stub).to have_been_requested
    expect(action_stub).to have_been_requested
  end
end
