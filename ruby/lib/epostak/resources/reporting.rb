# frozen_string_literal: true

module EPostak
  module Resources
    # Resource for retrieving document statistics and reports.
    #
    # @example
    #   stats = client.reporting.statistics(period: "month")
    #   puts "Sent: #{stats['sent']['total']}"
    #   puts "Delivery rate: #{stats['delivery_rate']}"
    #   puts "Top recipients:", stats["top_recipients"]
    class Reporting
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Get aggregated document statistics for a time period.
      #
      # In v2 the response shape was rebuilt:
      # * `period` — echoed window
      # * `sent` — `{ "total" => N, "by_type" => {...} }`
      # * `received` — `{ "total" => N, "by_type" => {...} }`
      # * `delivery_rate` — float in `[0, 1]`
      # * `top_recipients` — array of `StatisticsTopParty`
      # * `top_senders` — array of `StatisticsTopParty`
      #
      # @param from [String, nil] Start date (ISO 8601) — defaults server-side
      # @param to [String, nil] End date (ISO 8601) — defaults server-side
      # @param period [String, nil] One of `"month"`, `"quarter"`, `"year"`.
      #   When supplied, the server derives `from`/`to` for you.
      # @return [Hash] v2 statistics envelope
      #
      # @example Get last month
      #   stats = client.reporting.statistics(period: "month")
      #   puts "#{stats['sent']['total']} sent"
      #
      # @example Custom range
      #   stats = client.reporting.statistics(from: "2026-01-01", to: "2026-03-31")
      def statistics(from: nil, to: nil, period: nil)
        query = { from: from, to: to, period: period }.compact
        @http.request(:get, "/reporting/statistics", query: query)
      end
    end
  end
end
