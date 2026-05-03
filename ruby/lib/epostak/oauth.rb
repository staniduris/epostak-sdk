# frozen_string_literal: true

require "digest"
require "json"
require "net/http"
require "securerandom"
require "uri"

module EPostak
  # Stateless helpers for the **integrator-initiated** OAuth
  # `authorization_code` + PKCE flow. Use these from your own backend when you
  # want to onboard an end-user firm into ePošťák from inside your own
  # application — the user clicks a "Connect ePošťák" button in your UI,
  # lands on the ePošťák `/oauth/authorize` consent page, and ePošťák
  # redirects back to your `redirect_uri` with a `code`.
  #
  # This is independent of the regular `client.auth.token` flow (which uses
  # `client_credentials`). Pick one or the other depending on how the firm is
  # linked to you.
  #
  # The OAuth token endpoint lives at `https://epostak.sk/api/oauth/token` —
  # **not** under `/api/v1` — so this module bypasses the configured
  # {EPostak::Client} base URL.
  #
  # @example
  #   # 1. On every onboarding attempt, generate a fresh PKCE pair.
  #   pair = EPostak::OAuth.generate_pkce
  #   sessions[req.session_id] = pair[:code_verifier]
  #
  #   # 2. Build the authorize URL and redirect the user.
  #   url = EPostak::OAuth.build_authorize_url(
  #     client_id: ENV.fetch("EPOSTAK_OAUTH_CLIENT_ID"),
  #     redirect_uri: "https://your-app.com/oauth/epostak/callback",
  #     code_challenge: pair[:code_challenge],
  #     state: req.session_id,
  #     scope: "firm:read firm:manage document:send"
  #   )
  #   redirect_to url
  #
  #   # 3. On callback, exchange the code for a token pair.
  #   tokens = EPostak::OAuth.exchange_code(
  #     code: params[:code],
  #     code_verifier: sessions[params[:state]],
  #     client_id: ENV.fetch("EPOSTAK_OAUTH_CLIENT_ID"),
  #     client_secret: ENV.fetch("EPOSTAK_OAUTH_CLIENT_SECRET"),
  #     redirect_uri: "https://your-app.com/oauth/epostak/callback"
  #   )
  module OAuth
    # Default origin for ePošťák OAuth endpoints. Override for staging.
    DEFAULT_ORIGIN = "https://epostak.sk"

    module_function

    # Generate a fresh PKCE code-verifier + S256 code-challenge pair.
    #
    # The `code_verifier` is 43 base64url characters (≈256 bits of entropy).
    # Store it server-side keyed by `state` — you must NOT round-trip it
    # through the user's browser, that defeats PKCE.
    #
    # @return [Hash{Symbol=>String}] `{ code_verifier:, code_challenge: }`
    def generate_pkce
      code_verifier = SecureRandom.urlsafe_base64(32, false).delete("=")
      code_challenge = base64url(Digest::SHA256.digest(code_verifier))
      { code_verifier: code_verifier, code_challenge: code_challenge }
    end

    # Build a `/oauth/authorize` URL the integrator can redirect the user to.
    # Always sets `response_type=code` and `code_challenge_method=S256`.
    #
    # @param client_id [String] Integrator OAuth client id.
    # @param redirect_uri [String] Exact-match registered redirect URI.
    # @param code_challenge [String] From {.generate_pkce}.
    # @param state [String] CSRF/session token; echoed back on the callback.
    # @param scope [String, nil] Optional space-separated subset of registered
    #   scopes.
    # @param origin [String, nil] Override the host (defaults to
    #   {DEFAULT_ORIGIN}).
    # @return [String] Absolute authorize URL.
    def build_authorize_url(client_id:, redirect_uri:, code_challenge:, state:, scope: nil, origin: nil)
      params = {
        "client_id" => client_id,
        "redirect_uri" => redirect_uri,
        "response_type" => "code",
        "code_challenge" => code_challenge,
        "code_challenge_method" => "S256",
        "state" => state
      }
      params["scope"] = scope if scope && !scope.empty?
      base = (origin || DEFAULT_ORIGIN).chomp("/")
      "#{base}/oauth/authorize?#{URI.encode_www_form(params)}"
    end

    # Exchange an authorization `code` for an access + refresh token pair on
    # the OAuth token endpoint. Hits `${origin}/api/oauth/token` directly —
    # does not route through the {EPostak::Client} base URL.
    #
    # The returned access token is a 15-minute JWT; the refresh token is
    # 30-day rotating. Persist both server-side keyed by your firm record.
    #
    # @param code [String] The authorization code from the callback.
    # @param code_verifier [String] The verifier paired with the
    #   `code_challenge` used when starting the flow.
    # @param client_id [String] Integrator OAuth client id.
    # @param client_secret [String] Integrator OAuth client secret.
    # @param redirect_uri [String] Must match the URI used in
    #   {.build_authorize_url}.
    # @param origin [String, nil] Override the host.
    # @return [Hash] `{ "access_token", "refresh_token", "token_type",
    #   "expires_in", "scope" }`
    # @raise [EPostak::Error] On non-2xx responses.
    def exchange_code(code:, code_verifier:, client_id:, client_secret:, redirect_uri:, origin: nil)
      base = (origin || DEFAULT_ORIGIN).chomp("/")
      uri = URI("#{base}/api/oauth/token")
      body = URI.encode_www_form(
        "grant_type" => "authorization_code",
        "code" => code,
        "code_verifier" => code_verifier,
        "client_id" => client_id,
        "client_secret" => client_secret,
        "redirect_uri" => redirect_uri
      )

      http = Net::HTTP.new(uri.host, uri.port)
      http.use_ssl = (uri.scheme == "https")
      http.open_timeout = 10
      http.read_timeout = 30

      request = Net::HTTP::Post.new(uri.request_uri)
      request["Content-Type"] = "application/x-www-form-urlencoded"
      request["Accept"] = "application/json"
      request.body = body

      begin
        response = http.request(request)
      rescue StandardError => e
        raise Error.new(0, { "error" => e.message })
      end

      status = response.code.to_i
      payload = response.body.to_s
      parsed =
        if payload.empty?
          {}
        else
          begin
            JSON.parse(payload)
          rescue JSON::ParserError
            { "error" => { "message" => payload } }
          end
        end

      if status >= 400
        headers = {}
        response.each_header { |k, v| headers[k] = v }
        raise EPostak.build_api_error(status, parsed.is_a?(Hash) ? parsed : {}, headers)
      end
      parsed.is_a?(Hash) ? parsed : {}
    end

    # @api private
    def base64url(bytes)
      [bytes].pack("m0").tr("+/", "-_").delete("=")
    end
  end
end
