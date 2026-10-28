# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::Error do
  let(:body) do
    {
      "error" => {
        "code" => "DOCUMENT_INVALID",
        "message" => "unitPrice must be a number",
        "retryable" => false,
        "nextAction" => "Correct the indicated document field and retry.",
        "apiCode" => "VALIDATION_ERROR",
        "message_sk" => "Jednotková cena musí byť číslo",
        "fields" => [
          { "path" => "items[0].unitPrice", "message" => "unitPrice must be a number",
            "message_sk" => "Jednotková cena musí byť číslo" },
        ],
        "rule" => "PEPPOL-EN16931-R003",
        "fix_hint" => "Send unitPrice as a number.",
        "fix_hint_sk" => "Pošlite unitPrice ako číslo.",
        "docs" => "https://epostak.sk/api/docs",
        "requestId" => "request-ubl-validation",
      },
    }
  end

  it "exposes the additive error fields" do
    err = described_class.new(422, body)
    expect(err.api_code).to eq("VALIDATION_ERROR")
    expect(err.message_sk).to eq("Jednotková cena musí byť číslo")
    expect(err.fields).to eq([
      { "path" => "items[0].unitPrice", "message" => "unitPrice must be a number",
        "message_sk" => "Jednotková cena musí byť číslo" },
    ])
    expect(err.rule).to eq("PEPPOL-EN16931-R003")
    expect(err.fix_hint).to eq("Send unitPrice as a number.")
    expect(err.fix_hint_sk).to eq("Pošlite unitPrice ako číslo.")
    expect(err.docs).to eq("https://epostak.sk/api/docs")
  end

  it "keeps the existing fields unchanged" do
    err = described_class.new(422, body)
    expect(err.code).to eq("DOCUMENT_INVALID")
    expect(err.message).to eq("unitPrice must be a number")
    expect(err.next_action).to eq("Correct the indicated document field and retry.")
    expect(err.retryable).to eq(false)
    expect(err.request_id).to eq("request-ubl-validation")
  end

  it "returns nil for every new field when absent" do
    err = described_class.new(500, { "error" => { "code" => "X", "message" => "boom" } })
    expect([err.api_code, err.message_sk, err.fields, err.rule,
            err.fix_hint, err.fix_hint_sk, err.docs]).to all(be_nil)
  end

  it "returns nil for wrong-typed values" do
    err = described_class.new(422, { "error" => { "apiCode" => 5, "fields" => "x", "docs" => {}, "rule" => [] } })
    expect([err.api_code, err.fields, err.docs, err.rule]).to all(be_nil)
  end
end
