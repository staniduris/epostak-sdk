# frozen_string_literal: true

module EPostak
  module Resources
    # Sub-resource for searching the Peppol Business Card directory.
    # The directory contains publicly registered Peppol participants.
    #
    # Accessed via +client.peppol.directory+.
    #
    # @example
    #   results = client.peppol.directory.search(q: "consulting", country: "SK")
    #   results["results"].each { |r| puts r["name"] }
    class PeppolDirectory
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # Search the Peppol Business Card directory for registered participants.
      # Supports free-text search and country filtering with pagination.
      #
      # Requires +documents:read+ scope.
      #
      # @param q [String, nil] Free-text search query (company name, ID, etc.)
      # @param country [String, nil] ISO 3166-1 alpha-2 country code filter (e.g. "SK", "CZ")
      # @param page [Integer, nil] Page number (1-based)
      # @param page_size [Integer, nil] Results per page
      # @return [Hash] Paginated search results with "results" array and "total" count
      #
      # @example Search for Slovak companies
      #   results = client.peppol.directory.search(q: "consulting", country: "SK", page_size: 50)
      #   puts "Found #{results['total']} participants"
      def search(q: nil, country: nil, page: nil, page_size: nil)
        query = { q: q, country: country, page: page, page_size: page_size }
        @http.request(:get, "/peppol/directory/search", query: query)
      end
    end
  end
end
