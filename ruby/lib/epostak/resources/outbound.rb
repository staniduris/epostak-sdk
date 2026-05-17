# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for the Pull API outbound (sent) documents.
    #
    # Preferred over the legacy +/documents/outbox+ endpoint; requires only
    # the +api-eligible+ plan instead of enterprise.
    #
    # Accessed via +client.outbound+.
    #
    # @example List sent documents
    #   page = client.outbound.list(limit: 50)
    #   page["documents"].each { |d| puts "#{d['id']}: #{d['status']}" }
    class Outbound
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # List sent outbound documents (cursor-paginated).
      #
      # @param since [String, nil] ISO 8601 lower bound (exclusive)
      # @param limit [Integer] Max documents to return (1–500, default 100)
      # @param kind [String, nil] Filter by document kind
      # @param status [String, nil] Filter by transport status
      # @param business_status [String, nil] Filter by business status
      # @param recipient [String, nil] Filter by recipient Peppol ID
      # @return [Hash] { "documents" => [...], "next_cursor" => String|nil, "has_more" => bool }
      def list(since: nil, limit: 100, kind: nil, status: nil, business_status: nil, recipient: nil)
        query = {
          since: since,
          limit: limit,
          kind: kind,
          status: status,
          business_status: business_status,
          recipient: recipient,
        }
        @http.request(:get, "/outbound/documents", query: query)
      end

      # Fetch a single outbound document by ID.
      #
      # @param id [String] Document UUID
      # @return [Hash] Full outbound document including +attempt_history+
      #
      # @example
      #   doc = client.outbound.get("doc-uuid")
      #   puts doc["status"]
      def get(id)
        @http.request(:get, "/outbound/documents/#{encode(id)}")
      end

      # Download the raw UBL XML for an outbound document.
      #
      # @param id [String] Document UUID
      # @return [String] UBL 2.1 XML as a raw string
      #
      # @example
      #   xml = client.outbound.get_ubl("doc-uuid")
      #   File.write("sent.xml", xml)
      def get_ubl(id)
        @http.request_raw(:get, "/outbound/documents/#{encode(id)}/ubl")
      end

      # Download the raw AS4 MDN receipt for an outbound document.
      #
      # @param id [String] Document UUID
      # @return [String] Raw MDN bytes
      def get_mdn(id)
        @http.request_raw(:get, "/outbound/documents/#{encode(id)}/mdn")
      end

      # Stream outbound document events (cursor-paginated).
      #
      # Each event has: +id+, +document_id+, +type+, +actor+, +detail+, +meta+,
      # +occurred_at+. Optionally filter by +document_id+.
      #
      # @param since [String, nil] ISO 8601 lower bound
      # @param limit [Integer, nil] Max events to return
      # @param document_id [String, nil] Filter to a single document
      # @param cursor [String, nil] Cursor from previous page
      # @return [Hash] { "events" => [...], "next_cursor" => String|nil, "has_more" => bool }
      #
      # @example Poll all events since last run
      #   result = client.outbound.events(cursor: last_cursor)
      #   result["events"].each { |e| puts "#{e['type']} @ #{e['occurred_at']}" }
      #   last_cursor = result["next_cursor"]
      def events(since: nil, limit: nil, document_id: nil, cursor: nil)
        query = { since: since, limit: limit, document_id: document_id, cursor: cursor }
        @http.request(:get, "/outbound/events", query: query)
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
