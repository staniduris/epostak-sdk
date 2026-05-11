# frozen_string_literal: true

require "erb"

module EPostak
  module Resources
    # Resource for the Pull API inbound (received) documents.
    #
    # This is the preferred path for reading received invoices. Unlike the
    # legacy +/documents/inbox+ endpoint (enterprise-only), this resource
    # requires only the +api-eligible+ plan.
    #
    # Accessed via +client.inbound+.
    #
    # @example List and acknowledge received documents
    #   page = client.inbound.list(limit: 50)
    #   page["documents"].each do |doc|
    #     process(doc)
    #     client.inbound.ack(doc["id"])
    #   end
    class Inbound
      # @param http [EPostak::HttpClient] Internal HTTP client
      def initialize(http)
        @http = http
      end

      # List received inbound documents (cursor-paginated).
      #
      # @param since [String, nil] ISO 8601 lower bound (exclusive)
      # @param limit [Integer] Max documents to return (1–500, default 100)
      # @param kind [String, nil] Filter by document kind (e.g. "invoice", "credit_note", "self_billing")
      # @param sender [String, nil] Filter by sender Peppol ID
      # @return [Hash] { "documents" => [...], "next_cursor" => String|nil, "has_more" => bool }
      #
      # @example
      #   page = client.inbound.list(since: "2026-05-01T00:00:00Z", limit: 100)
      #   page["documents"].each { |d| puts d["id"] }
      def list(since: nil, limit: 100, kind: nil, sender: nil)
        query = { since: since, limit: limit, kind: kind, sender: sender }
        @http.request(:get, "/inbound/documents", query: query)
      end

      # Fetch a single inbound document by ID.
      #
      # @param id [String] Document UUID
      # @return [Hash] Full inbound document object
      #
      # @example
      #   doc = client.inbound.get("doc-uuid")
      #   puts doc["senderPeppolId"]
      def get(id)
        @http.request(:get, "/inbound/documents/#{encode(id)}")
      end

      # Download the raw UBL XML for an inbound document.
      #
      # @param id [String] Document UUID
      # @return [String] UBL 2.1 XML as a raw string
      # @raise [EPostak::Error] 404 when no XML is available (legacy rows without rawXmlPath)
      #
      # @example
      #   xml = client.inbound.get_ubl("doc-uuid")
      #   File.write("received.xml", xml)
      def get_ubl(id)
        @http.request_raw(:get, "/inbound/documents/#{encode(id)}/ubl")
      end

      # Acknowledge a received inbound document (marks it as processed on your side).
      #
      # Idempotent — re-acknowledging the same document overwrites +clientAckedAt+
      # with the latest timestamp.
      #
      # @param id [String] Document UUID
      # @param client_reference [String, nil] Optional client-side reference (max 256 chars)
      # @return [Hash] Updated inbound document shape
      #
      # @example
      #   result = client.inbound.ack("doc-uuid", client_reference: "PROC-2026-0042")
      #   puts result["clientAckedAt"]
      def ack(id, client_reference: nil)
        body = {}
        body[:client_reference] = client_reference if client_reference
        @http.request(:post, "/inbound/documents/#{encode(id)}/ack", body: body)
      end

      private

      def encode(value)
        ERB::Util.url_encode(value)
      end
    end
  end
end
