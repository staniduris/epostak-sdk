# frozen_string_literal: true

module EPostak
  module Resources
    # Resource for retrieving document statistics and reports.
    #
    # @example
    #   stats = client.reporting.statistics(from: "2026-01-01", to: "2026-03-31")
    #   puts "Sent: #{stats['outbound']['total']}, Received: #{stats['inbound']['total']}"
    class Reporting
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Get aggregated document statistics for a time period.
      # Returns counts of sent/received documents, delivery success rates,
      # and acknowledgment status.
      #
      # @param from [String, nil] Start date in ISO 8601 format (defaults to 30 days ago)
      # @param to [String, nil] End date in ISO 8601 format (defaults to today)
      # @return [Hash] Aggregated statistics with "outbound" and "inbound" sections
      #
      # @example Get stats for Q1 2026
      #   stats = client.reporting.statistics(from: "2026-01-01", to: "2026-03-31")
      #   puts "Delivery rate: #{stats['outbound']['delivered']}/#{stats['outbound']['total']}"
      #   puts "Pending inbox: #{stats['inbound']['pending']}"
      def statistics(from: nil, to: nil)
        query = { from: from, to: to }
        @http.request(:get, "/reporting/statistics", query: query)
      end
    end
  end
end
