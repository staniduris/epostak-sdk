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

  let(:firm_scoped_client) do
    EPostak::Client.new(client_id: "sk_live_test", client_secret: "secret", base_url: base_url, firm_id: "firm-1")
  end

  def firm_header(request)
    pair = request.headers.find { |key, _value| key.casecmp("X-Firm-Id").zero? }
    pair && pair.last
  end

  def expect_without_firm_header(method, url)
    expect(WebMock).to have_requested(method, url).with { |request| firm_header(request).nil? }
  end

  def expect_with_firm_header(method, url)
    expect(WebMock).to have_requested(method, url).with { |request| firm_header(request) == "firm-1" }
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
    stub = stub_request(:post, "#{host}/connector/autopilot")
      .with { |request|
        firm_header(request).nil? &&
          request.body.include?('"customerRef":"erp-customer-1"') &&
          request.body.include?('"mode":"stage"')
      }
      .to_return(
        status: 201,
        body: { "autopilotId" => "auto-1", "lifecycleStatus" => "staged" }.to_json,
        headers: { "Content-Type" => "application/json" },
      )

    firm_scoped_client.enterprise.connector.customers.for_customer("erp-customer-1").submit_document(
      externalId: "FA-1",
      idempotencyKey: "erp-fa-1",
      payload: { invoiceNumber: "FA-1" },
    )

    expect(stub).to have_been_requested
  end

  it "scopes Connector customer sync with Ruby keyword names" do
    stub = stub_request(:get, "#{host}/connector/sync")
      .with(query: hash_including("customerRef" => "erp-customer-1", "limit" => "50"))
      .to_return(status: 200, body: { "ok" => true }.to_json, headers: { "Content-Type" => "application/json" })

    firm_scoped_client.enterprise.connector.customers.for_customer("erp-customer-1").sync(limit: 50)

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
    expect_without_firm_header(:get, "#{host}/connector/reconcile")
    expect_without_firm_header(:get, "#{host}/connector/mailbox")
    expect_without_firm_header(:post, "#{host}/connector/mailbox/repair")
    expect_without_firm_header(:patch, "#{host}/connector/mailbox/erp-customer-1/send-policy")
    expect_without_firm_header(:get, "#{host}/connector/sync")
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

    firm_scoped_client.connector.preflight(invoiceNumber: "FA-1")
    firm_scoped_client.connector.send_document(invoiceNumber: "FA-1")
    firm_scoped_client.connector.inbox(limit: 20)
    firm_scoped_client.connector.events(limit: 20)

    expect_with_firm_header(:post, "#{host}/connector/preflight")
    expect_with_firm_header(:post, "#{host}/connector/send")
    expect_with_firm_header(:get, "#{host}/connector/inbox")
    expect_with_firm_header(:get, "#{host}/connector/events")
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

    client.connector.autopilot(
      customerRef: "erp-customer-1",
      mode: "shadow",
      externalId: "ERP-FA-2026-001",
      idempotencyKey: "erp-fa-2026-001",
      payload: { receiverPeppolId: "0245:1234567890", invoiceNumber: "FA-2026-001" },
    )
    client.connector.get_autopilot_run("auto-1")
    client.connector.send_autopilot_run("auto-1")
    client.connector.reconcile(status: "exceptions", since: "2026-06-01T00:00:00.000Z")

    expect(autopilot_stub).to have_been_requested
    expect(detail_stub).to have_been_requested
    expect(send_stub).to have_been_requested
    expect(reconcile_stub).to have_been_requested
  end

  it "uses managed Connector v2 paths" do
    mapper_stub = stub_request(:post, "#{host}/connector/mapper")
      .with(body: /pohoda-csv-v1/)
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

    client.connector.customers.for_customer("erp-customer-1").mapper(templateKey: "pohoda-csv-v1", sourceType: "csv", sourceText: "Doklad")
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
