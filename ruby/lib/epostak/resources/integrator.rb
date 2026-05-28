# frozen_string_literal: true

module EPostak
  module Resources
    # Integrator-aggregate endpoints (+sk_int_*+ keys only).
    #
    # Access via +client.integrator.licenses.info+. For the per-firm
    # +/account+ and +/licenses/info+ views (which integrators also reach via
    # +X-Firm-Id+), use +client.account+ instead.
    #
    # @example
    #   usage = client.integrator.licenses.info(limit: 100)
    #   puts usage["billable"]["totalCharge"], "EUR this month"
    class Integrator
      # @return [Resources::IntegratorKeys] Integrator API-key management.
      attr_reader :keys

      # @return [Resources::IntegratorLicenses] Billing aggregate views.
      attr_reader :licenses

      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @keys = IntegratorKeys.new(http)
        @licenses = IntegratorLicenses.new(http)
      end
    end

    # +/integrator/keys+ -- integrator API-key management.
    class IntegratorKeys
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # List all API keys for the current integrator.
      #
      # @return [Hash] Response with +keys+
      def list
        @http.request(:get, "/integrator/keys")
      end

      # Deactivate an integrator API key by UUID or +sk_int_*+ prefix.
      #
      # @param key_id [String, nil] API key UUID
      # @param client_id [String, nil] +sk_int_*+ key prefix/client ID
      # @return [Hash] Success response
      def deactivate(key_id: nil, client_id: nil)
        body = {}
        body[:keyId] = key_id unless key_id.nil?
        body[:client_id] = client_id unless client_id.nil?
        @http.request(:delete, "/integrator/keys", body: body)
      end
    end

    # +/integrator/licenses/*+ -- billing aggregate views.
    #
    # Tier rates are applied to the AGGREGATE document count across all the
    # integrator's +integrator-managed+ firms. A 100-firm x 50-doc integrator
    # lands in tier 2-3, not tier 1 like a standalone firm would. Volumes
    # above +contactThreshold+ (5 000 / month) flip +exceedsAutoTier+ to
    # +true+; auto-billing pauses there and sales handles invoicing manually.
    class IntegratorLicenses
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Aggregate plan + current-period usage across managed firms.
      #
      # Wraps +GET /api/v1/integrator/licenses/info+. Requires the
      # +account:read+ scope on a +sk_int_*+ integrator key. No +X-Firm-Id+
      # header -- the endpoint is integrator-scoped, not firm-scoped.
      #
      # @param offset [Integer] Pagination offset for the per-firm list
      #   (default 0).
      # @param limit [Integer] Page size for the per-firm list, max 100
      #   (default 50).
      # @return [Hash] Response with +integrator+, +period+, +nextResetAt+,
      #   +billable+ (managed-plan aggregate + tier-applied charges),
      #   +nonManaged+, +exceedsAutoTier+, +contactThreshold+,
      #   +pricing+ ({+outboundTiers+, +inboundApiTiers+}), paginated +firms+
      #   rows, and +pagination+.
      # @raise [EPostak::Error] On API error
      #
      # @example
      #   usage = client.integrator.licenses.info(limit: 100)
      #   if usage["exceedsAutoTier"]
      #     # sales review required, auto-billing has paused
      #   end
      def info(offset: 0, limit: 50)
        @http.request(:get, "/integrator/licenses/info", query: { offset: offset, limit: limit })
      end
    end
  end
end
