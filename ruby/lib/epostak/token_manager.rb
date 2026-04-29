# frozen_string_literal: true

require "faraday"
require "json"

module EPostak
  # Thread-safe OAuth JWT token manager. Mints tokens via
  # +POST /sapi/v1/auth/token+ and auto-refreshes 60 s before expiry.
  #
  # @api private
  class TokenManager
    # @param client_id [String] OAuth client ID (the sk_live_* or sk_int_* key)
    # @param client_secret [String] OAuth client secret
    # @param base_url [String] API base URL (e.g. https://epostak.sk/api/v1)
    # @param firm_id [String, nil] Optional firm UUID for integrator keys
    def initialize(client_id:, client_secret:, base_url:, firm_id: nil)
      @client_id     = client_id
      @client_secret = client_secret
      @base_url      = base_url
      @firm_id       = firm_id
      @mutex         = Mutex.new

      @access_token  = nil
      @refresh_token = nil
      @expires_at    = nil

      # Derive SAPI base: https://epostak.sk/api/v1 -> https://epostak.sk
      @sapi_base = base_url.sub(%r{/api/v1\z}, "")
    end

    # Returns a valid JWT access token, minting or refreshing as needed.
    #
    # @return [String] JWT access token
    def access_token
      @mutex.synchronize do
        if @access_token && @expires_at && Time.now < (@expires_at - 60)
          return @access_token
        end

        if @refresh_token && @expires_at && Time.now < @expires_at
          refresh!
        else
          mint!
        end

        @access_token
      end
    end

    private

    def mint!
      body = {
        grant_type: "client_credentials",
        client_id: @client_id,
        client_secret: @client_secret
      }

      conn = Faraday.new(url: @sapi_base) do |f|
        f.adapter Faraday.default_adapter
      end

      response = conn.post("/sapi/v1/auth/token") do |req|
        req.headers["Content-Type"] = "application/json"
        req.headers["X-Firm-Id"] = @firm_id if @firm_id
        req.body = JSON.generate(body)
      end

      handle_token_response(response)
    end

    def refresh!
      body = {
        grant_type: "refresh_token",
        refresh_token: @refresh_token
      }

      conn = Faraday.new(url: @sapi_base) do |f|
        f.adapter Faraday.default_adapter
      end

      response = conn.post("/sapi/v1/auth/renew") do |req|
        req.headers["Content-Type"] = "application/json"
        req.headers["Authorization"] = "Bearer #{@access_token}"
        req.headers["X-Firm-Id"] = @firm_id if @firm_id
        req.body = JSON.generate(body)
      end

      handle_token_response(response)
    rescue StandardError
      # If refresh fails, fall back to full mint
      mint!
    end

    def handle_token_response(response)
      unless response.success?
        error_body = begin
          JSON.parse(response.body)
        rescue StandardError
          { "error" => response.reason_phrase || "Token request failed" }
        end
        raise Error.new(response.status, error_body, response.headers)
      end

      data = JSON.parse(response.body)
      @access_token  = data["access_token"]
      @refresh_token = data["refresh_token"]
      @expires_at    = Time.now + (data["expires_in"] || 900).to_i
    end
  end
end
