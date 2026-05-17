# frozen_string_literal: true

require "faraday"
require "faraday/multipart"
require "json"

module EPostak
  # Carries the last rate-limit information returned by the server.
  # Populated from +X-RateLimit-Limit+, +X-RateLimit-Remaining+, and
  # +X-RateLimit-Reset+ (Unix epoch) response headers.
  RateLimitInfo = Struct.new(:limit, :remaining, :reset_at, keyword_init: true)

  # Internal HTTP wrapper around Faraday. Handles authentication,
  # JSON serialization, multipart uploads, and error normalization.
  #
  # Not intended for direct use — resource classes delegate here.
  #
  # @api private
  class HttpClient
    # HTTP methods that are safe to retry by default.
    RETRYABLE_METHODS = %i[get delete].freeze

    # @return [EPostak::RateLimitInfo, nil] Rate-limit snapshot from the most
    #   recent response. +nil+ before any request has been made.
    attr_reader :last_rate_limit

    # @param token_manager [EPostak::TokenManager] Manages OAuth JWT tokens
    # @param base_url [String] API base URL
    # @param firm_id [String, nil] Optional firm UUID sent as X-Firm-Id header
    # @param max_retries [Integer] Maximum retries on 429/5xx (default: 3)
    def initialize(token_manager:, base_url:, firm_id: nil, max_retries: 3)
      @token_manager   = token_manager
      @base_url        = base_url
      @firm_id         = firm_id
      @max_retries     = max_retries
      @last_rate_limit = nil

      @conn = build_connection
    end

    # Perform a JSON API request.
    #
    # @param method [Symbol] HTTP method (:get, :post, :patch, :delete, :put)
    # @param path [String] API endpoint path (appended to base_url)
    # @param body [Hash, nil] Request body (serialized as JSON)
    # @param query [Hash, nil] Query parameters
    # @param idempotency_key [String, nil] Optional `Idempotency-Key` header value
    # @param retry_on_failure [Boolean, nil] Override default retry policy. When +true+,
    #   non-idempotent methods (POST/PATCH/PUT) become retryable.
    # @return [Hash, nil] Parsed JSON response, or nil for 204 responses
    # @raise [EPostak::Error] On non-2xx responses or network errors
    def request(method, path, body: nil, query: nil, idempotency_key: nil, retry_on_failure: nil, headers: nil, base_url: nil)
      retryable =
        if retry_on_failure.nil?
          RETRYABLE_METHODS.include?(method)
        else
          retry_on_failure
        end
      attempt = 0

      loop do
        conn = base_url ? build_connection(base_url) : @conn
        response = conn.run_request(method, path, nil, nil) do |req|
          req.headers["Authorization"] = "Bearer #{@token_manager.access_token}"
          req.params.update(compact_params(query)) if query
          req.headers["Idempotency-Key"] = idempotency_key if idempotency_key
          req.headers.update(headers) if headers
          if body
            req.headers["Content-Type"] = "application/json"
            req.body = JSON.generate(body)
          end
        end

        status = response.status

        # Retry on 429 or 5xx for retryable requests
        if retryable && attempt < @max_retries && (status == 429 || status >= 500)
          delay = calculate_delay(attempt, response)
          sleep(delay)
          attempt += 1
          next
        end

        return handle_response(response)
      end
    rescue Faraday::Error => e
      raise Error.new(0, { "error" => e.message })
    end

    # Send a raw body (non-JSON) and return the parsed JSON response.
    # Used for endpoints that accept application/xml or another content type.
    #
    # @param method [Symbol] HTTP method (typically :post)
    # @param path [String] API endpoint path
    # @param xml [String] Raw body bytes to send
    # @param content_type [String] Value for the Content-Type header
    # @return [Hash, nil] Parsed JSON response, or nil for 204
    # @raise [EPostak::Error] On non-2xx responses or network errors
    def request_with_body(method, path, xml, content_type: "application/xml")
      response = @conn.run_request(method, path, nil, nil) do |req|
        req.headers["Authorization"] = "Bearer #{@token_manager.access_token}"
        req.headers["Content-Type"] = content_type
        req.body = xml
      end

      handle_response(response)
    rescue Faraday::Error => e
      raise Error.new(0, { "error" => e.message })
    end

    # Perform a raw request that returns the response body as a string.
    # Used for PDF and UBL downloads.
    #
    # @param method [Symbol] HTTP method
    # @param path [String] API endpoint path
    # @return [String] Raw response body bytes
    # @raise [EPostak::Error] On non-2xx responses
    def request_raw(method, path)
      response = @conn.run_request(method, path, nil, nil) do |req|
        req.headers["Authorization"] = "Bearer #{@token_manager.access_token}"
      end

      capture_rate_limit(response.headers)

      unless response.success?
        error_body = parse_error_body(response)
        raise EPostak.build_api_error(response.status, error_body, response.headers)
      end

      response.body
    rescue Faraday::Error => e
      raise Error.new(0, { "error" => e.message })
    end

    # Perform a multipart file upload request.
    #
    # @param path [String] API endpoint path
    # @param file_parts [Array<Hash>] Array of { field:, io:, mime_type:, filename: }
    # @return [Hash, nil] Parsed JSON response
    # @raise [EPostak::Error] On non-2xx responses
    def request_multipart(path, file_parts)
      multipart_conn = Faraday.new(url: @base_url) do |f|
        f.request :multipart
        f.request :url_encoded
        f.adapter Faraday.default_adapter
        f.headers["Authorization"] = "Bearer #{@token_manager.access_token}"
        f.headers["X-Firm-Id"] = @firm_id if @firm_id
      end

      payload = {}
      file_parts.each_with_index do |part, _idx|
        key = part[:field]
        upload = Faraday::Multipart::FilePart.new(
          part[:io],
          part[:mime_type],
          part[:filename] || "document"
        )
        if file_parts.count { |p| p[:field] == key } > 1
          payload["#{key}[]"] ||= []
          payload["#{key}[]"] << upload
        else
          payload[key] = upload
        end
      end

      response = multipart_conn.post(path, payload)
      handle_response(response)
    rescue Faraday::Error => e
      raise Error.new(0, { "error" => e.message })
    end

    private

    def calculate_delay(attempt, response)
      base_delay = 0.5
      max_delay = 30.0

      if response.status == 429
        retry_after = response.headers["Retry-After"] || response.headers["retry-after"]
        if retry_after
          seconds = Float(retry_after, exception: false)
          return [seconds, max_delay].min if seconds

          begin
            date = Time.httpdate(retry_after)
            return [[date - Time.now, 0].max, max_delay].min
          rescue ArgumentError
            # Fall through to exponential backoff
          end
        end
      end

      jitter = rand
      [base_delay * (2**attempt) + jitter, max_delay].min
    end

    def build_connection(base_url = @base_url)
      Faraday.new(url: base_url) do |f|
        f.adapter Faraday.default_adapter
        f.headers["X-Firm-Id"] = @firm_id if @firm_id
      end
    end

    def handle_response(response)
      capture_rate_limit(response.headers)

      unless response.success?
        error_body = parse_error_body(response)
        raise EPostak.build_api_error(response.status, error_body, response.headers)
      end

      return nil if response.status == 204 || response.body.nil? || response.body.empty?

      JSON.parse(response.body)
    rescue JSON::ParserError
      response.body
    end

    def capture_rate_limit(headers)
      return unless headers

      limit_val     = header_value(headers, "x-ratelimit-limit") ||
                      header_value(headers, "x-rate-limit-limit")
      remaining_val = header_value(headers, "x-ratelimit-remaining") ||
                      header_value(headers, "x-rate-limit-remaining")
      reset_val     = header_value(headers, "x-ratelimit-reset") ||
                      header_value(headers, "x-rate-limit-reset")

      return unless limit_val || remaining_val || reset_val

      reset_at =
        if reset_val
          epoch = Integer(reset_val, exception: false)
          epoch ? Time.at(epoch) : nil
        end

      @last_rate_limit = RateLimitInfo.new(
        limit:      limit_val     ? Integer(limit_val, exception: false)     : nil,
        remaining:  remaining_val ? Integer(remaining_val, exception: false) : nil,
        reset_at:   reset_at,
      )
    end

    def header_value(headers, name)
      lowered = name.downcase
      headers.each do |k, v|
        return v if k.to_s.downcase == lowered
      end
      nil
    end

    def parse_error_body(response)
      JSON.parse(response.body)
    rescue StandardError
      { "error" => response.reason_phrase || "Request failed" }
    end

    def compact_params(params)
      return {} unless params

      params.compact.transform_values(&:to_s)
    end
  end
end
