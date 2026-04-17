# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for managing firms (companies) associated with your account.
    # Integrators use this to assign client firms, view their documents,
    # and register Peppol identifiers.
    #
    # @example
    #   firms = client.firms.list
    #   firms["firms"].each { |f| puts "#{f['name']} (#{f['peppolStatus']})" }
    class Firms
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # List all firms associated with the current account.
      # For integrator keys, returns all assigned client firms.
      #
      # @return [Hash] Response with "firms" array of firm summaries
      #
      # @example
      #   response = client.firms.list
      #   response["firms"].each { |f| puts f["name"] }
      def list
        @http.request(:get, "/firms")
      end

      # Get detailed information about a specific firm, including tax IDs,
      # address, and all registered Peppol identifiers.
      #
      # @param id [String] Firm UUID
      # @return [Hash] Detailed firm information
      #
      # @example
      #   firm = client.firms.get("firm-uuid")
      #   puts firm["icDph"] # => "SK2020123456"
      def get(id)
        @http.request(:get, "/firms/#{encode(id)}")
      end

      # List documents belonging to a specific firm.
      # Useful for integrators to view a client's document history.
      #
      # @param id [String] Firm UUID
      # @param offset [Integer] Number of documents to skip (default: 0)
      # @param limit [Integer] Maximum documents to return (default: 20)
      # @param direction [String, nil] Filter by "inbound" or "outbound"
      # @return [Hash] Paginated list of the firm's documents
      #
      # @example
      #   response = client.firms.documents("firm-uuid", direction: "inbound", limit: 100)
      #   response["documents"].each { |doc| puts doc["id"] }
      def documents(id, offset: 0, limit: 20, direction: nil)
        query = { offset: offset, limit: limit, direction: direction }
        @http.request(:get, "/firms/#{encode(id)}/documents", query: query)
      end

      # Register a new Peppol identifier for a firm. This enables the firm
      # to send and receive documents on the Peppol network under this identifier.
      #
      # Slovak Peppol ID format: "0245:DIC" (e.g. "0245:1234567890"). Per
      # Slovak PASR, only the 0245 scheme is used — "9950:SK..." is not
      # supported.
      #
      # @param id [String] Firm UUID
      # @param scheme [String] Peppol identifier scheme (e.g. "0245")
      # @param identifier [String] Identifier value within the scheme (e.g. "1234567890")
      # @return [Hash] The registered identifier details
      #
      # @example
      #   result = client.firms.register_peppol_id("firm-uuid", scheme: "0245", identifier: "1234567890")
      #   puts result["peppolId"] # => "0245:1234567890"
      def register_peppol_id(id, scheme:, identifier:)
        @http.request(:post, "/firms/#{encode(id)}/peppol-identifiers", body: {
          scheme: scheme,
          identifier: identifier
        })
      end

      # Assign a firm to the integrator account by its Slovak ICO.
      # Once assigned, you can send/receive documents on behalf of this firm.
      #
      # @param ico [String] Slovak ICO (8-digit business registration number)
      # @return [Hash] The assigned firm details and status
      #
      # @example
      #   result = client.firms.assign(ico: "12345678")
      #   puts result["firm"]["id"] # => Use this UUID for subsequent operations
      def assign(ico:)
        @http.request(:post, "/firms/assign", body: { ico: ico })
      end

      # Assign multiple firms at once by their Slovak ICOs.
      # Each ICO is processed independently -- individual failures don't affect others.
      # Maximum 50 ICOs per request.
      #
      # @param icos [Array<String>] Array of Slovak ICOs
      # @return [Hash] Individual results for each ICO (success or error)
      #
      # @example
      #   result = client.firms.assign_batch(icos: ["12345678", "87654321", "11223344"])
      #   result["results"].each do |r|
      #     if r["error"]
      #       puts "#{r['ico']}: #{r['message']}"
      #     else
      #       puts "#{r['ico']}: #{r['status']}"
      #     end
      #   end
      def assign_batch(icos:)
        @http.request(:post, "/firms/assign/batch", body: { icos: icos })
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
