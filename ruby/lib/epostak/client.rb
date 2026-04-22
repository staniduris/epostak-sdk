# frozen_string_literal: true

module EPostak
  # Main entry point for the ePosťák Enterprise API.
  #
  # Provides access to all API resources through attribute readers:
  # +documents+, +firms+, +peppol+, +webhooks+, +reporting+, +account+, +extract+.
  #
  # @example Direct API key (single firm)
  #   client = EPostak::Client.new(api_key: "sk_live_xxxxx")
  #   account = client.account.get
  #
  # @example Integrator key (multi-tenant)
  #   integrator = EPostak::Client.new(api_key: "sk_int_xxxxx")
  #   firm_a = integrator.with_firm("firm-a-uuid")
  #   firm_a.documents.inbox.list
  class Client
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

    # Create a new ePosťák API client.
    #
    # @param api_key [String] Your Enterprise API key. Use +sk_live_*+ for direct
    #   access or +sk_int_*+ for integrator (multi-tenant) access.
    # @param base_url [String] Base URL for the API. Override for staging or local testing.
    # @param firm_id [String, nil] Firm UUID to act on behalf of. Required when using
    #   integrator keys (+sk_int_*+). Sets the +X-Firm-Id+ header on each request.
    # @param max_retries [Integer] Maximum retries on 429/5xx responses (default: 3).
    #
    # @raise [ArgumentError] If api_key is nil or empty
    #
    # @example
    #   client = EPostak::Client.new(api_key: "sk_live_xxxxx")
    def initialize(api_key:, base_url: EPostak::DEFAULT_BASE_URL, firm_id: nil, max_retries: 3)
      raise ArgumentError, "api_key is required" if api_key.nil? || api_key.empty?

      @api_key     = api_key
      @base_url    = base_url
      @firm_id     = firm_id
      @max_retries = max_retries

      http = HttpClient.new(api_key: @api_key, base_url: @base_url, firm_id: @firm_id, max_retries: @max_retries)

      @documents = Resources::Documents.new(http)
      @firms     = Resources::Firms.new(http)
      @peppol    = Resources::Peppol.new(http)
      @webhooks  = Resources::Webhooks.new(http)
      @reporting = Resources::Reporting.new(http)
      @account   = Resources::Account.new(http)
      @extract   = Resources::Extract.new(http)
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
    #   integrator = EPostak::Client.new(api_key: "sk_int_xxxxx")
    #
    #   firm_a = integrator.with_firm("firm-a-uuid")
    #   firm_a.documents.send_document(body)
    #
    #   firm_b = integrator.with_firm("firm-b-uuid")
    #   firm_b.documents.inbox.list
    def with_firm(firm_id)
      self.class.new(api_key: @api_key, base_url: @base_url, firm_id: firm_id, max_retries: @max_retries)
    end

    # Validate a UBL XML document via the public +/api/validate+ endpoint.
    #
    # This is the PUBLIC validation endpoint and does *not* require an
    # API key; the SDK intentionally bypasses the +Authorization+ header
    # for this call. Rate-limited to 20 requests per minute per IP.
    #
    # @param xml [String] UBL 2.1 XML invoice or credit note as a string
    # @return [Hash] Full 3-layer Peppol BIS 3.0 validation report
    # @raise [EPostak::Error] On non-2xx responses
    #
    # @example
    #   client = EPostak::Client.new(api_key: "sk_live_xxx")
    #   report = client.validate(File.read("invoice.xml"))
    #   report["errors"].each { |e| puts e } unless report["valid"]
    def validate(xml)
      EPostak.validate(xml, base_url: derive_public_base_url(@base_url))
    end

    private

    # Strip the trailing "/enterprise" segment (if any) to derive the public API base URL.
    def derive_public_base_url(base_url)
      stripped = base_url.to_s.sub(%r{/+\z}, "")
      stripped.end_with?("/enterprise") ? stripped.sub(%r{/enterprise\z}, "") : stripped
    end
  end
end
