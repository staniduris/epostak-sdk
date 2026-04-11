# frozen_string_literal: true

module EPostak
  # Error raised when an ePosťák API request fails.
  #
  # Wraps the HTTP status code, machine-readable error code, and any
  # additional details returned by the API.
  #
  # @example Catching API errors
  #   begin
  #     client.documents.send_document(body)
  #   rescue EPostak::Error => e
  #     puts "HTTP #{e.status}: #{e.message}"
  #     puts "Code: #{e.code}" if e.code
  #     puts "Details: #{e.details}" if e.details
  #   end
  class Error < StandardError
    # @return [Integer] HTTP status code (e.g. 400, 401, 404, 500). 0 for network errors.
    attr_reader :status

    # @return [String, nil] Machine-readable error code from the API (e.g. "VALIDATION_ERROR")
    attr_reader :code

    # @return [Object, nil] Additional error details, typically field-level validation messages
    attr_reader :details

    # @param status [Integer] HTTP status code from the response
    # @param body [Hash] Parsed JSON error body from the API
    def initialize(status, body = {})
      @status = status

      error_obj = body["error"] || body[:error]

      # The API returns either { error: "message" } or { error: { message, code, details } }
      if error_obj.is_a?(Hash)
        msg = error_obj["message"] || error_obj[:message] || "API request failed"
        @code = error_obj["code"] || error_obj[:code]
        @details = error_obj["details"] || error_obj[:details]
      elsif error_obj.is_a?(String)
        msg = error_obj
      else
        msg = "API request failed"
      end

      super(msg)
    end
  end
end
