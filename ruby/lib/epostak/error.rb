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
end
