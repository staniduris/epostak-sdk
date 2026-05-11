# frozen_string_literal: true

require "spec_helper"

RSpec.describe "Data classes" do
  describe EPostak::InboundDocument do
    let(:hash) do
      {
        "id"             => "in-1",
        "kind"           => "invoice",
        "senderPeppolId" => "0245:9876543210",
        "senderName"     => "ACME s.r.o.",
        "receivedAt"     => "2026-05-10T08:00:00Z",
        "clientAckedAt"  => nil,
        "status"         => "received",
      }
    end

    it "builds from a string-keyed hash" do
      doc = described_class.from_hash(hash)
      expect(doc.id).to eq("in-1")
      expect(doc.kind).to eq("invoice")
      expect(doc.sender_peppol_id).to eq("0245:9876543210")
      expect(doc.sender_name).to eq("ACME s.r.o.")
    end
  end

  describe EPostak::OutboundDocument do
    let(:hash) do
      {
        "id"                => "out-1",
        "kind"              => "invoice",
        "invoiceNumber"     => "FV-2026-001",
        "recipientPeppolId" => "0245:1234567890",
        "status"            => "DELIVERED",
        "sentAt"            => "2026-05-09T12:00:00Z",
      }
    end

    it "builds from a string-keyed hash" do
      doc = described_class.from_hash(hash)
      expect(doc.id).to eq("out-1")
      expect(doc.invoice_number).to eq("FV-2026-001")
      expect(doc.recipient_peppol_id).to eq("0245:1234567890")
      expect(doc.status).to eq("DELIVERED")
    end
  end

  describe EPostak::OutboundEvent do
    let(:hash) do
      {
        "id"          => "ev-1",
        "document_id" => "out-1",
        "type"        => "sent",
        "actor"       => "system",
        "occurred_at" => "2026-05-09T12:01:00Z",
      }
    end

    it "builds from a string-keyed hash" do
      ev = described_class.from_hash(hash)
      expect(ev.id).to eq("ev-1")
      expect(ev.document_id).to eq("out-1")
      expect(ev.type).to eq("sent")
      expect(ev.occurred_at).to eq("2026-05-09T12:01:00Z")
    end
  end

  describe EPostak::WebhookDelivery do
    let(:hash) do
      {
        "id"             => "del-1",
        "webhookId"      => "wh-1",
        "event"          => "document.received",
        "status"         => "SUCCESS",
        "statusCode"     => 200,
        "createdAt"      => "2026-05-10T08:00:00Z",
        "idempotencyKey" => "idem-abc",
      }
    end

    it "populates idempotency_key" do
      del = described_class.from_hash(hash)
      expect(del.idempotency_key).to eq("idem-abc")
    end

    it "allows nil idempotency_key" do
      h = hash.merge("idempotencyKey" => nil)
      del = described_class.from_hash(h)
      expect(del.idempotency_key).to be_nil
    end

    it "populates core delivery fields" do
      del = described_class.from_hash(hash)
      expect(del.id).to eq("del-1")
      expect(del.event).to eq("document.received")
      expect(del.status).to eq("SUCCESS")
      expect(del.status_code).to eq(200)
    end
  end
end
