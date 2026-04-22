# frozen_string_literal: true

require_relative "epostak/error"
require_relative "epostak/http_client"
require_relative "epostak/client"
require_relative "epostak/resources/documents"
require_relative "epostak/resources/inbox"
require_relative "epostak/resources/firms"
require_relative "epostak/resources/peppol"
require_relative "epostak/resources/peppol_directory"
require_relative "epostak/resources/webhooks"
require_relative "epostak/resources/webhook_queue"
require_relative "epostak/resources/reporting"
require_relative "epostak/resources/account"
require_relative "epostak/resources/extract"

# Top-level namespace for the ePosťák Enterprise API SDK.
#
# @example Quick start
#   client = EPostak::Client.new(api_key: "sk_live_xxxxx")
#   result = client.documents.send_document(
#     receiverPeppolId: "0245:1234567890",
#     items: [{ description: "Consulting", quantity: 10, unitPrice: 100, vatRate: 23 }]
#   )
module EPostak
  # Default base URL for the ePosťák Enterprise API
  DEFAULT_BASE_URL = "https://epostak.sk/api/enterprise"

  # Default base URL for the public (non-enterprise) ePosťák API.
  # Used by the public {.validate} endpoint which requires no API key.
  DEFAULT_PUBLIC_BASE_URL = "https://epostak.sk/api"

  # Validate a UBL XML document against the Peppol BIS 3.0 3-layer rules.
  #
  # This endpoint is *public* -- no API key is required. Rate-limited to
  # 20 requests per minute per IP.
  #
  # @param xml [String] UBL 2.1 XML invoice or credit note as a string
  # @param base_url [String, nil] Optional override for the public API base URL.
  #   Defaults to +https://epostak.sk/api+.
  # @return [Hash] Full 3-layer Peppol BIS 3.0 validation report
  # @raise [EPostak::Error] On non-2xx responses
  #
  # @example
  #   report = EPostak.validate(File.read("invoice.xml"))
  #   puts "Valid!" if report["valid"]
  def self.validate(xml, base_url: nil)
    require "faraday"
    require "json"

    url = base_url || DEFAULT_PUBLIC_BASE_URL
    conn = Faraday.new(url: url)
    response = conn.post("/validate") do |req|
      req.headers["Content-Type"] = "application/xml"
      req.body = xml
    end

    unless response.success?
      body =
        begin
          JSON.parse(response.body)
        rescue StandardError
          { "error" => response.reason_phrase || "validate request failed" }
        end
      raise Error.new(response.status, body)
    end

    return nil if response.body.nil? || response.body.empty?

    begin
      JSON.parse(response.body)
    rescue JSON::ParserError
      response.body
    end
  end
end
