# frozen_string_literal: true

module EPostak
  # Main entry point for the ePošťák API.
  #
  # Provides access to all API resources through attribute readers:
  # +auth+, +audit+, +documents+, +firms+, +peppol+, +webhooks+, +reporting+,
  # +account+, +extract+.
  #
  # @example Direct OAuth (single firm)
  #   client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "secret")
  #   account = client.account.get
  #
  # @example Integrator OAuth (multi-tenant)
  #   integrator = EPostak::Client.new(client_id: "sk_int_xxxxx", client_secret: "secret")
  #   firm_a = integrator.with_firm("firm-a-uuid")
  #   firm_a.documents.inbox.list
  class Client
    # @return [Resources::Auth] OAuth token mint/renew/revoke + key
    #   introspection, rotation, IP allowlist
    attr_reader :auth

    # @return [Resources::Audit] Per-firm audit feed (cursor-paginated)
    attr_reader :audit

    # @return [Resources::Documents] Send and receive documents via Peppol
    attr_reader :documents

    # @return [Resources::Firms] Manage client firms (integrator keys)
    attr_reader :firms

    # @return [Resources::Peppol] SMP lookup and Peppol directory search
    attr_reader :peppol

    # @return [Resources::Webhooks] Manage webhook subscriptions and pull queue
    attr_reader :webhooks

    # @return [Resources::Reporting] Document statistics and reports
    attr_reader :reporting

    # @return [Resources::Account] Account and firm information
    attr_reader :account

    # @return [Resources::Extract] AI-powered OCR extraction from PDFs and images
    attr_reader :extract

    # @return [Resources::Integrator] Integrator-aggregate endpoints (+sk_int_*+ keys)
    attr_reader :integrator

    # Create a new ePošťák API client.
    #
    # @param client_id [String] OAuth client ID (your +sk_live_*+ or +sk_int_*+ key).
    # @param client_secret [String] OAuth client secret.
    # @param base_url [String] Base URL for the API. Defaults to
    #   +https://epostak.sk/api/v1+. Override for staging or local testing.
    # @param firm_id [String, nil] Firm UUID to act on behalf of. Required when
    #   using integrator keys (+sk_int_*+). Sets the +X-Firm-Id+ header on
    #   each request.
    # @param max_retries [Integer] Maximum retries on 429/5xx responses
    #   (default: 3).
    # @param token_manager [EPostak::TokenManager, nil] Shared token manager
    #   (used internally by {#with_firm}).
    #
    # @raise [ArgumentError] If client_id or client_secret is nil or empty
    #
    # @example
    #   client = EPostak::Client.new(client_id: "sk_live_xxxxx", client_secret: "secret")
    def initialize(client_id:, client_secret:, base_url: EPostak::DEFAULT_BASE_URL, firm_id: nil, max_retries: 3, token_manager: nil)
      raise ArgumentError, "client_id is required" if client_id.nil? || client_id.empty?
      raise ArgumentError, "client_secret is required" if client_secret.nil? || client_secret.empty?

      @client_id     = client_id
      @client_secret = client_secret
      @base_url      = base_url
      @firm_id       = firm_id
      @max_retries   = max_retries

      @token_manager = token_manager || TokenManager.new(
        client_id: @client_id,
        client_secret: @client_secret,
        base_url: @base_url,
        firm_id: @firm_id
      )

      http = HttpClient.new(token_manager: @token_manager, base_url: @base_url, firm_id: @firm_id, max_retries: @max_retries)

      @auth      = Resources::Auth.new(http, base_url: @base_url)
      @audit     = Resources::Audit.new(http)
      @documents = Resources::Documents.new(http)
      @firms     = Resources::Firms.new(http)
      @peppol    = Resources::Peppol.new(http)
      @webhooks  = Resources::Webhooks.new(http)
      @reporting = Resources::Reporting.new(http)
      @account   = Resources::Account.new(http)
      @extract   = Resources::Extract.new(http)
      @integrator = Resources::Integrator.new(http)
    end

    # Create a new client instance scoped to a specific firm.
    #
    # Useful when an integrator key (+sk_int_*+) needs to switch between
    # client firms without rebuilding the entire configuration.
    #
    # @param firm_id [String] The firm UUID to scope subsequent requests to
    # @return [EPostak::Client] A new client with the +X-Firm-Id+ header set
    #
    # @example
    #   integrator = EPostak::Client.new(client_id: "sk_int_xxxxx", client_secret: "secret")
    #
    #   firm_a = integrator.with_firm("firm-a-uuid")
    #   firm_a.documents.send_document(body)
    #
    #   firm_b = integrator.with_firm("firm-b-uuid")
    #   firm_b.documents.inbox.list
    def with_firm(firm_id)
      self.class.new(
        client_id: @client_id,
        client_secret: @client_secret,
        base_url: @base_url,
        firm_id: firm_id,
        max_retries: @max_retries,
        token_manager: @token_manager
      )
    end

    # Validate a UBL XML document via the public +/api/validate+ endpoint.
    #
    # This is the PUBLIC validation endpoint and does *not* require an API
    # key; the SDK intentionally bypasses the +Authorization+ header for
    # this call. Rate-limited to 20 requests per minute per IP.
    #
    # @param xml [String] UBL 2.1 XML invoice or credit note as a string
    # @return [Hash] Full 3-layer Peppol BIS 3.0 validation report
    # @raise [EPostak::Error] On non-2xx responses
    #
    # @example
    #   client = EPostak::Client.new(client_id: "sk_live_xxx", client_secret: "secret")
    #   report = client.validate(File.read("invoice.xml"))
    #   report["errors"].each { |e| puts e } unless report["valid"]
    def validate(xml)
      EPostak.validate(xml, base_url: derive_public_base_url(@base_url))
    end

    private

    # Strip a trailing `/v1` (or legacy `/enterprise`) segment to derive the
    # public API base URL. The public validator lives directly under
    # `https://epostak.sk/api`.
    def derive_public_base_url(base_url)
      stripped = base_url.to_s.sub(%r{/+\z}, "")
      %w[/v1 /enterprise].each do |suffix|
        return stripped.sub(%r{#{suffix}\z}, "") if stripped.end_with?(suffix)
      end
      stripped
    end
  end
end
