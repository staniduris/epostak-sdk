# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for managing received (inbound) documents in your inbox.
    # Provides methods to list, retrieve, and acknowledge incoming invoices.
    #
    # Accessed via +client.documents.inbox+.
    #
    # @example Process unacknowledged documents
    #   response = client.documents.inbox.list(status: "RECEIVED", limit: 50)
    #   response["documents"].each do |doc|
    #     process(doc)
    #     client.documents.inbox.acknowledge(doc["id"])
    #   end
    class Inbox
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # List documents in your inbox with optional filtering and pagination.
      #
      # @param offset [Integer] Number of documents to skip (default: 0)
      # @param limit [Integer] Maximum documents to return (default: 20, max: 100)
      # @param status [String, nil] Filter by status ("RECEIVED", "ACKNOWLEDGED")
      # @param since [String, nil] ISO 8601 timestamp to fetch documents received after this time
      # @return [Hash] Paginated response with "documents" array and "total" count
      #
      # @example
      #   response = client.documents.inbox.list(status: "RECEIVED", limit: 50)
      #   response["documents"].each { |doc| puts doc["id"] }
      def list(offset: 0, limit: 20, status: nil, since: nil)
        query = { offset: offset, limit: limit, status: status, since: since }
        @http.request(:get, "/documents/inbox", query: query)
      end

      # Retrieve a single inbox document by ID, including the raw UBL XML payload.
      #
      # @param id [String] Document UUID
      # @return [Hash] Document details with "document" and "payload" (UBL XML string)
      #
      # @example
      #   result = client.documents.inbox.get("doc-uuid")
      #   puts result["payload"] # => UBL XML string
      def get(id)
        @http.request(:get, "/documents/inbox/#{encode(id)}")
      end

      # Acknowledge (mark as processed) a received inbox document.
      # Once acknowledged, the document moves from RECEIVED to ACKNOWLEDGED status.
      #
      # @param id [String] Document UUID to acknowledge
      # @return [Hash] Acknowledgment confirmation with "acknowledgedAt" timestamp
      #
      # @example
      #   ack = client.documents.inbox.acknowledge("doc-uuid")
      #   puts ack["acknowledgedAt"] # => "2026-04-11T12:00:00Z"
      def acknowledge(id)
        @http.request(:post, "/documents/inbox/#{encode(id)}/acknowledge")
      end

      # List documents across all managed firms (integrator endpoint).
      # Only available with integrator API keys (+sk_int_*+).
      #
      # @param offset [Integer] Number of documents to skip (default: 0)
      # @param limit [Integer] Maximum documents to return (default: 50)
      # @param status [String, nil] Filter by status
      # @param since [String, nil] ISO 8601 timestamp filter
      # @param firm_id [String, nil] Filter by specific firm UUID
      # @return [Hash] Paginated list of documents across all firms
      #
      # @example
      #   response = client.documents.inbox.list_all(since: "2026-04-01T00:00:00Z", status: "RECEIVED")
      #   response["documents"].each { |doc| puts "#{doc['firmId']}: #{doc['id']}" }
      def list_all(offset: 0, limit: 50, status: nil, since: nil, firm_id: nil)
        query = { offset: offset, limit: limit, status: status, since: since, firm_id: firm_id }
        @http.request(:get, "/documents/inbox/all", query: query)
      end

      private

      # URI-encode a path segment.
      # @param value [String]
      # @return [String]
      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
