# frozen_string_literal: true

require "faraday"
require "json"

module EPostak
  module Resources
    # Sub-resource for managing the per-key IP allowlist (Wave 3.1).
    #
    # An empty list means "no IP restriction" — any caller IP is accepted.
    # When the list is non-empty, requests authenticated with this key are
    # rejected (`403`) unless the source IP matches at least one entry. Each
    # entry is either a bare IPv4/IPv6 address or a CIDR block (`addr/prefix`).
    #
    # Accessed via +client.auth.ip_allowlist+.
    #
    # @example Read the current allowlist
    #   resp = client.auth.ip_allowlist.get
    #   puts resp["ip_allowlist"]
    #
    # @example Replace the allowlist (max 50 entries)
    #   client.auth.ip_allowlist.update(cidrs: ["192.168.1.0/24", "203.0.113.42"])
    class IpAllowlist
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Read the current IP allowlist for the calling API key.
      #
      # @return [Hash] { "ip_allowlist" => [...] }
      def get
        @http.request(:get, "/auth/ip-allowlist")
      end

      # Replace the IP allowlist for the calling API key. Pass an empty array
      # to clear the restriction. Maximum 50 entries; each must be either a
      # bare IP or a valid CIDR.
      #
      # @param cidrs [Array<String>] List of IPs / CIDR blocks
      # @return [Hash] { "ip_allowlist" => [...] }
      def update(cidrs:)
        @http.request(:put, "/auth/ip-allowlist", body: { ip_allowlist: cidrs })
      end
    end

    # Resource for the OAuth `client_credentials` flow and key management.
    #
    # The token mint endpoint accepts the API key as the `client_secret` of
    # an OAuth `client_credentials` exchange. ePošťák returns a short-lived
    # JWT access token (`expires_in: 900` seconds) and a 30-day rotating
    # refresh token. Use {#token} once at startup, cache the access token,
    # and call {#renew} before it expires.
    #
    # Accessed via +client.auth+. Replaces the v1
    # +client.account.status+ / +client.account.rotate_secret+ helpers.
    #
    # @example
    #   client = EPostak::Client.new(api_key: "sk_live_xxxxx")
    #
    #   tokens = client.auth.token(api_key: "sk_live_xxxxx")
    #   puts tokens["access_token"], tokens["expires_in"]
    #
    #   # Later, before the access token expires:
    #   renewed = client.auth.renew(refresh_token: tokens["refresh_token"])
    #
    #   # On logout / key rotation:
    #   client.auth.revoke(token: tokens["refresh_token"], token_type_hint: "refresh_token")
    class Auth
      # @return [Resources::IpAllowlist] Sub-resource for the per-key IP allowlist
      attr_reader :ip_allowlist

      # @param http [EPostak::HttpClient] Internal HTTP client
      # @param base_url [String] API base URL (used for unauthenticated token mint)
      def initialize(http, base_url:)
        @http = http
        @base_url = base_url
        @ip_allowlist = IpAllowlist.new(http)
      end

      # Mint an OAuth access token via the `client_credentials` grant.
      #
      # The API key is sent as both the `Authorization: Bearer` header and the
      # `client_secret` body field — the server accepts either, but doubling
      # up keeps the SDK compatible across spec revisions. For integrator keys
      # (`sk_int_*`) you must also pass `firm_id`, which is forwarded as
      # `X-Firm-Id` so the issued JWT is bound to the right tenant.
      #
      # @param api_key [String] The `sk_live_*` or `sk_int_*` key to exchange
      # @param firm_id [String, nil] Required for integrator keys
      # @param scope [String, nil] Optional space-separated scope subset
      # @return [Hash] Access + refresh token pair (access_token, refresh_token,
      #   token_type, expires_in, scope)
      #
      # @example sk_live_* — direct firm access
      #   tokens = client.auth.token(api_key: "sk_live_xxxxx")
      #
      # @example sk_int_* — integrator acting on behalf of a managed firm
      #   tokens = client.auth.token(api_key: "sk_int_xxxxx", firm_id: "client-firm-uuid")
      def token(api_key:, firm_id: nil, scope: nil)
        body = {
          grant_type: "client_credentials",
          client_id: api_key,
          client_secret: api_key
        }
        body[:scope] = scope if scope

        conn = Faraday.new(url: @base_url) do |f|
          f.adapter Faraday.default_adapter
          f.headers["Authorization"] = "Bearer #{api_key}"
          f.headers["X-Firm-Id"] = firm_id if firm_id
        end

        response = conn.run_request(:post, "/auth/token", nil, nil) do |req|
          req.headers["Content-Type"] = "application/json"
          req.body = JSON.generate(body)
        end

        unless response.success?
          error_body = begin
            JSON.parse(response.body)
          rescue StandardError
            { "error" => response.reason_phrase || "Request failed" }
          end
          raise Error.new(response.status, error_body, response.headers)
        end

        return nil if response.body.nil? || response.body.empty?

        JSON.parse(response.body)
      rescue Faraday::Error => e
        raise Error.new(0, { "error" => e.message })
      end

      # Exchange a refresh token for a new access + refresh pair. The old
      # refresh token is invalidated server-side, so always replace your
      # stored refresh token with the value returned by this call.
      #
      # @param refresh_token [String] The currently-valid refresh token
      # @return [Hash] New access + refresh token pair
      #
      # @example
      #   renewed = client.auth.renew(refresh_token: stored)
      #   stored  = renewed["refresh_token"]
      def renew(refresh_token:)
        @http.request(:post, "/auth/renew", body: {
          grant_type: "refresh_token",
          refresh_token: refresh_token
        })
      end

      # Revoke an access or refresh token. Idempotent — a 200 is returned
      # even if the token is unknown or already revoked, so this is safe to
      # call unconditionally on logout. Pass `token_type_hint` when you know
      # which variant the token is; the server will skip the auto-detect path.
      #
      # @param token [String] The token to revoke
      # @param token_type_hint [String, nil] One of `"access_token"` /
      #   `"refresh_token"`
      # @return [Hash] Confirmation envelope (`{ "revoked" => true }`)
      #
      # @example
      #   client.auth.revoke(token: stored_refresh, token_type_hint: "refresh_token")
      def revoke(token:, token_type_hint: nil)
        body = { token: token }
        body[:token_type_hint] = token_type_hint if token_type_hint
        @http.request(:post, "/auth/revoke", body: body)
      end

      # Introspect the calling API key without revealing the plaintext secret.
      # Returns the key metadata, the firm it is bound to, the current plan,
      # the per-key rate limit, and — for integrator keys — the integrator
      # summary.
      #
      # In v2 this endpoint is **GET** (was POST in v1).
      #
      # @return [Hash] { "key" => {id,name,prefix,permissions,active,createdAt,
      #   lastUsedAt}, "firm" => {id,peppolStatus}, "plan" =>
      #   {name,expiresAt,active}, "rateLimit" => {perMinute,window},
      #   "integrator" => {id} or nil }
      #
      # @example
      #   status = client.auth.status
      #   puts "#{status['key']['prefix']} on plan #{status['plan']['name']}"
      def status
        @http.request(:get, "/auth/status")
      end

      # Rotate the calling API key. The previous key is deactivated
      # immediately and the new plaintext key is returned ONCE — store it in
      # your secret manager before continuing. Integrator keys (`sk_int_*`)
      # are rejected with 403; rotate those through the integrator dashboard
      # instead.
      #
      # @return [Hash] { "key" => "sk_live_...", "prefix" => "sk_live_xxxx",
      #   "message" => "..." } — `key` shown only once.
      #
      # @example
      #   rotated = client.auth.rotate_secret
      #   ENV["EPOSTAK_API_KEY"] = rotated["key"]
      def rotate_secret
        @http.request(:post, "/auth/rotate-secret")
      end
    end
  end
end
