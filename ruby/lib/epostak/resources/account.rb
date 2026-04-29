# frozen_string_literal: true

module EPostak
  module Resources
    # Resource for retrieving account information — firm details, subscription
    # plan, and document usage for the current billing period.
    #
    # For key introspection, OAuth token minting, and key rotation see
    # `client.auth.*`. The v1 helpers `client.account.status` and
    # `client.account.rotate_secret` were removed in 2.0.0.
    #
    # @example
    #   account = client.account.get
    #   puts "Plan: #{account['plan']['name']} (#{account['plan']['status']})"
    #   puts "Usage: #{account['usage']['outbound']} sent, #{account['usage']['inbound']} received"
    class Account
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Get account information for the authenticated API key.
      # Returns the associated firm, current subscription plan, and usage counters.
      #
      # @return [Hash] Account details with "firm", "plan", and "usage" sections
      #
      # @example
      #   account = client.account.get
      #   if account["plan"]["status"] == "expired"
      #     puts "Subscription expired -- renew to continue sending documents"
      #   end
      def get
        @http.request(:get, "/account")
      end
    end
  end
end
