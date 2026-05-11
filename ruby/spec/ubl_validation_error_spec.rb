# frozen_string_literal: true

require "spec_helper"

RSpec.describe EPostak::UblValidationError do
  let(:body) do
    {
      "error" => {
        "code"    => "UBL_VALIDATION_ERROR",
        "message" => "Schematron rule BR-06 violated",
        "rule"    => "BR-06",
      },
    }
  end

  it "is a subclass of EPostak::Error" do
    expect(described_class.superclass).to eq(EPostak::Error)
  end

  it "exposes the rule attribute" do
    err = described_class.new(422, body)
    expect(err.rule).to eq("BR-06")
  end

  it "exposes the request_id from headers" do
    err = described_class.new(422, body, { "x-request-id" => "req-xyz" })
    expect(err.request_id).to eq("req-xyz")
  end

  it "has status 422" do
    err = described_class.new(422, body)
    expect(err.status).to eq(422)
  end

  it "handles ruleId key as alternative" do
    b = { "error" => { "code" => "UBL_VALIDATION_ERROR", "message" => "x", "ruleId" => "BR-02" } }
    err = described_class.new(422, b)
    expect(err.rule).to eq("BR-02")
  end

  it "raises via build_api_error on 422 + UBL_VALIDATION_ERROR" do
    err = EPostak.build_api_error(422, body)
    expect(err).to be_a(EPostak::UblValidationError)
  end

  it "does NOT raise UblValidationError for 422 with a different code" do
    other_body = { "error" => { "code" => "VALIDATION_ERROR", "message" => "bad" } }
    err = EPostak.build_api_error(422, other_body)
    expect(err).to be_a(EPostak::Error)
    expect(err).not_to be_a(EPostak::UblValidationError)
  end

  describe "UBL_RULES constant" do
    it "is frozen" do
      expect(EPostak::UblValidationError::UBL_RULES).to be_frozen
    end

    it "contains 7 entries" do
      expect(EPostak::UblValidationError::UBL_RULES.size).to eq(7)
    end
  end
end
