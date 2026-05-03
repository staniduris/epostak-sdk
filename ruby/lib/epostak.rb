# frozen_string_literal: true

require_relative "epostak/version"
require_relative "epostak/error"
require_relative "epostak/token_manager"
require_relative "epostak/http_client"
require_relative "epostak/webhook_signature"
require_relative "epostak/oauth"
require_relative "epostak/client"
require_relative "epostak/resources/auth"
require_relative "epostak/resources/audit"
require_relative "epostak/resources/documents"
require_relative "epostak/resources/inbox"
require_relative "epostak/resources/firms"
require_relative "epostak/resources/integrator"
require_relative "epostak/resources/peppol"
require_relative "epostak/resources/peppol_directory"
require_relative "epostak/resources/webhooks"
require_relative "epostak/resources/webhook_queue"
require_relative "epostak/resources/reporting"
require_relative "epostak/resources/account"
require_relative "epostak/resources/extract"

# Top-level namespace for the ePošťák Ruby SDK.
#
# @example Quick start
#   client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "secret")
#   result = client.documents.send_document(
#     receiverPeppolId: "0245:1234567890",
#     items: [{ description: "Consulting", quantity: 10, unitPrice: 100, vatRate: 23 }]
#   )
#
# @example Verify a webhook
#   result = EPostak.verify_webhook_signature(
#     payload: request.body.read,
#     signature_header: request.env["HTTP_X_EPOSTAK_SIGNATURE"].to_s,
#     secret: ENV["EPOSTAK_WEBHOOK_SECRET"]
#   )
#   halt 400 unless result[:valid]
module EPostak
  # Default base URL for the ePošťák API.
  DEFAULT_BASE_URL = "https://epostak.sk/api/v1"

  # Default base URL for the public (non-authenticated) ePošťák API.
  # Used by the public {.validate} endpoint which requires no API key.
  DEFAULT_PUBLIC_BASE_URL = "https://epostak.sk/api"

  # Validate a UBL XML document against the Peppol BIS 3.0 3-layer rules.
  #
  # This endpoint is *public* — no API key is required. Rate-limited to
  # 20 requests per minute per IP.
  #
  # @param xml [String] UBL 2.1 XML invoice or credit note as a string
  # @param base_url [String, nil] Optional override for the public API base
  #   URL. Defaults to +https://epostak.sk/api+.
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
      raise EPostak.build_api_error(response.status, body, response.headers)
    end

    return nil if response.body.nil? || response.body.empty?

    begin
      JSON.parse(response.body)
    rescue JSON::ParserError
      response.body
    end
  end
end
