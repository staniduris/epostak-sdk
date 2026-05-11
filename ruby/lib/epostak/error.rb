# frozen_string_literal: true

module EPostak
  # Error raised when an ePošťák API request fails.
  #
  # The SDK normalizes both the legacy `{ error: { code, message, details } }`
  # envelope and the RFC 7807 `application/problem+json` envelope
  # (`{ type, title, status, detail, instance }`) into the same shape.
  #
  # @example Catching API errors
  #   begin
  #     client.documents.send_document(body)
  #   rescue EPostak::Error => e
  #     puts "HTTP #{e.status} (#{e.code}): #{e.message}"
  #     puts "Required scope: #{e.required_scope}" if e.required_scope
  #     puts "Request id: #{e.request_id}" if e.request_id
  #   end
  class Error < StandardError
    # @return [Integer] HTTP status code (e.g. 400, 401, 404, 500). 0 for network errors.
    attr_reader :status

    # @return [String, nil] Machine-readable error code from the API
    #   (e.g. `"VALIDATION_ERROR"`, `"idempotency_conflict"`,
    #   `"insufficient_scope"`).
    attr_reader :code

    # @return [Object, nil] Additional error details, typically a list of
    #   field-level validation messages or schematron rule IDs.
    attr_reader :details

    # @return [String, nil] RFC 7807 `type` — URI reference identifying the
    #   problem type. Only set when the response carried `application/problem+json`.
    attr_reader :type

    # @return [String, nil] RFC 7807 `title` — short, human-readable summary.
    attr_reader :title

    # @return [String, nil] RFC 7807 `detail` — explanation of this specific
    #   occurrence.
    attr_reader :detail

    # @return [String, nil] RFC 7807 `instance` — URI reference identifying
    #   this specific occurrence.
    attr_reader :instance

    # @return [String, nil] Server-assigned request ID — populated from
    #   `X-Request-Id` header or `requestId` body field whenever the server
    #   returns one.
    attr_reader :request_id

    # @return [String, nil] Required OAuth scope when the server rejects with
    #   `403 insufficient_scope`. Parsed from the
    #   `WWW-Authenticate: Bearer error="insufficient_scope" scope="..."`
    #   response header. `nil` when the header is absent or the rejection was
    #   for a different reason.
    attr_reader :required_scope

    # @param status [Integer] HTTP status code from the response (or 0 for
    #   network errors).
    # @param body [Hash] Parsed JSON error body from the API.
    # @param headers [Hash, nil] Optional response headers — used to extract
    #   `X-Request-Id` and to parse `WWW-Authenticate` for `required_scope`.
    def initialize(status, body = {}, headers = nil)
      @status = status
      body ||= {}
      headers ||= {}

      # Detect RFC 7807 `application/problem+json` envelope.
      problem = body.is_a?(Hash) &&
                (string_key?(body, "title") || string_key?(body, "detail")) &&
                fetch_key(body, "error").nil?

      msg = "API request failed"

      if problem
        msg = string_value(body, "title") || string_value(body, "detail") || msg
        @code = string_value(body, "code")
        errors = fetch_key(body, "errors")
        @details = errors unless errors.nil?
      else
        error_obj = fetch_key(body, "error")
        if error_obj.is_a?(Hash)
          msg = string_value(error_obj, "message") || msg
          @code = string_value(error_obj, "code")
          @details = fetch_key(error_obj, "details")
        elsif error_obj.is_a?(String)
          msg = error_obj
        else
          msg = string_value(body, "message") || msg
        end
      end

      # RFC 7807 fields — copy through verbatim when present.
      @type     = string_value(body, "type")
      @title    = string_value(body, "title")
      @detail   = string_value(body, "detail")
      @instance = string_value(body, "instance")

      @request_id = extract_request_id(body, headers)
      @required_scope = extract_required_scope(body, headers)

      super(msg)
    end

    private

    def fetch_key(hash, key)
      return nil unless hash.is_a?(Hash)

      hash[key] || hash[key.to_sym]
    end

    def string_key?(hash, key)
      !fetch_key(hash, key).nil?
    end

    def string_value(hash, key)
      v = fetch_key(hash, key)
      v.is_a?(String) ? v : nil
    end

    def extract_request_id(body, headers)
      body_id = string_value(body, "requestId")
      return body_id if body_id

      err = fetch_key(body, "error")
      if err.is_a?(Hash)
        nested = string_value(err, "requestId")
        return nested if nested
      end

      header_value(headers, "x-request-id")
    end

    def extract_required_scope(body, headers)
      raw = header_value(headers, "www-authenticate")
      if raw.is_a?(String) && raw.match?(/error\s*=\s*"?insufficient_scope/i)
        m = raw.match(/scope\s*=\s*"([^"]+)"/i)
        return m[1] if m
      end

      body_scope = string_value(body, "required_scope")
      return body_scope if body_scope

      err = fetch_key(body, "error")
      string_value(err, "required_scope") if err.is_a?(Hash)
    end

    def header_value(headers, name)
      return nil if headers.nil?

      lowered = name.downcase
      headers.each do |k, v|
        return v if k.to_s.downcase == lowered
      end
      nil
    end
  end

  # Raised when POST /api/v1/documents/send (or the dashboard create
  # endpoint) rejects an outbound invoice whose +invoice_number+ already
  # exists for the firm.
  #
  # The conflict key is +(firmId, invoiceNumber)+ — recipient is
  # intentionally NOT part of it; outbound numbering belongs to the sender.
  #
  # @example
  #   begin
  #     client.documents.send_document(invoiceNumber: "2026001", ...)
  #   rescue EPostak::DuplicateInvoiceNumberError => e
  #     existing = e.existing_document
  #     warn "Already sent on #{existing[:sent_at]}, id=#{existing[:id]}" if existing
  #   end
  class DuplicateInvoiceNumberError < Error
    # @return [Array<String>] Always +["firmId", "invoiceNumber"]+.
    attr_reader :conflict_key

    # @return [Hash, nil] The pre-existing outbound invoice that triggered
    #   the conflict, or +nil+ if it was deleted between the constraint hit
    #   and the server-side lookup. Keys: +:id+, +:invoice_number+,
    #   +:status+, +:sent_at+ (ISO 8601), +:recipient+ ({+:peppol_id+,
    #   +:ico+, +:name+} or nil).
    attr_reader :existing_document

    def initialize(status, body = {}, headers = nil)
      super
      body ||= {}
      error_obj = (body["error"] || body[:error])
      error_obj = {} unless error_obj.is_a?(Hash)

      ck = error_obj["conflictKey"] || error_obj[:conflictKey]
      @conflict_key =
        if ck.is_a?(Array)
          ck.map(&:to_s)
        else
          %w[firmId invoiceNumber]
        end

      ed = error_obj["existingDocument"] || error_obj[:existingDocument]
      @existing_document =
        if ed.is_a?(Hash)
          recipient_raw = ed["recipient"] || ed[:recipient]
          recipient =
            if recipient_raw.is_a?(Hash)
              {
                peppol_id: recipient_raw["peppolId"] || recipient_raw[:peppolId],
                ico: recipient_raw["ico"] || recipient_raw[:ico],
                name: recipient_raw["name"] || recipient_raw[:name],
              }
            end
          {
            id: (ed["id"] || ed[:id] || "").to_s,
            invoice_number: (ed["invoiceNumber"] || ed[:invoiceNumber] || "").to_s,
            status: (ed["status"] || ed[:status] || "").to_s,
            sent_at: (ed["sentAt"] || ed[:sentAt] || "").to_s,
            recipient: recipient,
          }
        end
    end
  end

  # Raised when the API rejects a submitted UBL document with code
  # +UBL_VALIDATION_ERROR+ (HTTP 422). The +rule+ attribute identifies
  # which schematron rule fired.
  #
  # @example
  #   begin
  #     client.documents.send_document(xml: raw_ubl)
  #   rescue EPostak::UblValidationError => e
  #     puts "UBL rule violated: #{e.rule} (request #{e.request_id})"
  #   end
  class UblValidationError < Error
    # Known schematron rule codes returned by the server.
    UBL_RULES = %w[
      BR-01
      BR-02
      BR-04
      BR-CL-01
      BR-S-08
      PEPPOL-EN16931-R004
      PEPPOL-EN16931-R010
    ].freeze

    # @return [String, nil] The schematron rule code that triggered the error.
    attr_reader :rule

    def initialize(status, body = {}, headers = nil)
      super
      body ||= {}
      error_obj = (body["error"] || body[:error])
      error_obj = {} unless error_obj.is_a?(Hash)
      @rule = (error_obj["rule"] || error_obj[:rule] ||
               error_obj["ruleId"] || error_obj[:ruleId])&.to_s
    end
  end

  # Build the right Error subclass from a parsed API error body. Falls
  # back to {Error} when no specialised mapping applies.
  #
  # @param status [Integer]
  # @param body [Hash]
  # @param headers [Hash, nil]
  # @return [Error]
  def self.build_api_error(status, body = {}, headers = nil)
    error_obj = body.is_a?(Hash) ? (body["error"] || body[:error]) : nil
    code = error_obj.is_a?(Hash) ? (error_obj["code"] || error_obj[:code]) : nil
    return DuplicateInvoiceNumberError.new(status, body, headers) if code == "DUPLICATE_INVOICE_NUMBER"
    return UblValidationError.new(status, body, headers) if status == 422 && code == "UBL_VALIDATION_ERROR"

    Error.new(status, body, headers)
  end
end
