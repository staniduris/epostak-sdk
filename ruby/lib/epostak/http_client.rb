# frozen_string_literal: true

require "faraday"
require "faraday/multipart"
require "json"

module EPostak
  # Internal HTTP wrapper around Faraday. Handles authentication,
  # JSON serialization, multipart uploads, and error normalization.
  #
  # Not intended for direct use — resource classes delegate here.
  #
  # @api private
  class HttpClient
    # HTTP methods that are safe to retry by default.
    RETRYABLE_METHODS = %i[get delete].freeze

    # @param api_key [String] Bearer token for authentication
    # @param base_url [String] API base URL
    # @param firm_id [String, nil] Optional firm UUID sent as X-Firm-Id header
    # @param max_retries [Integer] Maximum retries on 429/5xx (default: 3)
    def initialize(api_key:, base_url:, firm_id: nil, max_retries: 3)
      @api_key     = api_key
      @base_url    = base_url
      @firm_id     = firm_id
      @max_retries = max_retries

      @conn = build_connection
    end

    # Perform a JSON API request.
    #
    # @param method [Symbol] HTTP method (:get, :post, :patch, :delete)
    # @param path [String] API endpoint path (appended to base_url)
    # @param body [Hash, nil] Request body (serialized as JSON)
    # @param query [Hash, nil] Query parameters
    # @return [Hash, nil] Parsed JSON response, or nil for 204 responses
    # @raise [EPostak::Error] On non-2xx responses or network errors
    def request(method, path, body: nil, query: nil)
      retryable = RETRYABLE_METHODS.include?(method)
      attempt = 0

      loop do
        response = @conn.run_request(method, path, nil, nil) do |req|
          req.params.update(compact_params(query)) if query
          if body
            req.headers["Content-Type"] = "application/json"
            req.body = JSON.generate(body)
          end
        end

        status = response.status

        # Retry on 429 or 5xx for safe methods
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

    # Perform a raw request that returns the response body as a string.
    # Used for PDF and UBL downloads.
    #
    # @param method [Symbol] HTTP method
    # @param path [String] API endpoint path
    # @return [String] Raw response body bytes
    # @raise [EPostak::Error] On non-2xx responses
    def request_raw(method, path)
      response = @conn.run_request(method, path, nil, nil)

      unless response.success?
        error_body = parse_error_body(response)
        raise Error.new(response.status, error_body)
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
      # Build a multipart connection for this request
      multipart_conn = Faraday.new(url: @base_url) do |f|
        f.request :multipart
        f.request :url_encoded
        f.adapter Faraday.default_adapter
        f.headers["Authorization"] = "Bearer #{@api_key}"
        f.headers["X-Firm-Id"] = @firm_id if @firm_id
      end

      payload = {}
      file_parts.each_with_index do |part, idx|
        key = part[:field]
        upload = Faraday::Multipart::FilePart.new(
          part[:io],
          part[:mime_type],
          part[:filename] || "document"
        )
        # If multiple files share the same field name, use array notation
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

    # Calculate the backoff delay for a retry attempt.
    # Uses exponential backoff with jitter: min(base_delay * 2^attempt + jitter, 30s).
    # Respects Retry-After header on 429 responses.
    #
    # @param attempt [Integer] Current attempt number (0-based)
    # @param response [Faraday::Response] The HTTP response
    # @return [Float] Delay in seconds
    def calculate_delay(attempt, response)
      base_delay = 0.5
      max_delay = 30.0

      if response.status == 429
        retry_after = response.headers["Retry-After"] || response.headers["retry-after"]
        if retry_after
          # Try parsing as numeric seconds
          seconds = Float(retry_after, exception: false)
          return [seconds, max_delay].min if seconds

          # Try parsing as HTTP date
          begin
            date = Time.httpdate(retry_after)
            return [[date - Time.now, 0].max, max_delay].min
          rescue ArgumentError
            # Fall through to exponential backoff
          end
        end
      end

      jitter = rand # 0–1s of jitter
      [base_delay * (2**attempt) + jitter, max_delay].min
    end

    # Build the base Faraday connection with auth headers.
    def build_connection
      Faraday.new(url: @base_url) do |f|
        f.adapter Faraday.default_adapter
        f.headers["Authorization"] = "Bearer #{@api_key}"
        f.headers["X-Firm-Id"] = @firm_id if @firm_id
      end
    end

    # Process the Faraday response — parse JSON or return nil for 204.
    #
    # @param response [Faraday::Response]
    # @return [Hash, nil]
    # @raise [EPostak::Error] On non-2xx status
    def handle_response(response)
      unless response.success?
        error_body = parse_error_body(response)
        raise Error.new(response.status, error_body)
      end

      # 204 No Content or empty body
      return nil if response.status == 204 || response.body.nil? || response.body.empty?

      JSON.parse(response.body)
    rescue JSON::ParserError
      # If the body isn't JSON but status is 2xx, return raw string
      response.body
    end

    # Attempt to parse an error response body as JSON.
    #
    # @param response [Faraday::Response]
    # @return [Hash]
    def parse_error_body(response)
      JSON.parse(response.body)
    rescue StandardError
      { "error" => response.reason_phrase || "Request failed" }
    end

    # Remove nil values from a params hash.
    #
    # @param params [Hash, nil]
    # @return [Hash]
    def compact_params(params)
      return {} unless params

      params.compact.transform_values(&:to_s)
    end
  end
end
