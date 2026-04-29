# frozen_string_literal: true

module EPostak
  module Resources
    # Resource for the per-firm security/auth audit feed (Wave 3.4).
    #
    # Tenant-isolated: every row is filtered by the firm the calling key is
    # bound to. Integrators with multiple managed firms see only the firm
    # specified by `X-Firm-Id` (set automatically on the client when you pass
    # `firm_id` to `EPostak::Client.new(...)` or use `client.with_firm(...)`).
    #
    # Cursor pagination over `(occurred_at DESC, id DESC)` — pass the
    # `next_cursor` from one page back into the next call to walk the feed
    # deterministically, even across rows with identical timestamps.
    #
    # @example Walk the audit feed
    #   cursor = nil
    #   loop do
    #     page = client.audit.list(
    #       event: "jwt.issued",
    #       since: "2026-04-01T00:00:00Z",
    #       cursor: cursor,
    #       limit: 50
    #     )
    #     page["items"].each { |ev| puts "#{ev['occurred_at']} #{ev['event']}" }
    #     cursor = page["next_cursor"]
    #     break unless cursor
    #   end
    class Audit
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # List audit events for the current firm. Cursor-paginated.
      #
      # @param event [String, nil] Exact-match event filter (e.g. `"jwt.issued"`)
      # @param actor_type [String, nil] One of `"user"`, `"apiKey"`,
      #   `"integratorKey"`, `"system"`
      # @param since [String, nil] ISO-8601 lower bound (inclusive)
      # @param until_ts [String, nil] ISO-8601 upper bound (inclusive). Named
      #   `until_ts` because `until` is a reserved keyword in Ruby.
      # @param cursor [String, nil] Opaque cursor from a previous page
      # @param limit [Integer, nil] 1–100 (default 20)
      # @return [Hash] { "items" => [...], "next_cursor" => String|nil }
      def list(event: nil, actor_type: nil, since: nil, until_ts: nil, cursor: nil, limit: nil, **opts)
        # Allow `until:` via kwargs splat too — Ruby refuses `until` as a named
        # parameter, but callers can still pass `**{ until: "..." }`.
        until_value = until_ts || opts[:until] || opts["until"]

        query = {
          event: event,
          actor_type: actor_type,
          since: since,
          until: until_value,
          cursor: cursor,
          limit: limit
        }.compact

        @http.request(:get, "/audit", query: query)
      end
    end
  end
end
