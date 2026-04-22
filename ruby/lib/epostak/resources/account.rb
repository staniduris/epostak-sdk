# frozen_string_literal: true

module EPostak
  module Resources
    # Resource for retrieving account information -- firm details, subscription plan,
    # and document usage for the current billing period.
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

      # Inspect the authenticated API key, firm, plan, and rate limits.
      #
      # Useful for debugging credentials, verifying which firm an integrator
      # key is scoped to, and discovering per-minute rate limits.
      #
      # @return [Hash] Status hash with "key" (id/name/prefix/permissions/active/
      #   createdAt/lastUsedAt), "firm" (id/peppolStatus), "plan"
      #   (name/expiresAt/active), "rateLimit" (perMinute/window), and
      #   "integrator" ({id} for sk_int_* keys, otherwise nil).
      #
      # @example
      #   info = client.account.status
      #   puts "#{info['firm']['id']} on plan #{info['plan']['name']}"
      #   puts "Rate limit: #{info['rateLimit']['perMinute']}/min"
      def status
        @http.request(:get, "/auth/status")
      end

      # Rotate the plaintext secret for the current API key.
      #
      # The returned +key+ is shown exactly once -- store it immediately.
      # The previous secret is invalidated on success.
      #
      # Integrator keys (+sk_int_*+) cannot be rotated through this endpoint;
      # the server returns HTTP 403, which raises +EPostak::Error+.
      #
      # @return [Hash] {"key" => ..., "prefix" => ..., "message" => ...}
      #
      # @example
      #   new_key = client.account.rotate_secret
      #   store_secret(new_key["key"]) # shown only once
      def rotate_secret
        @http.request(:post, "/auth/rotate-secret")
      end
    end
  end
end
